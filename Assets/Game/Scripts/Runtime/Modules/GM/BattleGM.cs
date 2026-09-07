#if JULYGF_DEBUG
using July.Arch;
using July.Diagnostics;
using July.UI;

namespace Game
{
    [GMCategory("战斗")]
    public static class BattleGM
    {
        [GMCommand("装备四种法术", Order = 1)]
        public static void EquipFourSpells()
        {
            var assets = ArchContext.Current?.GetSystem<SpellAssetSystem>();
            if (assets == null) return;

            ArchContext.Current.GetSystem<IUISystem>().ShowTip(assets.DebugEquipFourSpells()
                ? "已装备火球、雷链、冰环、护盾；原装备保留在合成区"
                : "合成区至少需要四个空位");
        }
    }
}
#endif
