namespace Game
{
    public readonly struct BattleStateChangedEvent
    {
    }

    public readonly struct BattleChallengeEndedEvent
    {
        public BattleChallengeEndedEvent(BattleOutcome outcome)
        {
            Outcome = outcome;
        }

        public BattleOutcome Outcome { get; }
    }
}
