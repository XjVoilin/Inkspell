using System.Collections.Generic;
using cfg;

namespace Game
{
    internal enum BattleFactKind
    {
        SpellCast, SpellImpact, EnemySpawned, EnemyDamaged, EnemyDied,
        BookDamaged, ShieldApplied, ShieldAbsorbed, ShieldBroken, ShieldExpired,
    }

    /// <summary>已提交的瞬时事实；值在记录时冻结，不引用可变的战斗对象。</summary>
    internal readonly struct BattleFact
    {
        internal BattleFact(BattleFactKind kind)
        {
            this = default;
            Kind = kind;
        }

        internal BattleFact(BattleFactKind kind, SpellType spellType, float pathPosition, int targetCount, float travelSeconds = 0f)
            : this(kind)
        {
            SpellType = spellType;
            PathPosition = pathPosition;
            TargetCount = targetCount;
            TravelSeconds = travelSeconds;
        }

        internal BattleFact(BattleFactKind kind, BattleEnemy enemy, float damage = 0f) : this(kind)
        {
            EnemyId = enemy.RuntimeId;
            EnemyType = enemy.Type;
            PathPosition = enemy.PathPosition;
            Health = enemy.Health;
            MaxHealth = enemy.MaxHealth;
            Damage = damage;
        }

        internal BattleFactKind Kind { get; }
        internal SpellType SpellType { get; }
        internal float PathPosition { get; }
        internal int TargetCount { get; }
        internal long EnemyId { get; }
        internal EnemyType EnemyType { get; }
        internal float Health { get; }
        internal float MaxHealth { get; }
        internal float TravelSeconds { get; }
        internal float Damage { get; }
    }

    internal readonly struct BattleFactsEvent
    {
        internal BattleFactsEvent(long battleRunId, IReadOnlyList<BattleFact> facts)
        {
            BattleRunId = battleRunId;
            Facts = facts;
        }

        internal long BattleRunId { get; }
        internal IReadOnlyList<BattleFact> Facts { get; }
    }

    /// <summary>重新读取连续状态；推进时每帧一次，不用于推断瞬时事实。</summary>
    public readonly struct BattleStateChangedEvent
    {
    }

    public readonly struct BattleChallengeEndedEvent
    {
        public BattleChallengeEndedEvent(BattleOutcome outcome)
        {
            Outcome = outcome;
        }

        public BattleOutcome Outcome { get; }
    }
}
