using System;
using System.Collections.Generic;
using cfg;

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
            CurrentRun.AdvanceSpawnTime(deltaTime);
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
            CurrentRun.Stop();
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
            var index = 0;
            while (index < CurrentRun.Attacks.Count)
            {
                var attack = CurrentRun.Attacks[index];
                if (attack.RemainingTravelSeconds > 0f)
                {
                    index++;
                    continue;
                }

                if (attack.SpellType == SpellType.Shield)
                {
                    CurrentRun.Book.ApplyShield(
                        attack.Shield,
                        attack.EffectDurationSeconds);
                    AddEffect(
                        attack.SpellType,
                        0,
                        _battleRule.BookContactPosition,
                        0f,
                        attack.EffectDurationSeconds);
                }
                else
                {
                    ResolveDamageAttack(attack);
                }

                CurrentRun.RemoveAttackAt(index);
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
                    attack.EffectRange,
                    attack.EffectDurationSeconds);
            }
        }

        private void RemoveExpiredEffects()
        {
            var index = 0;
            while (index < CurrentRun.Effects.Count)
            {
                if (CurrentRun.Effects[index].RemainingSeconds > 0f)
                {
                    index++;
                    continue;
                }

                CurrentRun.RemoveEffectAt(index);
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

        private void LaunchAttack(
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
            CurrentRun.AddAttack(new BattleAttack(
                CurrentRun.AllocateAttackId(),
                spellType,
                targetEnemyIds,
                targetPathPosition,
                travelSeconds,
                damage,
                shield,
                effectRange,
                effectDurationSeconds,
                slowMultiplier));
        }

        private void AddEffect(
            SpellType spellType,
            long targetEnemyId,
            float pathPosition,
            float range,
            float remainingSeconds)
        {
            CurrentRun.AddEffect(new BattleEffect(
                CurrentRun.AllocateEffectId(),
                spellType,
                targetEnemyId,
                pathPosition,
                range,
                remainingSeconds));
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
            return CurrentRun.Complete(victory);
        }
    }
}
