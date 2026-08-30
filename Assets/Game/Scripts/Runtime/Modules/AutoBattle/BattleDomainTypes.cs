using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;

namespace Game
{
    [Serializable]
    public readonly struct BattleOutcome
    {
        public BattleOutcome(long challengeId, int stageId, bool victory)
        {
            ChallengeId = challengeId;
            StageId = stageId;
            Victory = victory;
        }

        public long ChallengeId { get; }
        public int StageId { get; }
        public bool Victory { get; }
    }

    public readonly struct EnemyBattleView
    {
        internal EnemyBattleView(EnemyBattleState state)
        {
            RuntimeId = state.RuntimeId;
            Type = state.Type;
            Health = state.Health;
            MaxHealth = state.MaxHealth;
            PathPosition = state.PathPosition;
            SlowRemainingSeconds = state.SlowRemainingSeconds;
            SlowMultiplier = state.SlowMultiplier;
        }

        public long RuntimeId { get; }
        public EnemyType Type { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public float PathPosition { get; }
        public float SlowRemainingSeconds { get; }
        public float SlowMultiplier { get; }
    }

    public readonly struct SpellCooldownView
    {
        internal SpellCooldownView(SpellSlotCooldownState state)
        {
            EquipmentSlot = state.EquipmentSlot;
            RemainingSeconds = state.RemainingSeconds;
        }

        public int EquipmentSlot { get; }
        public float RemainingSeconds { get; }
    }

    public readonly struct BattleAttackView
    {
        internal BattleAttackView(BattleAttackState state)
        {
            AttackId = state.AttackId;
            EquipmentSlot = state.EquipmentSlot;
            SpellType = state.SpellType;
            TargetEnemyIds = new ReadOnlyCollection<long>(state.TargetEnemyIds.ToArray());
            TargetPathPosition = state.TargetPathPosition;
            RemainingTravelSeconds = state.RemainingTravelSeconds;
        }

        public long AttackId { get; }
        public int EquipmentSlot { get; }
        public SpellType SpellType { get; }
        public IReadOnlyList<long> TargetEnemyIds { get; }
        public float TargetPathPosition { get; }
        public float RemainingTravelSeconds { get; }
    }

    public readonly struct BattleEffectView
    {
        internal BattleEffectView(BattleEffectState state)
        {
            EffectId = state.EffectId;
            SpellType = state.SpellType;
            TargetEnemyId = state.TargetEnemyId;
            PathPosition = state.PathPosition;
            RemainingSeconds = state.RemainingSeconds;
        }

        public long EffectId { get; }
        public SpellType SpellType { get; }
        public long TargetEnemyId { get; }
        public float PathPosition { get; }
        public float RemainingSeconds { get; }
    }

    public readonly struct BattleViewState
    {
        internal BattleViewState(
            long challengeId,
            bool isRunning,
            int stageId,
            float bookHealth,
            float bookMaxHealth,
            float bookShield,
            IReadOnlyList<EnemyBattleView> enemies,
            IReadOnlyList<SpellCooldownView> cooldowns,
            IReadOnlyList<BattleAttackView> attacks,
            IReadOnlyList<BattleEffectView> effects,
            BattleOutcome? outcome)
        {
            ChallengeId = challengeId;
            IsRunning = isRunning;
            StageId = stageId;
            BookHealth = bookHealth;
            BookMaxHealth = bookMaxHealth;
            BookShield = bookShield;
            Enemies = enemies;
            Cooldowns = cooldowns;
            Attacks = attacks;
            Effects = effects;
            Outcome = outcome;
        }

        public long ChallengeId { get; }
        public bool IsRunning { get; }
        public int StageId { get; }
        public float BookHealth { get; }
        public float BookMaxHealth { get; }
        public float BookShield { get; }
        public IReadOnlyList<EnemyBattleView> Enemies { get; }
        public IReadOnlyList<SpellCooldownView> Cooldowns { get; }
        public IReadOnlyList<BattleAttackView> Attacks { get; }
        public IReadOnlyList<BattleEffectView> Effects { get; }
        public BattleOutcome? Outcome { get; }
    }
}
