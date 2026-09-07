using July.Arch;

namespace Game
{
    /// <summary>独立维护长期关卡记录；通关判断和关卡选择属于 System。</summary>
    internal sealed class StageProgressionStore : StoreBase<StageProgressionStoreData>
    {
        internal int CurrentStageId => Data.CurrentHighestStageId;

        internal void Initialize(int initialStageId)
        {
            if (Data.Initialized)
                return;
            Data.Initialized = true;
            Data.CurrentHighestStageId = initialStageId;
            MarkDirty();
        }

        internal void SetCurrentStage(int stageId)
        {
            Data.CurrentHighestStageId = stageId;
            MarkDirty();
            Publish(new StageProgressChangedEvent(stageId));
        }
    }
}
