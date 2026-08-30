namespace Game
{
    public enum SpellUpgradeRejectReason
    {
        InsufficientInk = 0,
        MaxLevel = 1,
    }

    public readonly struct SpellUpgradeInfo
    {
        public SpellUpgradeInfo(
            long instanceId,
            int currentLevel,
            int nextLevel,
            int inkCost,
            float currentPowerMultiplier,
            float nextPowerMultiplier,
            bool isMaxLevel,
            int currentInk)
        {
            InstanceId = instanceId;
            CurrentLevel = currentLevel;
            NextLevel = nextLevel;
            InkCost = inkCost;
            CurrentPowerMultiplier = currentPowerMultiplier;
            NextPowerMultiplier = nextPowerMultiplier;
            IsMaxLevel = isMaxLevel;
            CurrentInk = currentInk;
        }

        public long InstanceId { get; }
        public int CurrentLevel { get; }
        public int NextLevel { get; }
        public int InkCost { get; }
        public float CurrentPowerMultiplier { get; }
        public float NextPowerMultiplier { get; }
        public bool IsMaxLevel { get; }
        public int CurrentInk { get; }
    }

    public readonly struct SpellUpgradeRejectedEvent
    {
        public SpellUpgradeRejectedEvent(
            long instanceId,
            SpellUpgradeRejectReason reason)
        {
            InstanceId = instanceId;
            Reason = reason;
        }

        public long InstanceId { get; }
        public SpellUpgradeRejectReason Reason { get; }
    }

    public readonly struct SpellUpgradedEvent
    {
        public SpellUpgradedEvent(SpellUpgradeInfo upgrade)
        {
            Upgrade = upgrade;
        }

        public SpellUpgradeInfo Upgrade { get; }
    }
}
