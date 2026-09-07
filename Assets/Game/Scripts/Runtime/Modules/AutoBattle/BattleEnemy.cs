using System;
using cfg;

namespace Game
{
    internal sealed class BattleEnemy
    {
        internal BattleEnemy(
            long runtimeId,
            EnemyType type,
            float maxHealth,
            float pathPosition,
            float attackIntervalSeconds)
        {
            RuntimeId = runtimeId;
            Type = type;
            Health = maxHealth;
            MaxHealth = maxHealth;
            PathPosition = pathPosition;
            AttackRemainingSeconds = attackIntervalSeconds;
        }

        public long RuntimeId { get; }
        public EnemyType Type { get; }
        public float Health { get; private set; }
        public float MaxHealth { get; }

        // 一维路径坐标：数值越小越接近魔法书。
        public float PathPosition { get; private set; }
        public float AttackRemainingSeconds { get; private set; }
        public float SlowRemainingSeconds { get; private set; }
        public float SlowMultiplier { get; private set; } = 1f;

        internal bool CanAttack(float contactPosition)
        {
            return PathPosition <= contactPosition && AttackRemainingSeconds <= 0f;
        }

        internal void MoveTowards(float contactPosition, float speedPerSecond, float deltaTime)
        {
            if (PathPosition <= contactPosition)
            {
                return;
            }

            PathPosition = Math.Max(
                contactPosition,
                PathPosition - speedPerSecond * SlowMultiplier * deltaTime);
        }

        internal void ResetAttack(float attackIntervalSeconds)
        {
            AttackRemainingSeconds = attackIntervalSeconds;
        }

        internal void ApplyDamage(float damage)
        {
            Health = Math.Max(0f, Health - damage);
        }

        internal void ApplySlow(float remainingSeconds, float multiplier)
        {
            SlowRemainingSeconds = remainingSeconds;
            SlowMultiplier = multiplier;
        }

        internal void Tick(float deltaTime)
        {
            AttackRemainingSeconds -= deltaTime;
            if (SlowRemainingSeconds <= 0f)
            {
                return;
            }

            SlowRemainingSeconds = Math.Max(0f, SlowRemainingSeconds - deltaTime);
            if (SlowRemainingSeconds == 0f)
            {
                SlowMultiplier = 1f;
            }
        }
    }
}
