using System.Collections.Generic;
using cfg;
// ReSharper disable All

namespace Game
{
    /// <summary>单次战斗运行实例；随 AutoBattleSystem 生命周期存在且不持久化。</summary>
    internal sealed class BattleRun
    {
        internal BattleRun()
        {
        }

        internal BattleRun(
            long battleRunId,
            int stageId,
            float bookMaxHealth,
            int equipmentSlotCount)
        {
            BattleRunId = battleRunId;
            StageId = stageId;
            IsRunning = true;
            Book = new BattleBook(bookMaxHealth);
            Cooldowns.Initialize(equipmentSlotCount);
        }

        // BattleRunId 标识某一次战斗尝试；同一 StageId 重试时会产生新值。
        internal long BattleRunId { get; }

        // StageId 标识稳定的关卡配置；同一关的多次尝试共享此值。
        internal int StageId { get; }
        internal bool IsRunning { get; set; }
        internal float SpawnElapsedSeconds { get; set; }

        internal BattleBook Book { get; } = new();
        internal EnemyRoster Enemies { get; } = new();
        internal SpellCooldownSet Cooldowns { get; } = new();

        // 攻击和效果 ID 在当次战斗内单调递增，供 View 去重。
        internal long NextAttackId { get; set; } = 1;
        internal long NextEffectId { get; set; } = 1;
        internal List<BattleAttack> Attacks { get; } = new();
        internal List<BattleEffect> Effects { get; } = new();
        internal BattleOutcome? Outcome { get; set; }
    }

    internal sealed class BattleAttack
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

    internal sealed class BattleEffect
    {
        public long EffectId;
        public SpellType SpellType;
        public long TargetEnemyId;
        public float PathPosition;
        public float RemainingSeconds;
    }
}
