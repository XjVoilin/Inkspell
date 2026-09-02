using System;
using cfg;

namespace Game
{
    /// <summary>
    /// 主界面战场区域的唯一显示输入。
    /// 字段可写，供 GM 构造显示测试数据。
    /// </summary>
    public sealed class BattlefieldViewData
    {
        public long BattleRunId { get; set; }
        public bool IsRunning { get; set; }
        public float BookHealth { get; set; }
        public float BookMaxHealth { get; set; }
        public float BookShield { get; set; }
        public float BookShieldMaximum { get; set; }
        public EnemyBattleViewData[] Enemies { get; set; } = Array.Empty<EnemyBattleViewData>();
        public SpellCooldownViewData[] Cooldowns { get; set; } = Array.Empty<SpellCooldownViewData>();
        public BattleAttackFeedbackViewData[] Attacks { get; set; } =
            Array.Empty<BattleAttackFeedbackViewData>();
        public BattleEffectFeedbackViewData[] Effects { get; set; } =
            Array.Empty<BattleEffectFeedbackViewData>();
    }

    public sealed class EnemyBattleViewData
    {
        public long RuntimeId { get; set; }
        public EnemyType Type { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float PathNormalized { get; set; }
        public float SlowRemainingSeconds { get; set; }
        public float SlowMultiplier { get; set; }
        public bool IsSlowed => SlowRemainingSeconds > 0f;
    }

    public sealed class SpellCooldownViewData
    {
        public int EquipmentSlot { get; set; }
        public float RemainingSeconds { get; set; }
        public float TotalSeconds { get; set; }
        public float ReadyProgressSeconds => Math.Max(0f, TotalSeconds - RemainingSeconds);
    }

    /// <summary>
    /// 已提交攻击的短暂表现标识；View 只按 ID 播放，不决定命中或伤害。
    /// </summary>
    public sealed class BattleAttackFeedbackViewData
    {
        public long AttackId { get; set; }
        public SpellType SpellType { get; set; }
        public float TargetPathNormalized { get; set; }
        public float RemainingTravelSeconds { get; set; }
    }

    /// <summary>
    /// 已提交效果的短暂表现标识；View 只按 ID 播放，不修改效果状态。
    /// </summary>
    public sealed class BattleEffectFeedbackViewData
    {
        public long EffectId { get; set; }
        public SpellType SpellType { get; set; }
        public long TargetEnemyId { get; set; }
        public float PathNormalized { get; set; }
        public float RemainingSeconds { get; set; }
    }
}
