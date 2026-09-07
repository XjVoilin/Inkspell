using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;

namespace Game
{
    /// <summary>单次战斗运行聚合；随 AutoBattleSystem 生命周期存在且不持久化。</summary>
    internal sealed class BattleRun
    {
        private readonly List<BattleAttack> _attacks = new();
        private readonly List<BattleEffect> _effects = new();
        private readonly List<BattleFact> _pendingFacts = new();
        private readonly ReadOnlyCollection<BattleAttack> _attacksView;
        private readonly ReadOnlyCollection<BattleEffect> _effectsView;
        private long _nextAttackId = 1;
        private long _nextEffectId = 1;

        internal BattleRun(long battleRunId, int stageId, float bookMaxHealth, int equipmentSlotCount)
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

        internal void RecordFact(BattleFact fact) => _pendingFacts.Add(fact);

        internal IReadOnlyList<BattleFact> TakeFacts()
        {
            if (_pendingFacts.Count == 0)
                return System.Array.Empty<BattleFact>();
            // 发布前转移所有权；同步监听方取消或开始下一局时不会改变正在交付的事实。
            var facts = System.Array.AsReadOnly(_pendingFacts.ToArray());
            _pendingFacts.Clear();
            return facts;
        }


        internal void AdvanceSpawnTime(float deltaTime)
        {
            SpawnElapsedSeconds += deltaTime;
        }

        internal void AddAttack(
            SpellType spellType,
            IReadOnlyList<long> targetEnemyIds,
            float targetPathPosition,
            float travelSeconds,
            float damage,
            float shield,
            float effectRange,
            float effectDurationSeconds,
            float slowMultiplier)
        {
            _attacks.Add(new BattleAttack(
                _nextAttackId++,
                spellType,
                targetEnemyIds,
                targetPathPosition,
                travelSeconds,
                damage,
                shield,
                effectRange,
                effectDurationSeconds,
                slowMultiplier));
        }

        internal void RemoveAttackAt(int index)
        {
            _attacks.RemoveAt(index);
        }

        internal void AddEffect(
            SpellType spellType,
            long targetEnemyId,
            float pathPosition,
            float range,
            float durationSeconds)
        {
            _effects.Add(new BattleEffect(
                _nextEffectId++,
                spellType,
                targetEnemyId,
                pathPosition,
                range,
                durationSeconds));
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
