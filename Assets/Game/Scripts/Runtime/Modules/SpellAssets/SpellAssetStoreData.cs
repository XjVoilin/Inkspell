using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    [Serializable]
    public sealed class SpellAssetStoreData
    {
        public bool Initialized;
        public long NextInstanceId;
        public int MagicInk;
        public List<SpellInstanceState> Spells = new();
    }

    [Serializable]
    public sealed class SpellInstanceState
    {
        public long InstanceId;
        public SpellType Type;
        public int Tier;
        public int Level;
        public SpellLocation Location;
        public int EquipmentSlot;
        public bool IsLocked;
    }
}
