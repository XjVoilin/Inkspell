#if JULYGF_DEBUG
using cfg;
using July.Config;

namespace Game
{
    public sealed partial class SpellAssetSystem
    {
        internal int DebugFreeSlots => _store.RemainingCraftingCapacity;

        internal int DebugAddMaterials(SpellType type, int tier, int count)
        {
            if (count < 1 || count > 24) throw new System.ArgumentOutOfRangeException(nameof(count));
            var config = GetSystem<IConfigSystem>();
            if (!_spellDefinitions.Get(type).IsOpen || config.GetTable<TbSpellTier>().Get(tier) == null)
                throw new System.ArgumentException("请选择已开放的法术和阶级。");
            var added = 0;
            while (added < count && _store.DebugAddSpell(type, tier) != 0) added++;
            return added;
        }

        internal void DebugAddInk(int amount) => _store.DebugAddInk(amount);

        internal bool DebugEquipFourSpells()
        {
            // Reserve all four additions before changing anything: occupied slots return
            // their previous spells to crafting, so replacement also consumes capacity.
            if (_store.RemainingCraftingCapacity < 4) return false;
            var types = new[] {SpellType.Fireball, SpellType.ChainLightning, SpellType.FrostRing, SpellType.Shield};
            for (var slot = 0; slot < types.Length; slot++)
            {
                var id = _store.DebugAddSpell(types[slot], 1);
                if (id == 0 || !_store.TryEquip(id, slot)) return false;
            }
            return true;
        }
    }
}
#endif
