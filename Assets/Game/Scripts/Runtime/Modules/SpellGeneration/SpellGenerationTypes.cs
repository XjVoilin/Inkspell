namespace Game
{
    public readonly struct OfflineGenerationOutcome
    {
        public OfflineGenerationOutcome(
            long elapsedSeconds,
            int generatedCount,
            int transferredCount)
        {
            ElapsedSeconds = elapsedSeconds;
            GeneratedCount = generatedCount;
            TransferredCount = transferredCount;
        }

        public long ElapsedSeconds { get; }
        public int GeneratedCount { get; }
        public int TransferredCount { get; }
    }

    public readonly struct SpellGenerationChangedEvent
    {
    }
}
