using System.Collections.Generic;
using cfg;

namespace Game
{
    /// <summary>施法瞬间冻结的跨帧攻击数据。</summary>
    internal sealed class BattleAttack
    {
        internal BattleAttack(
            long attackId,
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
            AttackId = attackId;
            SpellType = spellType;
            TargetEnemyIds = new List<long>(targetEnemyIds);
            TargetPathPosition = targetPathPosition;
            TotalTravelSeconds = travelSeconds;
            RemainingTravelSeconds = travelSeconds;
            Damage = damage;
            Shield = shield;
            EffectRange = effectRange;
            EffectDurationSeconds = effectDurationSeconds;
            SlowMultiplier = slowMultiplier;
        }

        public long AttackId { get; }
        public SpellType SpellType { get; }
        public IReadOnlyList<long> TargetEnemyIds { get; }
        public float TargetPathPosition { get; }
        public float TotalTravelSeconds { get; }
        public float RemainingTravelSeconds { get; internal set; }

        internal float Damage { get; }
        internal float Shield { get; }
        internal float EffectRange { get; }
        internal float EffectDurationSeconds { get; }
        internal float SlowMultiplier { get; }
    }
}
