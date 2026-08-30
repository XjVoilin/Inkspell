using cfg;

namespace Game
{
    /// <summary>
    /// 单张法术卡的显示输入。
    /// </summary>
    public sealed class SpellCardViewData
    {
        public long InstanceId { get; set; }
        public SpellType SpellType { get; set; }
        public int Tier { get; set; }
        public int Level { get; set; }
        public bool IsLocked { get; set; }
        public string IconResourceKey { get; set; }
        public string TierDisplayKey { get; set; }
        public bool CanDrag { get; set; }
    }

    /// <summary>
    /// 合成区固定可见格的显示输入。
    /// 空格使用 null 表示，不保存业务位置。
    /// </summary>
    public sealed class SpellBoardViewData
    {
        public SpellCardViewData[] Slots { get; set; }
    }

    /// <summary>
    /// 装备槽显示输入，数组索引即配置槽位编号。
    /// </summary>
    public sealed class EquipmentBarViewData
    {
        public SpellCardViewData[] Slots { get; set; }
    }
}
