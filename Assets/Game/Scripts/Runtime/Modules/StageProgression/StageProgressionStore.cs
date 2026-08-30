using cfg;
using July.Arch;

namespace Game
{
    public sealed class StageProgressionStore : StoreBase<StageProgressionStoreData>
    {
        private TbStageProgression _stages;

        internal int CurrentStageId => Data.CurrentHighestStageId;

        internal void Initialize(TbStageProgression stages)
        {
            _stages = stages;
            if (Data.Initialized)
            {
                return;
            }

            Data.Initialized = true;
            Data.CurrentHighestStageId = stages.DataList[0].StageId;
            MarkDirty();
        }

        internal void AdvanceOneStage()
        {
            var current = _stages.Get(Data.CurrentHighestStageId);
            var currentIndex = _stages.DataList.IndexOf(current);
            var next = _stages.DataList[currentIndex + 1];

            Data.CurrentHighestStageId = next.StageId;
            MarkDirty();
            Publish(new StageProgressChangedEvent(next.StageId));
        }
    }
}
