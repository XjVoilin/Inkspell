#if JULYGF_DEBUG
using cfg;
using July.Arch;
using July.Diagnostics;
using July.UI;

namespace Game
{
    [GMCategory("法术")]
    public static class SpellGM
    {
        [GMCommand("添加一对法术", Order = 1)]
        public static void AddPair(
            [GMParam("法术类型")] SpellType spellType = SpellType.Fireball,
            [GMParam("阶级（1—3）")] int tier = 1)
        {
            var assets = ArchContext.Current?.GetSystem<SpellAssetSystem>();
            if (assets == null) return;

            var ui = ArchContext.Current.GetSystem<IUISystem>();
            if (assets.DebugFreeSlots < 2)
            {
                ui.ShowTip("合成区至少需要两个空位");
                return;
            }

            var count = assets.DebugAddMaterials(spellType, tier, 2);
            ui.ShowTip($"已添加 {count} 张同阶法术，拖动一张到另一张上进行合成");
        }

        [GMCommand("下一次合成成功", Order = 2)]
        public static void NextSuccess()
        {
            var synthesis = ArchContext.Current?.GetSystem<SpellSynthesisSystem>();
            if (synthesis == null) return;

            synthesis.DebugNextSuccess = true;
            ArchContext.Current.GetSystem<IUISystem>().ShowTip("下一次合法合成必定成功");
        }

        [GMCommand("下一次合成转为墨水", Order = 3)]
        public static void NextFailure()
        {
            var synthesis = ArchContext.Current?.GetSystem<SpellSynthesisSystem>();
            if (synthesis == null) return;

            synthesis.DebugNextSuccess = false;
            ArchContext.Current.GetSystem<IUISystem>().ShowTip("下一次合法合成转为墨水");
        }

        [GMCommand("恢复正常合成概率", Order = 4)]
        public static void ResetOutcome()
        {
            var synthesis = ArchContext.Current?.GetSystem<SpellSynthesisSystem>();
            if (synthesis == null) return;

            synthesis.DebugNextSuccess = null;
            ArchContext.Current.GetSystem<IUISystem>().ShowTip("已恢复正常合成概率");
        }

        [GMCommand("添加魔法墨水", Order = 5)]
        public static void AddInk([GMParam("数量")] int amount = 1000)
        {
            var assets = ArchContext.Current?.GetSystem<SpellAssetSystem>();
            if (assets == null) return;

            assets.DebugAddInk(amount);
            ArchContext.Current.GetSystem<IUISystem>().ShowTip($"已增加 {amount} 墨水");
        }
    }
}
#endif
