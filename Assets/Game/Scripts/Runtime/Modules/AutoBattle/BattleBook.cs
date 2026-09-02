using System;

namespace Game
{
    /// <summary>单次战斗中魔法书的生命与护盾边界。</summary>
    internal sealed class BattleBook
    {
        internal BattleBook()
        {
        }

        internal BattleBook(float maxHealth)
        {
            MaxHealth = maxHealth;
            Health = maxHealth;
        }

        internal float Health { get; private set; }
        internal float MaxHealth { get; private set; }
        internal float Shield { get; private set; }
        internal float ShieldRemainingSeconds { get; private set; }
        internal bool IsDestroyed => Health <= 0f;

        internal void ApplyDamage(float damage)
        {
            // 护盾优先吸收伤害，只有溢出部分扣减魔法书生命。
            var shieldDamage = Math.Min(Shield, damage);
            Shield -= shieldDamage;
            Health = Math.Max(0f, Health - (damage - shieldDamage));
        }

        internal void ApplyShield(float shield, float durationSeconds)
        {
            Shield = Math.Max(Shield, shield);
            ShieldRemainingSeconds = durationSeconds;
        }

        internal void Tick(float deltaTime)
        {
            if (ShieldRemainingSeconds <= 0f)
            {
                return;
            }

            ShieldRemainingSeconds = Math.Max(
                0f,
                ShieldRemainingSeconds - deltaTime);
            if (ShieldRemainingSeconds == 0f)
            {
                Shield = 0f;
            }
        }
    }
}
