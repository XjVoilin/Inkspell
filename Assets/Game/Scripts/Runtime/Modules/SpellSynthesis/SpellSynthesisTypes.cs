using cfg;

namespace Game
{
    public enum SynthesisRejectReason
    {
        SpellNotFound = 0,
        SameInstance = 1,
        DifferentTier = 2,
        Locked = 3,
        Equipped = 4,
        HighestTier = 5,
        Cultivated = 6,
    }

    public enum SynthesisOutcomeKind
    {
        HigherTierSpell = 0,
        MagicInk = 1,
    }

    public readonly struct SpellSynthesisRejectedEvent
    {
        public SpellSynthesisRejectedEvent(
            long firstSpellId,
            long secondSpellId,
            SynthesisRejectReason reason)
        {
            FirstSpellId = firstSpellId;
            SecondSpellId = secondSpellId;
            Reason = reason;
        }

        public long FirstSpellId { get; }
        public long SecondSpellId { get; }
        public SynthesisRejectReason Reason { get; }
    }

    public readonly struct SpellSynthesisResolvedEvent
    {
        public SpellSynthesisResolvedEvent(
            SynthesisOutcomeKind kind,
            SpellType rewardSpellType,
            int rewardTier,
            int inkReward)
        {
            Kind = kind;
            RewardSpellType = rewardSpellType;
            RewardTier = rewardTier;
            InkReward = inkReward;
        }

        public SynthesisOutcomeKind Kind { get; }
        public SpellType RewardSpellType { get; }
        public int RewardTier { get; }
        public int InkReward { get; }
    }
}
