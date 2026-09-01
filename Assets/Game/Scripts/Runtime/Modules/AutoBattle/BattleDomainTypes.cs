using System;

namespace Game
{
    [Serializable]
    public readonly struct BattleOutcome
    {
        public BattleOutcome(long challengeId, int stageId, bool victory)
        {
            ChallengeId = challengeId;
            StageId = stageId;
            Victory = victory;
        }

        public long ChallengeId { get; }
        public int StageId { get; }
        public bool Victory { get; }
    }

}
