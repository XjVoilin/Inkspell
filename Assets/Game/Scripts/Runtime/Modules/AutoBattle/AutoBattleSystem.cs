using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    public sealed class AutoBattleSystem : SystemBase, IUpdatableSystem
    {
        private AutoBattleStore _store;
        private SpellAssetSystem _spellAssets;
        private TbBattleRule _battleRule;
        private TbSpellAssetRule _assetRule;
        private TbSpellCombat _spellCombat;
        private TbSpellUpgrade _spellUpgrades;
        private TbEnemy _enemies;
        private TbStageBattle _stages;
        private BattleChallengeProcedure _currentProcedure;

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

        public BattleViewState GetBattleViewState()
        {
            return _store.CreateViewState();
        }

        public void OnUpdate(float deltaTime)
        {
            if (_currentProcedure == null || !_store.Current.IsRunning)
            {
                return;
            }

            try
            {
                AdvanceBattle(deltaTime);
            }
            catch (Exception exception)
            {
                if (_store.Current.IsRunning)
                {
                    _store.StopChallenge();
                    Publish(new BattleStateChangedEvent());
                }

                _currentProcedure?.Fail(exception);
                throw;
            }
        }

        protected override UniTask OnInitializeAsync()
        {
            _store = GetStore<AutoBattleStore>();
            _spellAssets = GetSystem<SpellAssetSystem>();

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
            var challengeId = _store.Current.ChallengeId + 1;
            _store.StartChallenge(
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

            if (_store.Current.IsRunning)
            {
                _store.StopChallenge();
                Publish(new BattleStateChangedEvent());
            }

            _currentProcedure = null;
        }

        private void AdvanceBattle(float deltaTime)
        {
            var stage = _stages.Get(_store.Current.StageId);
            var previousSpawnElapsed = _store.Current.SpawnElapsedSeconds;

            _store.AdvanceTimers(deltaTime);
            _store.AdvanceSpawnProgress(deltaTime);
            SpawnEnemies(stage, previousSpawnElapsed, _store.Current.SpawnElapsedSeconds);
            ResolveArrivedSpellAttacks();
            _store.RemoveDefeatedEnemies();
            RemoveExpiredEffects();

            if (_store.Current.Enemies.Count == 0 && !HasPendingSpawns(stage))
            {
                FinalizeChallenge(true);
                return;
            }

            AdvanceEnemies(deltaTime);
            if (_store.Current.BookHealth <= 0f && _store.Current.Enemies.Count > 0)
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
            _store.SpawnEnemy(
                enemyType,
                enemy.MaxHealth,
                _battleRule.EnemySpawnPosition,
                enemy.AttackIntervalSeconds);
        }

        private bool HasPendingSpawns(StageBattle stage)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds > _store.Current.SpawnElapsedSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveArrivedSpellAttacks()
        {
            var arrivedAttackIds = new List<long>();
            foreach (var attack in _store.Current.Attacks)
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
                    _store.ApplyBookShield(attack.Shield, attack.EffectDurationSeconds);
                    _store.AddEffect(
                        attack.SpellType,
                        0,
                        _battleRule.BookContactPosition,
                        attack.EffectDurationSeconds);
                }
                else
                {
                    ResolveDamageAttack(attack);
                }

                _store.RemoveAttack(attackId);
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

                _store.ApplyEnemyDamage(targetId, attack.Damage);
                if (attack.SpellType == SpellType.FrostRing)
                {
                    _store.SetEnemySlow(
                        targetId,
                        attack.EffectDurationSeconds,
                        attack.SlowMultiplier);
                }
            }

            if (attack.EffectDurationSeconds > 0f)
            {
                _store.AddEffect(
                    attack.SpellType,
                    attack.TargetEnemyIds.Count > 0 ? attack.TargetEnemyIds[0] : 0,
                    attack.TargetPathPosition,
                    attack.EffectDurationSeconds);
            }
        }

        private void RemoveExpiredEffects()
        {
            var expiredEffectIds = new List<long>();
            foreach (var effect in _store.Current.Effects)
            {
                if (effect.RemainingSeconds <= 0f)
                {
                    expiredEffectIds.Add(effect.EffectId);
                }
            }

            foreach (var effectId in expiredEffectIds)
            {
                _store.RemoveEffect(effectId);
            }
        }

        private void AdvanceEnemies(float deltaTime)
        {
            var enemyIds = new List<long>(_store.Current.Enemies.Count);
            foreach (var enemy in _store.Current.Enemies)
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
                    _store.SetEnemyPathPosition(enemyId, nextPosition);
                    state = FindLivingEnemy(enemyId);
                }

                if (state.PathPosition <= _battleRule.BookContactPosition &&
                    state.AttackRemainingSeconds <= 0f)
                {
                    _store.ApplyBookDamage(config.AttackDamage);
                    _store.ResetEnemyAttack(enemyId, config.AttackIntervalSeconds);
                }
            }
        }

        private void CastReadySpells()
        {
            foreach (var cooldown in _store.Current.Cooldowns)
            {
                if (cooldown.RemainingSeconds > 0f)
                {
                    continue;
                }

                var spell = _spellAssets.GetEquippedSpell(cooldown.EquipmentSlot);
                var target = FindNearestEnemyToBook();
                if (!spell.HasValue || target == null)
                {
                    continue;
                }

                CastSpell(cooldown.EquipmentSlot, spell.Value, target);
            }
        }

        private void CastSpell(int equipmentSlot, SpellInfo spell, EnemyBattleState primaryTarget)
        {
            var combat = _spellCombat.Get(spell.Type, spell.Tier);
            var upgrade = _spellUpgrades.Get(spell.Type, spell.Tier, spell.Level);
            var targetIds = SelectTargets(combat, primaryTarget);
            var damage = combat.BaseDamage * upgrade.CurrentPowerMultiplier;
            var shield = combat.BaseShield * upgrade.CurrentPowerMultiplier;
            var targetPosition = spell.Type == SpellType.Shield
                ? _battleRule.BookContactPosition
                : primaryTarget.PathPosition;

            _store.LaunchAttack(
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
            _store.SetSpellCooldown(equipmentSlot, combat.CooldownSeconds);
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
            foreach (var enemy in _store.Current.Enemies)
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
            foreach (var enemy in _store.Current.Enemies)
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

            foreach (var enemy in _store.Current.Enemies)
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
            return _store.Current.Enemies.Find(
                enemy => enemy.RuntimeId == runtimeId && enemy.Health > 0f);
        }

        private BattleAttackState FindAttack(long attackId)
        {
            return _store.Current.Attacks.Find(attack => attack.AttackId == attackId)
                   ?? throw new InvalidOperationException($"攻击不存在: {attackId}");
        }

        private static int CompareEnemiesByPathThenId(
            EnemyBattleState left,
            EnemyBattleState right)
        {
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

        private void FinalizeChallenge(bool victory)
        {
            var outcome = _store.FinalizeOutcome(victory);
            Publish(new BattleStateChangedEvent());
            Publish(new BattleChallengeEndedEvent(outcome));
            _currentProcedure.Complete(outcome);
        }
    }
}
