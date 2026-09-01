namespace Game
{
    public readonly struct SpellAssetsChangedEvent
    {
        public SpellAssetsChangedEvent(bool craftingSpaceFreed)
        {
            CraftingSpaceFreed = craftingSpaceFreed;
        }

        public bool CraftingSpaceFreed { get; }
    }
}
