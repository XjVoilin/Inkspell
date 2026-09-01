using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    /// <summary>单次挑战的运行时权威状态；随 AutoBattleSystem 生命周期存在且不持久化。</summary>
    [Serializable]
    internal class AutoBattleState
    {
        public long ChallengeId;
        public int StageId;
        public bool IsRunning;
        public float BookHealth;
        public float BookMaxHealth;
        public float BookShield;
        public float BookShieldRemainingSeconds;
        public float SpawnElapsedSeconds;

        // 三类运行时 ID 在挑战内单调递增，供 View 去重和关联表现对象。
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
    internal sealed class EnemyBattleState
    {
        public long RuntimeId;
        public EnemyType Type;
        public float Health;
        public float MaxHealth;

        // 一维路径坐标：数值越小越接近魔法书。
        public float PathPosition;
        public float AttackRemainingSeconds;
        public float SlowRemainingSeconds;
        public float SlowMultiplier = 1f;
    }

    [Serializable]
    internal sealed class SpellSlotCooldownState
    {
        public int EquipmentSlot;
        public float RemainingSeconds;
    }

    [Serializable]
    internal sealed class BattleAttackState
    {
        public long AttackId;
        public int EquipmentSlot;
        public SpellType SpellType;
        public List<long> TargetEnemyIds = new();
        public float TargetPathPosition;
        public float RemainingTravelSeconds;

        // 以下数值在施法瞬间固化，后续升级或换装不会追溯修改已发出的攻击。
        public float Damage;
        public float Shield;
        public float EffectRange;
        public float EffectDurationSeconds;
        public float SlowMultiplier = 1f;
    }

    [Serializable]
    internal sealed class BattleEffectState
    {
        public long EffectId;
        public SpellType SpellType;
        public long TargetEnemyId;
        public float PathPosition;
        public float RemainingSeconds;
    }
}
