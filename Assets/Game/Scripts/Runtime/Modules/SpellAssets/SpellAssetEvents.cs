namespace Game
{
    public readonly struct SpellAssetsChangedEvent
    {
        public SpellAssetsChangedEvent(bool capacityIncreased)
        {
            CapacityIncreased = capacityIncreased;
        }

        public bool CapacityIncreased { get; }
    }
}
