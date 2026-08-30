namespace Game
{
    public readonly struct StageProgressChangedEvent
    {
        internal StageProgressChangedEvent(int currentStageId)
        {
            CurrentStageId = currentStageId;
        }

        public int CurrentStageId { get; }
    }
}
