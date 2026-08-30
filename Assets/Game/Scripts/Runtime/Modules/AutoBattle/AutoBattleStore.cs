using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;
using July.Arch;

namespace Game
{
    public sealed class AutoBattleStore : StoreBase<AutoBattleStoreData>
    {
        internal AutoBattleStoreData Current => Data;

        internal void StartChallenge(
            long challengeId,
            int stageId,
            float bookMaxHealth,
            int equipmentSlotCount)
        {
            var next = new AutoBattleStoreData
            {
                ChallengeId = challengeId,
                StageId = stageId,
                IsRunning = true,
                BookHealth = bookMaxHealth,
                BookMaxHealth = bookMaxHealth,
            };

            for (var slot = 0; slot < equipmentSlotCount; slot++)
            {
                next.Cooldowns.Add(new SpellSlotCooldownState
                {
                    EquipmentSlot = slot,
                });
            }

            ReplaceData(next);
        }

        internal void StopChallenge()
        {
            Data.IsRunning = false;
            MarkDirty();
        }

        internal void AdvanceSpawnProgress(float deltaTime)
        {
            Data.SpawnElapsedSeconds += deltaTime;
            MarkDirty();
        }

        internal long SpawnEnemy(
            EnemyType type,
            float maxHealth,
            float pathPosition,
            float attackIntervalSeconds)
        {
            var runtimeId = Data.NextEnemyRuntimeId++;
            Data.Enemies.Add(new EnemyBattleState
            {
                RuntimeId = runtimeId,
                Type = type,
                Health = maxHealth,
                MaxHealth = maxHealth,
                PathPosition = pathPosition,
                AttackRemainingSeconds = attackIntervalSeconds,
            });
            MarkDirty();
            return runtimeId;
        }

        internal void SetEnemyPathPosition(long runtimeId, float pathPosition)
        {
            FindEnemy(runtimeId).PathPosition = pathPosition;
            MarkDirty();
        }

        internal void ResetEnemyAttack(long runtimeId, float attackIntervalSeconds)
        {
            FindEnemy(runtimeId).AttackRemainingSeconds = attackIntervalSeconds;
            MarkDirty();
        }

        internal void SetEnemySlow(
            long runtimeId,
            float remainingSeconds,
            float multiplier)
        {
            var enemy = FindEnemy(runtimeId);
            enemy.SlowRemainingSeconds = remainingSeconds;
            enemy.SlowMultiplier = multiplier;
            MarkDirty();
        }

        internal void ApplyEnemyDamage(long runtimeId, float damage)
        {
            var enemy = FindEnemy(runtimeId);
            enemy.Health = Math.Max(0f, enemy.Health - damage);
            MarkDirty();
        }

        internal int RemoveDefeatedEnemies()
        {
            var removed = Data.Enemies.RemoveAll(enemy => enemy.Health <= 0f);
            if (removed > 0)
            {
                MarkDirty();
            }

            return removed;
        }

        internal void ApplyBookDamage(float damage)
        {
            var shieldDamage = Math.Min(Data.BookShield, damage);
            Data.BookShield -= shieldDamage;
            Data.BookHealth = Math.Max(0f, Data.BookHealth - (damage - shieldDamage));
            MarkDirty();
        }

        internal void ApplyBookShield(float shield, float durationSeconds)
        {
            Data.BookShield = Math.Max(Data.BookShield, shield);
            Data.BookShieldRemainingSeconds = durationSeconds;
            MarkDirty();
        }

        internal void SetSpellCooldown(int equipmentSlot, float remainingSeconds)
        {
            FindCooldown(equipmentSlot).RemainingSeconds = remainingSeconds;
            MarkDirty();
        }

        internal long LaunchAttack(
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
            var attackId = Data.NextAttackId++;
            Data.Attacks.Add(new BattleAttackState
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
            MarkDirty();
            return attackId;
        }

        internal void RemoveAttack(long attackId)
        {
            Data.Attacks.Remove(FindAttack(attackId));
            MarkDirty();
        }

        internal long AddEffect(
            SpellType spellType,
            long targetEnemyId,
            float pathPosition,
            float remainingSeconds)
        {
            var effectId = Data.NextEffectId++;
            Data.Effects.Add(new BattleEffectState
            {
                EffectId = effectId,
                SpellType = spellType,
                TargetEnemyId = targetEnemyId,
                PathPosition = pathPosition,
                RemainingSeconds = remainingSeconds,
            });
            MarkDirty();
            return effectId;
        }

        internal void RemoveEffect(long effectId)
        {
            Data.Effects.Remove(FindEffect(effectId));
            MarkDirty();
        }

        internal void AdvanceTimers(float deltaTime)
        {
            foreach (var enemy in Data.Enemies)
            {
                enemy.AttackRemainingSeconds -= deltaTime;
                if (enemy.SlowRemainingSeconds > 0f)
                {
                    enemy.SlowRemainingSeconds = Math.Max(0f, enemy.SlowRemainingSeconds - deltaTime);
                    if (enemy.SlowRemainingSeconds == 0f)
                    {
                        enemy.SlowMultiplier = 1f;
                    }
                }
            }

            foreach (var cooldown in Data.Cooldowns)
            {
                cooldown.RemainingSeconds = Math.Max(0f, cooldown.RemainingSeconds - deltaTime);
            }

            foreach (var attack in Data.Attacks)
            {
                attack.RemainingTravelSeconds = Math.Max(0f, attack.RemainingTravelSeconds - deltaTime);
            }

            foreach (var effect in Data.Effects)
            {
                effect.RemainingSeconds = Math.Max(0f, effect.RemainingSeconds - deltaTime);
            }

            if (Data.BookShieldRemainingSeconds > 0f)
            {
                Data.BookShieldRemainingSeconds = Math.Max(
                    0f,
                    Data.BookShieldRemainingSeconds - deltaTime);
                if (Data.BookShieldRemainingSeconds == 0f)
                {
                    Data.BookShield = 0f;
                }
            }

            MarkDirty();
        }

        internal BattleOutcome FinalizeOutcome(bool victory)
        {
            if (Data.Outcome.HasValue)
            {
                throw new InvalidOperationException("当前挑战已经产生最终结果。");
            }

            var outcome = new BattleOutcome(Data.ChallengeId, Data.StageId, victory);
            Data.Outcome = outcome;
            Data.IsRunning = false;
            MarkDirty();
            return outcome;
        }

        internal BattleViewState CreateViewState()
        {
            var enemies = new List<EnemyBattleView>(Data.Enemies.Count);
            foreach (var enemy in Data.Enemies)
            {
                enemies.Add(new EnemyBattleView(enemy));
            }

            var cooldowns = new List<SpellCooldownView>(Data.Cooldowns.Count);
            foreach (var cooldown in Data.Cooldowns)
            {
                cooldowns.Add(new SpellCooldownView(cooldown));
            }

            var attacks = new List<BattleAttackView>(Data.Attacks.Count);
            foreach (var attack in Data.Attacks)
            {
                attacks.Add(new BattleAttackView(attack));
            }

            var effects = new List<BattleEffectView>(Data.Effects.Count);
            foreach (var effect in Data.Effects)
            {
                effects.Add(new BattleEffectView(effect));
            }

            return new BattleViewState(
                Data.ChallengeId,
                Data.IsRunning,
                Data.StageId,
                Data.BookHealth,
                Data.BookMaxHealth,
                Data.BookShield,
                new ReadOnlyCollection<EnemyBattleView>(enemies),
                new ReadOnlyCollection<SpellCooldownView>(cooldowns),
                new ReadOnlyCollection<BattleAttackView>(attacks),
                new ReadOnlyCollection<BattleEffectView>(effects),
                Data.Outcome);
        }

        private EnemyBattleState FindEnemy(long runtimeId)
            => Data.Enemies.Find(enemy => enemy.RuntimeId == runtimeId)
               ?? throw new KeyNotFoundException($"敌人不存在: {runtimeId}");

        private SpellSlotCooldownState FindCooldown(int equipmentSlot)
            => Data.Cooldowns.Find(cooldown => cooldown.EquipmentSlot == equipmentSlot)
               ?? throw new KeyNotFoundException($"装备槽冷却不存在: {equipmentSlot}");

        private BattleAttackState FindAttack(long attackId)
            => Data.Attacks.Find(attack => attack.AttackId == attackId)
               ?? throw new KeyNotFoundException($"攻击不存在: {attackId}");

        private BattleEffectState FindEffect(long effectId)
            => Data.Effects.Find(effect => effect.EffectId == effectId)
               ?? throw new KeyNotFoundException($"效果不存在: {effectId}");
    }
}
