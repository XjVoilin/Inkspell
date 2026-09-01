using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    /// <summary>法术实例与魔法墨水的持久化权威状态。</summary>
    [Serializable]
    internal sealed class SpellAssetStoreData
    {
        // StoreBase<TData> 的 new() 约束和 LitJson 反序列化都需要公共无参构造函数。
        public SpellAssetStoreData()
        {
        }

        public bool Initialized;

        // 单调递增且不复用，保证删除、合成后旧实例 ID 不会重新指向新法术。
        public long NextInstanceId;
        public int MagicInk;
        public List<SpellInstanceState> Spells = new();
    }

    [Serializable]
    internal sealed class SpellInstanceState
    {
        public long InstanceId;
        public SpellType Type;
        public int Tier;
        public int Level;
        public SpellLocation Location;

        // 仅 Location 为 EquipmentSlot 时有效；合成区实例固定为 -1。
        public int EquipmentSlot;
        public bool IsLocked;
    }
}
