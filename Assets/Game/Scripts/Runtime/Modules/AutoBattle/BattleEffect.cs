using cfg;

namespace Game
{
    /// <summary>已经生效、仍需持续显示的临时战斗效果。</summary>
    internal sealed class BattleEffect
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
