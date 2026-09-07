#if JULYGF_DEBUG
using System;
using cfg;

namespace Game
{
    internal sealed partial class SpellAssetStore
    {
        internal long DebugAddSpell(SpellType type, int tier)
        {
            if (RemainingCraftingCapacity <= 0) return 0;
            var spell = AddNewSpell(type, tier, 1, SpellLocation.CraftingArea, -1);
            CommitChange(false);
            return spell.InstanceId;
        }

        internal void DebugAddInk(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Data.MagicInk = checked(Data.MagicInk + amount);
            CommitChange(false);
        }
    }
}
#endif
