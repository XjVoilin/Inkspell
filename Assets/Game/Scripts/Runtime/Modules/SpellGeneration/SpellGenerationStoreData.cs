using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    [Serializable]
    public sealed class SpellGenerationStoreData
    {
        public bool Initialized;
        public List<SpellType> PendingSpells = new();
        public float CycleProgressSeconds;
        public float ActiveIntervalSeconds;
        public long InactiveSinceUtcSeconds;
        public bool HasInactiveAnchor;
    }
}
