using System;
using cfg;
using UnityEngine;

namespace Game
{
    /// <summary>Authored tier silhouettes shared by cards and battlefield feedback.</summary>
    [Serializable]
    public struct SpellTierVisualSet
    {
        public SpellType SpellType;
        public string ResourceKey;
        public Sprite Tier1;
        public Sprite Tier2;
        public Sprite Tier3;

        public Sprite Get(int tier)
        {
            return Mathf.Clamp(tier, 1, 3) switch
            {
                1 => Tier1,
                2 => Tier2,
                _ => Tier3,
            };
        }
    }
}
