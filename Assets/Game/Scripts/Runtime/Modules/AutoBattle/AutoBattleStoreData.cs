using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    [Serializable]
    public sealed class AutoBattleStoreData
    {
        public long ChallengeId;
        public int StageId;
        public bool IsRunning;
        public float BookHealth;
        public float BookMaxHealth;
        public float BookShield;
        public float BookShieldRemainingSeconds;
        public float SpawnElapsedSeconds;
        public long NextEnemyRuntimeId = 1;
        public long NextAttackId = 1;
        public long NextEffectId = 1;
        public List<EnemyBattleState> Enemies = new();
        public List<SpellSlotCooldownState> Cooldowns = new();
        public List<BattleAttackState> Attacks = new();
        public List<BattleEffectState> Effects = new();
        public BattleOutcome? Outcome;
    }

    [Serializable]
    public sealed class EnemyBattleState
    {
        public long RuntimeId;
        public EnemyType Type;
        public float Health;
        public float MaxHealth;
        public float PathPosition;
        public float AttackRemainingSeconds;
        public float SlowRemainingSeconds;
        public float SlowMultiplier = 1f;
    }

    [Serializable]
    public sealed class SpellSlotCooldownState
    {
        public int EquipmentSlot;
        public float RemainingSeconds;
    }

    [Serializable]
    public sealed class BattleAttackState
    {
        public long AttackId;
        public int EquipmentSlot;
        public SpellType SpellType;
        public List<long> TargetEnemyIds = new();
        public float TargetPathPosition;
        public float RemainingTravelSeconds;
        public float Damage;
        public float Shield;
        public float EffectRange;
        public float EffectDurationSeconds;
        public float SlowMultiplier = 1f;
    }

    [Serializable]
    public sealed class BattleEffectState
    {
        public long EffectId;
        public SpellType SpellType;
        public long TargetEnemyId;
        public float PathPosition;
        public float RemainingSeconds;
    }
}
