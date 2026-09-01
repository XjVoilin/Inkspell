using System;

namespace Game
{
    /// <summary>玩家已解锁最高关卡的持久化状态。</summary>
    [Serializable]
    internal sealed class StageProgressionStoreData
    {
        // StoreBase<TData> 的 new() 约束和 LitJson 反序列化都需要公共无参构造函数。
        public StageProgressionStoreData()
        {
        }

        public bool Initialized;
        // 同时也是连续挑战下一次要进入的关卡；首版不支持回退或选关。
        public int CurrentHighestStageId;
    }
}
