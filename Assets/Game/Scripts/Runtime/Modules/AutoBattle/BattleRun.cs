using System;
using System.Collections.Generic;
using cfg;
// ReSharper disable All

namespace Game
{
    /// <summary>封装单次自动战斗的全部规则、配置解释与运行态变更。</summary>
    internal sealed class BattleSimulation
    {
        private readonly SpellAssetStore _spellAssets;
        private readonly TbBattleRule _battleRule;
        private readonly TbSpellAssetRule _assetRule;
        private readonly TbSpellCombat _spellCombat;
        private readonly TbSpellUpgrade _spellUpgrades;
        private readonly TbEnemy _enemies;
        private readonly TbStageBattle _stages;

        internal BattleSimulation(
            SpellAssetStore spellAssets,
            TbBattleRule battleRule,
            TbSpellAssetRule assetRule,
            TbSpellCombat spellCombat,
            TbSpellUpgrade spellUpgrades,
            TbEnemy enemies,
            TbStageBattle stages)
        {
            _spellAssets = spellAssets;
            _battleRule = battleRule;
            _assetRule = assetRule;
            _spellCombat = spellCombat;
            _spellUpgrades = spellUpgrades;
            _enemies = enemies;
            _stages = stages;
        }

        internal BattleRun CurrentRun { get; private set; } = new();

        internal void Begin(long battleRunId, int stageId)
        {
            var stage = _stages.Get(stageId);
            CurrentRun = new BattleRun(
                battleRunId,
                stage.StageId,
                _battleRule.BookMaxHealth,
                _assetRule.EquipmentSlotCount);

            SpawnEnemiesAtChallengeStart(stage);
        }

        internal BattleOutcome? Advance(float deltaTime)
        {
            var stage = _stages.Get(CurrentRun.StageId);
            var previousSpawnElapsed = CurrentRun.SpawnElapsedSeconds;

            // 结算顺序属于战斗规则：新敌人/已到达攻击先结算，再判胜、移动攻击、判负、最后施法。
            AdvanceTimers(deltaTime);
            CurrentRun.SpawnElapsedSeconds += deltaTime;
            SpawnEnemies(stage, previousSpawnElapsed, CurrentRun.SpawnElapsedSeconds);
            ResolveArrivedSpellAttacks();
            CurrentRun.Enemies.RemoveDefeated();
            RemoveExpiredEffects();

            if (CurrentRun.Enemies.Count == 0 && !HasPendingSpawns(stage))
            {
                return FinalizeOutcome(true);
            }

            AdvanceEnemies(deltaTime);
            if (CurrentRun.Book.IsDestroyed && CurrentRun.Enemies.Count > 0)
            {
                return FinalizeOutcome(false);
            }

            CastReadySpells();
            return null;
        }

        internal void Stop()
        {
            CurrentRun.IsRunning = false;
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
    }

    /// <summary>单次战斗运行实例；随 AutoBattleSystem 生命周期存在且不持久化。</summary>
    internal sealed class BattleRun
    {
        internal BattleRun()
        {
        }

        internal BattleRun(
            long battleRunId,
            int stageId,
            float bookMaxHealth,
            int equipmentSlotCount)
        {
            BattleRunId = battleRunId;
            StageId = stageId;
            IsRunning = true;
            Book = new BattleBook(bookMaxHealth);
            Cooldowns.Initialize(equipmentSlotCount);
        }

        // BattleRunId 标识某一次战斗尝试；同一 StageId 重试时会产生新值。
        internal long BattleRunId { get; }

        // StageId 标识稳定的关卡配置；同一关的多次尝试共享此值。
        internal int StageId { get; }
        internal bool IsRunning { get; set; }
        internal float SpawnElapsedSeconds { get; set; }

        internal BattleBook Book { get; } = new();
        internal EnemyRoster Enemies { get; } = new();
        internal SpellCooldownSet Cooldowns { get; } = new();

        // 攻击和效果 ID 在当次战斗内单调递增，供 View 去重。
        internal long NextAttackId { get; set; } = 1;
        internal long NextEffectId { get; set; } = 1;
        internal List<BattleAttack> Attacks { get; } = new();
        internal List<BattleEffect> Effects { get; } = new();
        internal BattleOutcome? Outcome { get; set; }
    }

    internal sealed class BattleAttack
    {
        public long AttackId;
        public int EquipmentSlot;
        public SpellType SpellType;
        public List<long> TargetEnemyIds = new();
        public float TargetPathPosition;
        public float RemainingTravelSeconds;

        // 以下数值在施法瞬间固化，后续升级或换装不会追溯修改已发出的攻击。
        public float Damage;
        public float Shield;
        public float EffectRange;
        public float EffectDurationSeconds;
        public float SlowMultiplier = 1f;
    }

    internal sealed class BattleEffect
    {
        public long EffectId;
        public SpellType SpellType;
        public long TargetEnemyId;
        public float PathPosition;
        public float RemainingSeconds;
    }
}
