using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game
{
    /// <summary>单次战斗运行聚合；随 AutoBattleSystem 生命周期存在且不持久化。</summary>
    internal sealed class BattleRun
    {
        private readonly List<BattleAttack> _attacks = new();
        private readonly List<BattleEffect> _effects = new();
        private readonly ReadOnlyCollection<BattleAttack> _attacksView;
        private readonly ReadOnlyCollection<BattleEffect> _effectsView;
        private long _nextAttackId = 1;
        private long _nextEffectId = 1;

        internal BattleRun(
            long battleRunId,
            int stageId,
            float bookMaxHealth,
            int equipmentSlotCount)
        {
            _attacksView = _attacks.AsReadOnly();
            _effectsView = _effects.AsReadOnly();
            BattleRunId = battleRunId;
            StageId = stageId;
            IsRunning = true;
            Book = new BattleBook(bookMaxHealth);
            Cooldowns.Initialize(equipmentSlotCount);
        }

        public long BattleRunId { get; }
        public int StageId { get; }
        public bool IsRunning { get; private set; }
        public float SpawnElapsedSeconds { get; private set; }
        public BattleOutcome? Outcome { get; private set; }

        internal BattleBook Book { get; }
        internal EnemyRoster Enemies { get; } = new();
        internal SpellCooldownSet Cooldowns { get; } = new();
        internal IReadOnlyList<BattleAttack> Attacks => _attacksView;
        internal IReadOnlyList<BattleEffect> Effects => _effectsView;


        internal void AdvanceSpawnTime(float deltaTime)
        {
            SpawnElapsedSeconds += deltaTime;
        }

        internal long AllocateAttackId()
        {
            return _nextAttackId++;
        }

        internal void AddAttack(BattleAttack attack)
        {
            _attacks.Add(attack);
        }

        internal void RemoveAttackAt(int index)
        {
            _attacks.RemoveAt(index);
        }

        internal long AllocateEffectId()
        {
            return _nextEffectId++;
        }

        internal void AddEffect(BattleEffect effect)
        {
            _effects.Add(effect);
        }

        internal void RemoveEffectAt(int index)
        {
            _effects.RemoveAt(index);
        }

        internal BattleOutcome Complete(bool victory)
        {
            if (!IsRunning)
            {
                throw new System.InvalidOperationException("当前挑战已经产生最终结果。");
            }

            var outcome = new BattleOutcome(BattleRunId, StageId, victory);
            Outcome = outcome;
            IsRunning = false;
            return outcome;
        }

        internal void Stop()
        {
            IsRunning = false;
        }
    }
}
