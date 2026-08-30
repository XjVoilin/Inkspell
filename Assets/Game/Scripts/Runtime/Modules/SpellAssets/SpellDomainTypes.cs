using cfg;

namespace Game
{
    /// <summary>
    /// 法术实例所在的业务区域。
    /// </summary>
    public enum SpellLocation
    {
        CraftingArea = 0,
        EquipmentSlot = 1,
    }

    /// <summary>
    /// 对外只读的法术实例业务事实。
    /// </summary>
    public readonly struct SpellInfo
    {
        public SpellInfo(
            long instanceId,
            SpellType type,
            int tier,
            int level,
            SpellLocation location,
            int equipmentSlot,
            bool isLocked)
        {
            InstanceId = instanceId;
            Type = type;
            Tier = tier;
            Level = level;
            Location = location;
            EquipmentSlot = equipmentSlot;
            IsLocked = isLocked;
        }

        public long InstanceId { get; }
        public SpellType Type { get; }
        public int Tier { get; }
        public int Level { get; }
        public SpellLocation Location { get; }
        public int EquipmentSlot { get; }
        public bool IsLocked { get; }
    }
}
