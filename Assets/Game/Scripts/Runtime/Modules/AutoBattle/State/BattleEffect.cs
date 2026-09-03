using cfg;

namespace Game
{
    internal interface IReadOnlyBattleEffect
    {
        long EffectId { get; }
        SpellType SpellType { get; }
        long TargetEnemyId { get; }
        float PathPosition { get; }
        float Range { get; }
        float TotalSeconds { get; }
        float RemainingSeconds { get; }
    }

    /// <summary>已经生效、仍需持续显示的临时战斗效果。</summary>
    internal sealed class BattleEffect : IReadOnlyBattleEffect
    {
        internal BattleEffect(
            long effectId,
            SpellType spellType,
            long targetEnemyId,
            float pathPosition,
            float range,
            float durationSeconds)
        {
            EffectId = effectId;
            SpellType = spellType;
            TargetEnemyId = targetEnemyId;
            PathPosition = pathPosition;
            Range = range;
            TotalSeconds = durationSeconds;
            RemainingSeconds = durationSeconds;
        }

        public long EffectId { get; }
        public SpellType SpellType { get; }
        public long TargetEnemyId { get; }
        public float PathPosition { get; }
        public float Range { get; }
        public float TotalSeconds { get; }
        public float RemainingSeconds { get; internal set; }
    }
}
