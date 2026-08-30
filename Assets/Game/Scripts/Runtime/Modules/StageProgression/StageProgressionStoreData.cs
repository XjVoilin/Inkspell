using System;

namespace Game
{
    [Serializable]
    public sealed class StageProgressionStoreData
    {
        public bool Initialized;
        public int CurrentHighestStageId;
    }
}
