using System;

namespace Game
{
    internal sealed class SpellSlotCooldown
    {
        internal SpellSlotCooldown(int equipmentSlot)
        {
            EquipmentSlot = equipmentSlot;
        }

        public int EquipmentSlot { get; }
        public float TotalSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }

        internal void Set(float remainingSeconds)
        {
            TotalSeconds = Math.Max(0f, remainingSeconds);
            RemainingSeconds = TotalSeconds;
        }

        internal void Tick(float deltaTime)
        {
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaTime);
        }
    }
}
