using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game
{
    /// <summary>单次战斗中按装备槽唯一维护的法术冷却集合。</summary>
    internal sealed class SpellCooldownSet
    {
        private readonly List<SpellSlotCooldown> _items = new();
        private readonly ReadOnlyCollection<SpellSlotCooldown> _itemsView;

        internal SpellCooldownSet()
        {
            _itemsView = _items.AsReadOnly();
        }

        internal IReadOnlyList<SpellSlotCooldown> Items => _itemsView;


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
}
