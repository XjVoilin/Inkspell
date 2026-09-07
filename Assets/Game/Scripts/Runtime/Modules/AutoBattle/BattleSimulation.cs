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

        internal BattleSimulation(SpellAssetStore spellAssets, TbBattleRule battleRule, TbSpellAssetRule assetRule,
            TbSpellCombat spellCombat, TbSpellUpgrade spellUpgrades, TbEnemy enemies, TbStageBattle stages)
        {
            _spellAssets = spellAssets;
            _battleRule = battleRule;
            _assetRule = assetRule;
            _spellCombat = spellCombat;
            _spellUpgrades = spellUpgrades;
            _enemies = enemies;
            _stages = stages;
        }

        internal BattleRun CreateRun(long battleRunId, int stageId)
        {
            var stage = _stages.Get(stageId);
            var run = new BattleRun(
                battleRunId, stage.StageId, _battleRule.BookMaxHealth, _assetRule.EquipmentSlotCount);

            SpawnEnemiesAtChallengeStart(run, stage);
            return run;
        }

        internal BattleOutcome? Advance(BattleRun run, float deltaTime)
        {
            var stage = _stages.Get(run.StageId);
            var previousSpawnElapsed = run.SpawnElapsedSeconds;

            // 结算顺序属于战斗规则：新敌人/已到达攻击先结算，再判胜、移动攻击、判负、最后施法。
            AdvanceTimers(run, deltaTime);
            run.AdvanceSpawnTime(deltaTime);
            SpawnEnemies(run, stage, previousSpawnElapsed, run.SpawnElapsedSeconds);
            ResolveArrivedSpellAttacks(run);
            foreach (var enemy in run.Enemies.Items)
            {
                if (enemy.Health <= 0f)
                    run.RecordFact(new BattleFact(BattleFactKind.EnemyDied, enemy));
            }
            run.Enemies.RemoveDefeated();
            RemoveExpiredEffects(run);

            if (run.Enemies.Count == 0 && !HasPendingSpawns(run, stage))
            {
                return run.Complete(true);
            }

            AdvanceEnemies(run, deltaTime);
            if (run.Book.IsDestroyed && run.Enemies.Count > 0)
            {
                return run.Complete(false);
            }

            CastReadySpells(run);
            return null;
        }

        private void SpawnEnemiesAtChallengeStart(BattleRun run, StageBattle stage)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds != 0f)
                {
                    continue;
                }

                SpawnEnemy(run, spawn.EnemyType);
            }
        }

        private void SpawnEnemies(BattleRun run, StageBattle stage, float previousElapsed, float currentElapsed)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds <= previousElapsed ||
                    spawn.SpawnTimeSeconds > currentElapsed)
                {
                    continue;
                }

                SpawnEnemy(run, spawn.EnemyType);
            }
        }

        private void SpawnEnemy(BattleRun run, EnemyType enemyType)
        {
            var enemy = _enemies.Get(enemyType);
            var spawned = run.Enemies.Spawn(
                enemyType,
                enemy.MaxHealth,
                _battleRule.EnemySpawnPosition,
                enemy.AttackIntervalSeconds);
            run.RecordFact(new BattleFact(BattleFactKind.EnemySpawned, spawned));
        }

        private bool HasPendingSpawns(BattleRun run, StageBattle stage)
        {
            foreach (var spawn in stage.Spawns)
            {
                if (spawn.SpawnTimeSeconds > run.SpawnElapsedSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveArrivedSpellAttacks(BattleRun run)
        {
            var index = 0;
            while (index < run.Attacks.Count)
            {
                var attack = run.Attacks[index];
                if (attack.RemainingTravelSeconds > 0f)
                {
                    index++;
                    continue;
                }

                if (attack.SpellType == SpellType.Shield)
                {
                    var previousShield = run.Book.Shield;
                    run.Book.ApplyShield(
                        attack.Shield,
                        attack.EffectDurationSeconds);
                    if (run.Book.Shield > previousShield)
                        run.RecordFact(new BattleFact(BattleFactKind.ShieldApplied));
                    run.AddEffect(
                        spellType: attack.SpellType,
                        targetEnemyId: 0,
                        pathPosition: _battleRule.BookContactPosition,
                        range: 0f,
                        durationSeconds: attack.EffectDurationSeconds);
                }
                else
                {
                    ResolveDamageAttack(run, attack);
                }

                run.RecordFact(new BattleFact(BattleFactKind.SpellImpact,
                    attack.SpellType, attack.TargetPathPosition, attack.TargetEnemyIds.Count));
                run.RemoveAttackAt(index);
            }
        }

        private void ResolveDamageAttack(BattleRun run, BattleAttack attack)
        {
            foreach (var targetId in attack.TargetEnemyIds)
            {
                var target = run.Enemies.FindLiving(targetId);
                if (target == null)
                {
                    continue;
                }

                var previousHealth = target.Health;
                target.ApplyDamage(attack.Damage);
                if (target.Health < previousHealth)
                    run.RecordFact(new BattleFact(BattleFactKind.EnemyDamaged, target, previousHealth - target.Health));
                if (attack.SpellType == SpellType.FrostRing)
                {
                    target.ApplySlow(
                        attack.EffectDurationSeconds,
                        attack.SlowMultiplier);
                }
            }

            if (attack.EffectDurationSeconds > 0f)
            {
                run.AddEffect(
                    spellType: attack.SpellType,
                    targetEnemyId: attack.TargetEnemyIds.Count > 0 ? attack.TargetEnemyIds[0] : 0,
                    pathPosition: attack.TargetPathPosition,
                    range: attack.EffectRange,
                    durationSeconds: attack.EffectDurationSeconds);
            }
        }

        private void RemoveExpiredEffects(BattleRun run)
        {
            var index = 0;
            while (index < run.Effects.Count)
            {
                if (run.Effects[index].RemainingSeconds > 0f)
                {
                    index++;
                    continue;
                }

                run.RemoveEffectAt(index);
            }
        }

        private void AdvanceEnemies(BattleRun run, float deltaTime)
        {
            foreach (var enemy in run.Enemies.Items)
            {
                var config = _enemies.Get(enemy.Type);
                enemy.MoveTowards(
                    _battleRule.BookContactPosition,
                    config.MoveSpeedPerSecond,
                    deltaTime);

                if (enemy.CanAttack(_battleRule.BookContactPosition))
                {
                    var previousHealth = run.Book.Health;
                    var previousShield = run.Book.Shield;
                    run.Book.ApplyDamage(config.AttackDamage);
                    if (run.Book.Shield < previousShield)
                        run.RecordFact(new BattleFact(run.Book.Shield == 0f
                            ? BattleFactKind.ShieldBroken : BattleFactKind.ShieldAbsorbed));
                    if (run.Book.Health < previousHealth)
                        run.RecordFact(new BattleFact(BattleFactKind.BookDamaged));
                    enemy.ResetAttack(config.AttackIntervalSeconds);
                }
            }
        }

        private void CastReadySpells(BattleRun run)
        {
            foreach (var cooldown in run.Cooldowns.Items)
            {
                if (cooldown.RemainingSeconds > 0f)
                {
                    continue;
                }

                var target = run.Enemies.FindNearestToBook();
                SpellInstance spell;
                if (!_spellAssets.TryGetEquippedSpell(
                        cooldown.EquipmentSlot,
                        out spell) ||
                    target == null)
                {
                    continue;
                }

                CastSpell(run, cooldown.EquipmentSlot, spell, target);
            }
        }

        private void CastSpell(BattleRun run, int equipmentSlot, SpellInstance spell, BattleEnemy primaryTarget)
        {
            var combat = _spellCombat.Get(spell.Type, spell.Tier);
            var upgrade = _spellUpgrades.Get(spell.Type, spell.Tier, spell.Level);
            var targetIds = SelectTargets(run, combat, primaryTarget);
            var damage = combat.BaseDamage * upgrade.CurrentPowerMultiplier;
            var shield = combat.BaseShield * upgrade.CurrentPowerMultiplier;
            var targetPosition = spell.Type == SpellType.Shield
                ? _battleRule.BookContactPosition
                : primaryTarget.PathPosition;

            run.AddAttack(
                spellType: spell.Type,
                targetEnemyIds: targetIds,
                targetPathPosition: targetPosition,
                travelSeconds: _battleRule.AttackTravelSeconds,
                damage: damage,
                shield: shield,
                effectRange: combat.EffectRange,
                effectDurationSeconds: combat.EffectDurationSeconds,
                slowMultiplier: combat.SlowMultiplier);
            run.Cooldowns.Set(equipmentSlot, combat.CooldownSeconds);
            run.RecordFact(new BattleFact(BattleFactKind.SpellCast,
                spell.Type, targetPosition, targetIds.Count, _battleRule.AttackTravelSeconds));
        }

        private IReadOnlyList<long> SelectTargets(BattleRun run,
            SpellCombat combat,
            BattleEnemy primaryTarget)
        {
            switch (combat.SpellType)
            {
                case SpellType.Fireball:
                    return new[] { primaryTarget.RuntimeId };
                case SpellType.ChainLightning:
                    return run.Enemies.SelectChainTargets(
                        primaryTarget,
                        combat.ChainTargetCount,
                        combat.EffectRange);
                case SpellType.FrostRing:
                    return run.Enemies.SelectAreaTargets(
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

        private void AdvanceTimers(BattleRun run, float deltaTime)
        {
            var previousShield = run.Book.Shield;
            run.Book.Tick(deltaTime);
            if (previousShield > 0f && run.Book.Shield == 0f)
                run.RecordFact(new BattleFact(BattleFactKind.ShieldExpired));
            run.Enemies.Tick(deltaTime);
            run.Cooldowns.Tick(deltaTime);

            foreach (var attack in run.Attacks)
            {
                attack.RemainingTravelSeconds = Math.Max(
                    0f,
                    attack.RemainingTravelSeconds - deltaTime);
            }

            foreach (var effect in run.Effects)
            {
                effect.RemainingSeconds = Math.Max(
                    0f,
                    effect.RemainingSeconds - deltaTime);
            }
        }
    }
}
