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
        private AutoBattleState _state = new();
        private SpellAssetStore _spellAssets;
        private TbBattleRule _battleRule;
        private TbSpellAssetRule _assetRule;
        private TbSpellCombat _spellCombat;
        private TbSpellUpgrade _spellUpgrades;
        private TbEnemy _enemies;
        private TbStageBattle _stages;
        private BattleChallengeProcedure _currentProcedure;

        internal AutoBattleState CurrentState => _state;

        public async UniTask<BattleOutcome> RunChallengeAsync(
            int stageId,
            CancellationToken ct = default)
        {
            if (_currentProcedure != null)
            {
                throw new InvalidOperationException("同一时间只能运行一次自动战斗挑战。");
            }

            var procedure = new BattleChallengeProcedure(stageId);
            _currentProcedure = procedure;

            try
            {
                await RunProcedure(procedure, ct);
                return procedure.Outcome;
            }
            finally
            {
                EndChallenge(procedure);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_currentProcedure == null || !CurrentState.IsRunning)
            {
                return;
            }

            try
            {
                AdvanceBattle(deltaTime);
            }
            catch (Exception exception)
            {
                if (CurrentState.IsRunning)
                {
                    StopChallenge();
                    Publish(new BattleStateChangedEvent());
                }

                _currentProcedure?.Fail(exception);
                throw;
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

        internal void StartChallenge(BattleChallengeProcedure procedure, int stageId)
        {
            if (!ReferenceEquals(_currentProcedure, procedure))
            {
                throw new InvalidOperationException("挑战 Procedure 与当前运行引用不一致。");
            }

            var stage = _stages.Get(stageId);
            var challengeId = CurrentState.ChallengeId + 1;
            StartChallengeState(
                challengeId,
                stageId,
                _battleRule.BookMaxHealth,
                _assetRule.EquipmentSlotCount);

            SpawnEnemiesAtChallengeStart(stage);
            Publish(new BattleStateChangedEvent());
        }

        private void EndChallenge(BattleChallengeProcedure procedure)
        {
            if (!ReferenceEquals(_currentProcedure, procedure))
            {
                return;
            }

            if (CurrentState.IsRunning)
            {
                StopChallenge();
                Publish(new BattleStateChangedEvent());
            }

            _currentProcedure = null;
        }

        private void AdvanceBattle(float deltaTime)
        {
            var stage = _stages.Get(CurrentState.StageId);
            var previousSpawnElapsed = CurrentState.SpawnElapsedSeconds;

            // 结算顺序属于战斗规则：新敌人/已到达攻击先结算，再判胜、移动攻击、判负、最后施法。
            AdvanceTimers(deltaTime);
            AdvanceSpawnProgress(deltaTime);
            SpawnEnemies(stage, previousSpawnElapsed, CurrentState.SpawnElapsedSeconds);
            ResolveArrivedSpellAttacks();
            RemoveDefeatedEnemies();
            RemoveExpiredEffects();

            if (CurrentState.Enemies.Count == 0 && !HasPendingSpawns(stage))
            {
                FinalizeChallenge(true);
                return;
            }

            AdvanceEnemies(deltaTime);
            if (CurrentState.BookHealth <= 0f && CurrentState.Enemies.Count > 0)
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
            SpawnEnemyState(
                enemyType,
                enemy.MaxHealth,
                _battleRule.EnemySpawnPosition,
                enemy.AttackIntervalSeconds);
        }

        private bool HasPendingSpawns(StageBattle stage)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds > CurrentState.SpawnElapsedSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveArrivedSpellAttacks()
        {
            var arrivedAttackIds = new List<long>();
            foreach (var attack in CurrentState.Attacks)
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
                    ApplyBookShield(attack.Shield, attack.EffectDurationSeconds);
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

        private void ResolveDamageAttack(BattleAttackState attack)
        {
            foreach (var targetId in attack.TargetEnemyIds)
            {
                var target = FindLivingEnemy(targetId);
                if (target == null)
                {
                    continue;
                }

                ApplyEnemyDamage(targetId, attack.Damage);
                if (attack.SpellType == SpellType.FrostRing)
                {
                    SetEnemySlow(
                        targetId,
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
            foreach (var effect in CurrentState.Effects)
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
            // 固定本帧要处理的敌人集合，后续均通过 ID 回查移动后的最新状态。
            var enemyIds = new List<long>(CurrentState.Enemies.Count);
            foreach (var enemy in CurrentState.Enemies)
            {
                enemyIds.Add(enemy.RuntimeId);
            }

            foreach (var enemyId in enemyIds)
            {
                var state = FindLivingEnemy(enemyId);
                var config = _enemies.Get(state.Type);

                if (state.PathPosition > _battleRule.BookContactPosition)
                {
                    var nextPosition = Math.Max(
                        _battleRule.BookContactPosition,
                        state.PathPosition -
                        config.MoveSpeedPerSecond * state.SlowMultiplier * deltaTime);
                    SetEnemyPathPosition(enemyId, nextPosition);
                    state = FindLivingEnemy(enemyId);
                }

                if (state.PathPosition <= _battleRule.BookContactPosition &&
                    state.AttackRemainingSeconds <= 0f)
                {
                    ApplyBookDamage(config.AttackDamage);
                    ResetEnemyAttack(enemyId, config.AttackIntervalSeconds);
                }
            }
        }

        private void CastReadySpells()
        {
            foreach (var cooldown in CurrentState.Cooldowns)
            {
                if (cooldown.RemainingSeconds > 0f)
                {
                    continue;
                }

                var target = FindNearestEnemyToBook();
                SpellInstanceState spell;
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
            SpellInstanceState spell,
            EnemyBattleState primaryTarget)
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
            SetSpellCooldown(equipmentSlot, combat.CooldownSeconds);
        }

        private IReadOnlyList<long> SelectTargets(
            SpellCombat combat,
            EnemyBattleState primaryTarget)
        {
            switch (combat.SpellType)
            {
                case SpellType.Fireball:
                    return new[] { primaryTarget.RuntimeId };
                case SpellType.ChainLightning:
                    return SelectChainTargets(
                        primaryTarget,
                        combat.ChainTargetCount,
                        combat.EffectRange);
                case SpellType.FrostRing:
                    return SelectAreaTargets(primaryTarget.PathPosition, combat.EffectRange);
                case SpellType.Shield:
                    return Array.Empty<long>();
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(combat.SpellType),
                        combat.SpellType,
                        null);
            }
        }

        private IReadOnlyList<long> SelectChainTargets(
            EnemyBattleState primaryTarget,
            int targetCount,
            float chainRange)
        {
            var selected = new List<long>(targetCount) { primaryTarget.RuntimeId };
            var current = primaryTarget;

            while (selected.Count < targetCount)
            {
                var next = FindNearestUnselectedEnemy(
                    current.PathPosition,
                    chainRange,
                    selected);
                if (next == null)
                {
                    break;
                }

                selected.Add(next.RuntimeId);
                current = next;
            }

            return selected;
        }

        private IReadOnlyList<long> SelectAreaTargets(float center, float range)
        {
            var targets = new List<EnemyBattleState>();
            foreach (var enemy in CurrentState.Enemies)
            {
                if (enemy.Health > 0f && Math.Abs(enemy.PathPosition - center) <= range)
                {
                    targets.Add(enemy);
                }
            }

            targets.Sort(CompareEnemiesByPathThenId);
            var targetIds = new List<long>(targets.Count);
            foreach (var target in targets)
            {
                targetIds.Add(target.RuntimeId);
            }

            return targetIds;
        }

        private EnemyBattleState FindNearestEnemyToBook()
        {
            EnemyBattleState nearest = null;
            foreach (var enemy in CurrentState.Enemies)
            {
                if (enemy.Health <= 0f ||
                    nearest != null && CompareEnemiesByPathThenId(enemy, nearest) >= 0)
                {
                    continue;
                }

                nearest = enemy;
            }

            return nearest;
        }

        private EnemyBattleState FindNearestUnselectedEnemy(
            float origin,
            float range,
            IReadOnlyList<long> selected)
        {
            EnemyBattleState nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var enemy in CurrentState.Enemies)
            {
                if (enemy.Health <= 0f || Contains(selected, enemy.RuntimeId))
                {
                    continue;
                }

                var distance = Math.Abs(enemy.PathPosition - origin);
                if (distance > range)
                {
                    continue;
                }

                if (nearest == null ||
                    distance < nearestDistance ||
                    distance == nearestDistance && CompareEnemiesByPathThenId(enemy, nearest) < 0)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private EnemyBattleState FindLivingEnemy(long runtimeId)
        {
            return CurrentState.Enemies.Find(
                enemy => enemy.RuntimeId == runtimeId && enemy.Health > 0f);
        }

        private BattleAttackState FindAttack(long attackId)
        {
            return CurrentState.Attacks.Find(attack => attack.AttackId == attackId)
                   ?? throw new InvalidOperationException($"攻击不存在: {attackId}");
        }

        private static int CompareEnemiesByPathThenId(
            EnemyBattleState left,
            EnemyBattleState right)
        {
            // 路径相同时以运行时 ID 打破平局，保证目标选择可复现。
            var pathComparison = left.PathPosition.CompareTo(right.PathPosition);
            return pathComparison != 0
                ? pathComparison
                : left.RuntimeId.CompareTo(right.RuntimeId);
        }

        private static bool Contains(IReadOnlyList<long> values, long value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartChallengeState(
            long challengeId,
            int stageId,
            float bookMaxHealth,
            int equipmentSlotCount)
        {
            // 每次挑战整体替换瞬时状态，敌人、弹道、冷却和临时效果不会泄漏到下一局。
            _state = new AutoBattleState
            {
                ChallengeId = challengeId,
                StageId = stageId,
                IsRunning = true,
                BookHealth = bookMaxHealth,
                BookMaxHealth = bookMaxHealth,
            };

            for (var slot = 0; slot < equipmentSlotCount; slot++)
            {
                CurrentState.Cooldowns.Add(new SpellSlotCooldownState
                {
                    EquipmentSlot = slot,
                });
            }
        }

        private void StopChallenge()
        {
            CurrentState.IsRunning = false;
        }

        private void AdvanceSpawnProgress(float deltaTime)
        {
            CurrentState.SpawnElapsedSeconds += deltaTime;
        }

        private void SpawnEnemyState(
            EnemyType type,
            float maxHealth,
            float pathPosition,
            float attackIntervalSeconds)
        {
            var runtimeId = CurrentState.NextEnemyRuntimeId++;
            CurrentState.Enemies.Add(new EnemyBattleState
            {
                RuntimeId = runtimeId,
                Type = type,
                Health = maxHealth,
                MaxHealth = maxHealth,
                PathPosition = pathPosition,
                AttackRemainingSeconds = attackIntervalSeconds,
            });
        }

        private void SetEnemyPathPosition(long runtimeId, float pathPosition)
        {
            FindEnemy(runtimeId).PathPosition = pathPosition;
        }

        private void ResetEnemyAttack(long runtimeId, float attackIntervalSeconds)
        {
            FindEnemy(runtimeId).AttackRemainingSeconds = attackIntervalSeconds;
        }

        private void SetEnemySlow(
            long runtimeId,
            float remainingSeconds,
            float multiplier)
        {
            var enemy = FindEnemy(runtimeId);
            enemy.SlowRemainingSeconds = remainingSeconds;
            enemy.SlowMultiplier = multiplier;
        }

        private void ApplyEnemyDamage(long runtimeId, float damage)
        {
            var enemy = FindEnemy(runtimeId);
            enemy.Health = Math.Max(0f, enemy.Health - damage);
        }

        private void RemoveDefeatedEnemies()
        {
            CurrentState.Enemies.RemoveAll(enemy => enemy.Health <= 0f);
        }

        private void ApplyBookDamage(float damage)
        {
            // 护盾优先吸收伤害，只有溢出部分扣减魔法书生命。
            var shieldDamage = Math.Min(CurrentState.BookShield, damage);
            CurrentState.BookShield -= shieldDamage;
            CurrentState.BookHealth = Math.Max(
                0f,
                CurrentState.BookHealth - (damage - shieldDamage));
        }

        private void ApplyBookShield(float shield, float durationSeconds)
        {
            CurrentState.BookShield = Math.Max(CurrentState.BookShield, shield);
            CurrentState.BookShieldRemainingSeconds = durationSeconds;
        }

        private void SetSpellCooldown(int equipmentSlot, float remainingSeconds)
        {
            FindCooldown(equipmentSlot).RemainingSeconds = remainingSeconds;
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
            var attackId = CurrentState.NextAttackId++;
            CurrentState.Attacks.Add(new BattleAttackState
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
            CurrentState.Attacks.Remove(FindAttack(attackId));
        }

        private void AddEffect(
            SpellType spellType,
            long targetEnemyId,
            float pathPosition,
            float remainingSeconds)
        {
            var effectId = CurrentState.NextEffectId++;
            CurrentState.Effects.Add(new BattleEffectState
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
            CurrentState.Effects.Remove(FindEffect(effectId));
        }

        private void AdvanceTimers(float deltaTime)
        {
            foreach (var enemy in CurrentState.Enemies)
            {
                enemy.AttackRemainingSeconds -= deltaTime;
                if (enemy.SlowRemainingSeconds > 0f)
                {
                    enemy.SlowRemainingSeconds = Math.Max(
                        0f,
                        enemy.SlowRemainingSeconds - deltaTime);
                    if (enemy.SlowRemainingSeconds == 0f)
                    {
                        enemy.SlowMultiplier = 1f;
                    }
                }
            }

            foreach (var cooldown in CurrentState.Cooldowns)
            {
                cooldown.RemainingSeconds = Math.Max(
                    0f,
                    cooldown.RemainingSeconds - deltaTime);
            }

            foreach (var attack in CurrentState.Attacks)
            {
                attack.RemainingTravelSeconds = Math.Max(
                    0f,
                    attack.RemainingTravelSeconds - deltaTime);
            }

            foreach (var effect in CurrentState.Effects)
            {
                effect.RemainingSeconds = Math.Max(
                    0f,
                    effect.RemainingSeconds - deltaTime);
            }

            if (CurrentState.BookShieldRemainingSeconds > 0f)
            {
                CurrentState.BookShieldRemainingSeconds = Math.Max(
                    0f,
                    CurrentState.BookShieldRemainingSeconds - deltaTime);
                if (CurrentState.BookShieldRemainingSeconds == 0f)
                {
                    CurrentState.BookShield = 0f;
                }
            }
        }

        private BattleOutcome FinalizeOutcome(bool victory)
        {
            // Outcome 是一次性终态；重复结算说明战斗时序被破坏，应立即暴露。
            if (CurrentState.Outcome.HasValue)
            {
                throw new InvalidOperationException("当前挑战已经产生最终结果。");
            }

            var outcome = new BattleOutcome(
                CurrentState.ChallengeId,
                CurrentState.StageId,
                victory);
            CurrentState.Outcome = outcome;
            CurrentState.IsRunning = false;
            return outcome;
        }

        private EnemyBattleState FindEnemy(long runtimeId)
        {
            return CurrentState.Enemies.Find(enemy => enemy.RuntimeId == runtimeId)
                   ?? throw new KeyNotFoundException($"敌人不存在: {runtimeId}");
        }

        private SpellSlotCooldownState FindCooldown(int equipmentSlot)
        {
            return CurrentState.Cooldowns.Find(
                       cooldown => cooldown.EquipmentSlot == equipmentSlot)
                   ?? throw new KeyNotFoundException($"装备槽冷却不存在: {equipmentSlot}");
        }

        private BattleEffectState FindEffect(long effectId)
        {
            return CurrentState.Effects.Find(effect => effect.EffectId == effectId)
                   ?? throw new KeyNotFoundException($"效果不存在: {effectId}");
        }

        private void FinalizeChallenge(bool victory)
        {
            var outcome = FinalizeOutcome(victory);
            Publish(new BattleStateChangedEvent());
            Publish(new BattleChallengeEndedEvent(outcome));
            _currentProcedure.Complete(outcome);
        }
    }
}
