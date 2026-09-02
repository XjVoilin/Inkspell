using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>驱动一次有界自动挑战，并按固定顺序结算生成、命中、移动、施法与胜负。</summary>
    public sealed class AutoBattleSystem : SystemBase, IUpdatableSystem
    {
        private BattleRun _currentRun = new();
        private SpellAssetStore _spellAssets;
        private TbBattleRule _battleRule;
        private TbSpellAssetRule _assetRule;
        private TbSpellCombat _spellCombat;
        private TbSpellUpgrade _spellUpgrades;
        private TbEnemy _enemies;
        private TbStageBattle _stages;
        private UniTaskCompletionSource<BattleOutcome> _battleCompletion;
        private long _nextBattleRunId = 1;

        internal BattleRun CurrentRun => _currentRun;

        public async UniTask<BattleOutcome> RunChallengeAsync(
            int stageId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (_battleCompletion != null)
            {
                throw new InvalidOperationException("同一时间只能运行一次自动战斗挑战。");
            }

            var completion = new UniTaskCompletionSource<BattleOutcome>();
            _battleCompletion = completion;

            try
            {
                BeginChallenge(stageId);
                return await completion.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                CleanupChallenge(completion);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_battleCompletion == null || !CurrentRun.IsRunning)
            {
                return;
            }

            try
            {
                AdvanceBattle(deltaTime);
            }
            catch (Exception exception)
            {
                if (CurrentRun.IsRunning)
                {
                    StopChallenge();
                    Publish(new BattleStateChangedEvent());
                }

                // 通过 RunChallengeAsync 的单一异步通道向调用方报告错误，
                // 避免同一异常又从 Unity Update 重复抛出。
                _battleCompletion?.TrySetException(exception);
            }
        }

        protected override UniTask OnInitializeAsync()
        {
            _spellAssets = GetStore<SpellAssetStore>();

            var config = GetSystem<IConfigSystem>();
            _battleRule = config.GetTable<TbBattleRule>();
            _assetRule = config.GetTable<TbSpellAssetRule>();
            _spellCombat = config.GetTable<TbSpellCombat>();
            _spellUpgrades = config.GetTable<TbSpellUpgrade>();
            _enemies = config.GetTable<TbEnemy>();
            _stages = config.GetTable<TbStageBattle>();
            return UniTask.CompletedTask;
        }

        private void BeginChallenge(int stageId)
        {
            var stage = _stages.Get(stageId);
            _currentRun = new BattleRun(
                _nextBattleRunId++,
                stage.StageId,
                _battleRule.BookMaxHealth,
                _assetRule.EquipmentSlotCount);

            SpawnEnemiesAtChallengeStart(stage);
            Publish(new BattleStateChangedEvent());
        }

        private void CleanupChallenge(UniTaskCompletionSource<BattleOutcome> completion)
        {
            if (!ReferenceEquals(_battleCompletion, completion))
            {
                return;
            }

            if (CurrentRun.IsRunning)
            {
                StopChallenge();
                Publish(new BattleStateChangedEvent());
            }

            _battleCompletion = null;
        }

        private void AdvanceBattle(float deltaTime)
        {
            var stage = _stages.Get(CurrentRun.StageId);
            var previousSpawnElapsed = CurrentRun.SpawnElapsedSeconds;

            // 结算顺序属于战斗规则：新敌人/已到达攻击先结算，再判胜、移动攻击、判负、最后施法。
            AdvanceTimers(deltaTime);
            AdvanceSpawnProgress(deltaTime);
            SpawnEnemies(stage, previousSpawnElapsed, CurrentRun.SpawnElapsedSeconds);
            ResolveArrivedSpellAttacks();
            CurrentRun.Enemies.RemoveDefeated();
            RemoveExpiredEffects();

            if (CurrentRun.Enemies.Count == 0 && !HasPendingSpawns(stage))
            {
                FinalizeChallenge(true);
                return;
            }

            AdvanceEnemies(deltaTime);
            if (CurrentRun.Book.IsDestroyed && CurrentRun.Enemies.Count > 0)
            {
                FinalizeChallenge(false);
                return;
            }

            CastReadySpells();
            Publish(new BattleStateChangedEvent());
        }

        private void SpawnEnemiesAtChallengeStart(StageBattle stage)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds != 0f)
                {
                    continue;
                }

                SpawnEnemy(spawn.EnemyType);
            }
        }

        private void SpawnEnemies(StageBattle stage, float previousElapsed, float currentElapsed)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds <= previousElapsed ||
                    spawn.SpawnTimeSeconds > currentElapsed)
                {
                    continue;
                }

                SpawnEnemy(spawn.EnemyType);
            }
        }

        private void SpawnEnemy(EnemyType enemyType)
        {
            var enemy = _enemies.Get(enemyType);
            CurrentRun.Enemies.Spawn(
                enemyType,
                enemy.MaxHealth,
                _battleRule.EnemySpawnPosition,
                enemy.AttackIntervalSeconds);
        }

        private bool HasPendingSpawns(StageBattle stage)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds > CurrentRun.SpawnElapsedSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveArrivedSpellAttacks()
        {
            var arrivedAttackIds = new List<long>();
            foreach (var attack in CurrentRun.Attacks)
            {
                if (attack.RemainingTravelSeconds <= 0f)
                {
                    arrivedAttackIds.Add(attack.AttackId);
                }
            }

            foreach (var attackId in arrivedAttackIds)
            {
                var attack = FindAttack(attackId);
                if (attack.SpellType == SpellType.Shield)
                {
                    CurrentRun.Book.ApplyShield(
                        attack.Shield,
                        attack.EffectDurationSeconds);
                    AddEffect(
                        attack.SpellType,
                        0,
                        _battleRule.BookContactPosition,
                        attack.EffectDurationSeconds);
                }
                else
                {
                    ResolveDamageAttack(attack);
                }

                RemoveAttack(attackId);
            }
        }

        private void ResolveDamageAttack(BattleAttack attack)
        {
            foreach (var targetId in attack.TargetEnemyIds)
            {
                var target = CurrentRun.Enemies.FindLiving(targetId);
                if (target == null)
                {
                    continue;
                }

                target.ApplyDamage(attack.Damage);
                if (attack.SpellType == SpellType.FrostRing)
                {
                    target.ApplySlow(
                        attack.EffectDurationSeconds,
                        attack.SlowMultiplier);
                }
            }

            if (attack.EffectDurationSeconds > 0f)
            {
                AddEffect(
                    attack.SpellType,
                    attack.TargetEnemyIds.Count > 0 ? attack.TargetEnemyIds[0] : 0,
                    attack.TargetPathPosition,
                    attack.EffectDurationSeconds);
            }
        }

        private void RemoveExpiredEffects()
        {
            var expiredEffectIds = new List<long>();
            foreach (var effect in CurrentRun.Effects)
            {
                if (effect.RemainingSeconds <= 0f)
                {
                    expiredEffectIds.Add(effect.EffectId);
                }
            }

            foreach (var effectId in expiredEffectIds)
            {
                RemoveEffect(effectId);
            }
        }

        private void AdvanceEnemies(float deltaTime)
        {
            foreach (var enemy in CurrentRun.Enemies.Items)
            {
                var config = _enemies.Get(enemy.Type);
                enemy.MoveTowards(
                    _battleRule.BookContactPosition,
                    config.MoveSpeedPerSecond,
                    deltaTime);

                if (enemy.CanAttack(_battleRule.BookContactPosition))
                {
                    CurrentRun.Book.ApplyDamage(config.AttackDamage);
                    enemy.ResetAttack(config.AttackIntervalSeconds);
                }
            }
        }

        private void CastReadySpells()
        {
            foreach (var cooldown in CurrentRun.Cooldowns.Items)
            {
                if (cooldown.RemainingSeconds > 0f)
                {
                    continue;
                }

                var target = CurrentRun.Enemies.FindNearestToBook();
                SpellInstance spell;
                if (!_spellAssets.TryGetEquippedSpell(
                        cooldown.EquipmentSlot,
                        out spell) ||
                    target == null)
                {
                    continue;
                }

                CastSpell(cooldown.EquipmentSlot, spell, target);
            }
        }

        private void CastSpell(
            int equipmentSlot,
            SpellInstance spell,
            BattleEnemy primaryTarget)
        {
            var combat = _spellCombat.Get(spell.Type, spell.Tier);
            var upgrade = _spellUpgrades.Get(spell.Type, spell.Tier, spell.Level);
            var targetIds = SelectTargets(combat, primaryTarget);
            var damage = combat.BaseDamage * upgrade.CurrentPowerMultiplier;
            var shield = combat.BaseShield * upgrade.CurrentPowerMultiplier;
            var targetPosition = spell.Type == SpellType.Shield
                ? _battleRule.BookContactPosition
                : primaryTarget.PathPosition;

            LaunchAttack(
                equipmentSlot,
                spell.Type,
                targetIds,
                targetPosition,
                _battleRule.AttackTravelSeconds,
                damage,
                shield,
                combat.EffectRange,
                combat.EffectDurationSeconds,
                combat.SlowMultiplier);
            CurrentRun.Cooldowns.Set(equipmentSlot, combat.CooldownSeconds);
        }

        private IReadOnlyList<long> SelectTargets(
            SpellCombat combat,
            BattleEnemy primaryTarget)
        {
            switch (combat.SpellType)
            {
                case SpellType.Fireball:
                    return new[] { primaryTarget.RuntimeId };
                case SpellType.ChainLightning:
                    return CurrentRun.Enemies.SelectChainTargets(
                        primaryTarget,
                        combat.ChainTargetCount,
                        combat.EffectRange);
                case SpellType.FrostRing:
                    return CurrentRun.Enemies.SelectAreaTargets(
                        primaryTarget.PathPosition,
                        combat.EffectRange);
                case SpellType.Shield:
                    return Array.Empty<long>();
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(combat.SpellType),
                        combat.SpellType,
                        null);
            }
        }

        private BattleAttack FindAttack(long attackId)
        {
            return CurrentRun.Attacks.Find(attack => attack.AttackId == attackId)
                   ?? throw new InvalidOperationException($"攻击不存在: {attackId}");
        }

        private void StopChallenge()
        {
            CurrentRun.IsRunning = false;
        }

        private void AdvanceSpawnProgress(float deltaTime)
        {
            CurrentRun.SpawnElapsedSeconds += deltaTime;
        }

        private void LaunchAttack(
            int equipmentSlot,
            SpellType spellType,
            IReadOnlyList<long> targetEnemyIds,
            float targetPathPosition,
            float travelSeconds,
            float damage,
            float shield,
            float effectRange,
            float effectDurationSeconds,
            float slowMultiplier)
        {
            var attackId = CurrentRun.NextAttackId++;
            CurrentRun.Attacks.Add(new BattleAttack
            {
                AttackId = attackId,
                EquipmentSlot = equipmentSlot,
                SpellType = spellType,
                TargetEnemyIds = new List<long>(targetEnemyIds),
                TargetPathPosition = targetPathPosition,
                RemainingTravelSeconds = travelSeconds,
                Damage = damage,
                Shield = shield,
                EffectRange = effectRange,
                EffectDurationSeconds = effectDurationSeconds,
                SlowMultiplier = slowMultiplier,
            });
        }

        private void RemoveAttack(long attackId)
        {
            CurrentRun.Attacks.Remove(FindAttack(attackId));
        }

        private void AddEffect(
            SpellType spellType,
            long targetEnemyId,
            float pathPosition,
            float remainingSeconds)
        {
            var effectId = CurrentRun.NextEffectId++;
            CurrentRun.Effects.Add(new BattleEffect
            {
                EffectId = effectId,
                SpellType = spellType,
                TargetEnemyId = targetEnemyId,
                PathPosition = pathPosition,
                RemainingSeconds = remainingSeconds,
            });
        }

        private void RemoveEffect(long effectId)
        {
            CurrentRun.Effects.Remove(FindEffect(effectId));
        }

        private void AdvanceTimers(float deltaTime)
        {
            CurrentRun.Book.Tick(deltaTime);
            CurrentRun.Enemies.Tick(deltaTime);
            CurrentRun.Cooldowns.Tick(deltaTime);

            foreach (var attack in CurrentRun.Attacks)
            {
                attack.RemainingTravelSeconds = Math.Max(
                    0f,
                    attack.RemainingTravelSeconds - deltaTime);
            }

            foreach (var effect in CurrentRun.Effects)
            {
                effect.RemainingSeconds = Math.Max(
                    0f,
                    effect.RemainingSeconds - deltaTime);
            }
        }

        private BattleOutcome FinalizeOutcome(bool victory)
        {
            // Outcome 是一次性终态；重复结算说明战斗时序被破坏，应立即暴露。
            if (CurrentRun.Outcome.HasValue)
            {
                throw new InvalidOperationException("当前挑战已经产生最终结果。");
            }

            var outcome = new BattleOutcome(
                CurrentRun.BattleRunId,
                CurrentRun.StageId,
                victory);
            CurrentRun.Outcome = outcome;
            CurrentRun.IsRunning = false;
            return outcome;
        }

        private BattleEffect FindEffect(long effectId)
        {
            return CurrentRun.Effects.Find(effect => effect.EffectId == effectId)
                   ?? throw new KeyNotFoundException($"效果不存在: {effectId}");
        }

        private void FinalizeChallenge(bool victory)
        {
            var outcome = FinalizeOutcome(victory);
            Publish(new BattleStateChangedEvent());
            Publish(new BattleChallengeEndedEvent(outcome));
            (_battleCompletion ?? throw new InvalidOperationException("当前战斗缺少完成源。"))
                .TrySetResult(outcome);
        }
    }
}
