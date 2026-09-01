using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    /// <summary>法术生成的持久化进度；待领取队列与玩家法术资产相互独立。</summary>
    [Serializable]
    internal sealed class SpellGenerationStoreData
    {
        // StoreBase<TData> 的 new() 约束和 LitJson 反序列化都需要公共无参构造函数。
        public SpellGenerationStoreData()
        {
        }

        public bool Initialized;

        // 保留生成顺序；只有资产系统确认接收队首后才能移除。
        public List<SpellType> PendingSpells = new();
        public float CycleProgressSeconds;

        // 进入后台时生效的间隔会保留到离线结算，避免离线期间关卡变化改写历史收益。
        public float ActiveIntervalSeconds;
        public long InactiveSinceUtcSeconds;
        public bool HasInactiveAnchor;
    }
}
