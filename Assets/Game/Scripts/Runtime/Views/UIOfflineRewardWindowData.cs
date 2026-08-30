namespace Game
{
    /// <summary>
    /// 一次已完成离线生成结算的唯一显示输入。
    /// </summary>
    public sealed class UIOfflineRewardWindowData
    {
        public UIOfflineRewardWindowData()
        {
        }

        public UIOfflineRewardWindowData(OfflineGenerationOutcome outcome)
        {
            ElapsedSeconds = outcome.ElapsedSeconds;
            GeneratedCount = outcome.GeneratedCount;
            TransferredCount = outcome.TransferredCount;
        }

        public long ElapsedSeconds { get; set; }
        public int GeneratedCount { get; set; }
        public int TransferredCount { get; set; }
    }
}
