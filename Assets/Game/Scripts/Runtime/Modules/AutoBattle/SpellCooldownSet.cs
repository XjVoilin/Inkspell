using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>单次战斗中按装备槽唯一维护的法术冷却集合。</summary>
    internal sealed class SpellCooldownSet
    {
        private readonly List<SpellSlotCooldown> _items = new();

        internal IReadOnlyList<SpellSlotCooldown> Items => _items;

        internal void Initialize(int equipmentSlotCount)
        {
            _items.Clear();
            for (var slot = 0; slot < equipmentSlotCount; slot++)
            {
                _items.Add(new SpellSlotCooldown(slot));
            }
        }

        internal void Set(int equipmentSlot, float remainingSeconds)
        {
            var cooldown = _items.Find(item => item.EquipmentSlot == equipmentSlot)
                           ?? throw new KeyNotFoundException(
                               $"装备槽冷却不存在: {equipmentSlot}");
            cooldown.Set(remainingSeconds);
        }

        internal void Tick(float deltaTime)
        {
            foreach (var cooldown in _items)
            {
                cooldown.Tick(deltaTime);
            }
        }
    }

    internal sealed class SpellSlotCooldown
    {
        internal SpellSlotCooldown(int equipmentSlot)
        {
            EquipmentSlot = equipmentSlot;
        }

        internal int EquipmentSlot { get; }
        internal float RemainingSeconds { get; private set; }

        internal void Set(float remainingSeconds)
        {
            RemainingSeconds = remainingSeconds;
        }

        internal void Tick(float deltaTime)
        {
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaTime);
        }
    }
}
