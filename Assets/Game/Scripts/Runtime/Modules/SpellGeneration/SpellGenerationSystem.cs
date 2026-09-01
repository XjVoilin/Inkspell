using System;
using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Time;
using UnityEngine;

namespace Game
{
    /// <summary>统一推进在线生成、触发离线结算，并把待领取法术按队列移交给资产模块。</summary>
    public sealed class SpellGenerationSystem : SystemBase, IUpdatableSystem
    {
        private SpellGenerationStore _store;
        private SpellAssetStore _spellAssets;
        private StageProgressionSystem _stageProgression;
        private ITimeSystem _time;
        private SpellGeneration _generationRule;
        private TbStageGeneration _stageGeneration;
        private bool _hasFocus;

        public int PendingCount => _store.PendingCount;
        public float CurrentCycleProgressSeconds => _store.CycleProgressSeconds;
        public float CurrentIntervalSeconds => _store.ActiveIntervalSeconds;

        public OfflineGenerationOutcome SettleOffline()
        {
            if (!_store.HasInactiveAnchor)
            {
                _store.UpdateInterval(ResolveInterval(_stageProgression.CurrentStageId));
                var emptyOutcome = new OfflineGenerationOutcome(0L, 0, 0);
                Publish(new OfflineGenerationSettledEvent(emptyOutcome));
                return emptyOutcome;
            }

            var elapsedSeconds = ResolveElapsedSeconds(
                _time.ServerTimeSeconds,
                _store.InactiveSinceUtcSeconds,
                _generationRule.OfflineLimitSeconds);
            var intervalSeconds = Math.Max(
                _store.ActiveIntervalSeconds,
                _generationRule.MinimumIntervalSeconds);
            // 离线时间与离开前未完成的周期连续累计，而不是重新从零开始。
            var totalProgressSeconds = _store.CycleProgressSeconds + (double)elapsedSeconds;
            var generatedCount = checked(
                (int)Math.Floor(totalProgressSeconds / intervalSeconds));
            var remainingProgressSeconds = (float)(
                totalProgressSeconds - generatedCount * intervalSeconds);

            var generatedSpells = new List<SpellType>(generatedCount);
            for (var index = 0; index < generatedCount; index++)
            {
                generatedSpells.Add(SelectGeneratedSpell());
            }

            // 先把全部收益写入待领取，再尝试移交；容量不足时剩余产物仍可在之后领取。
            _store.CommitOfflineSettlement(remainingProgressSeconds, generatedSpells);
            var transferredCount = TransferPendingSpells();
            _store.UpdateInterval(ResolveInterval(_stageProgression.CurrentStageId));
            var outcome = new OfflineGenerationOutcome(
                elapsedSeconds,
                generatedCount,
                transferredCount);
            Publish(new OfflineGenerationSettledEvent(outcome));
            return outcome;
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_hasFocus || _store.HasInactiveAnchor)
            {
                return;
            }

            // 单帧可能跨越多个周期，因此批量结算完整周期并保留不足一周期的余量。
            var interval = _store.ActiveIntervalSeconds;
            var elapsed = _store.CycleProgressSeconds + (double)deltaTime;
            var generatedCount = checked((int)Math.Floor(elapsed / interval));
            var remaining = (float)(elapsed - generatedCount * interval);

            IReadOnlyList<SpellType> generatedSpells = Array.Empty<SpellType>();
            if (generatedCount > 0)
            {
                var spells = new List<SpellType>(generatedCount);
                for (var index = 0; index < generatedCount; index++)
                {
                    spells.Add(SelectGeneratedSpell());
                }

                generatedSpells = spells;
            }

            _store.CommitOnlineProgress(remaining, generatedSpells);
            TransferPendingSpells();
        }

        protected override UniTask OnInitializeAsync()
        {
            _store = GetStore<SpellGenerationStore>();
            _spellAssets = GetStore<SpellAssetStore>();
            _stageProgression = GetSystem<StageProgressionSystem>();
            _time = GetSystem<ITimeSystem>();

            var config = GetSystem<IConfigSystem>();
            _generationRule = config.GetTable<TbSpellGeneration>().Data;
            _stageGeneration = config.GetTable<TbStageGeneration>();

            _store.Initialize(ResolveInterval(_stageProgression.CurrentStageId));
            _hasFocus = Application.isFocused;

            Subscribe<SpellAssetsChangedEvent>(OnSpellAssetsChanged);
            Subscribe<StageProgressChangedEvent>(OnStageProgressChanged);
            Application.focusChanged += OnFocusChanged;
            Application.quitting += OnQuitting;
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= OnQuitting;
        }

        internal SpellType SelectGeneratedSpell()
        {
            var totalWeight = 0;
            for (var index = 0; index < _generationRule.Weights.Count; index++)
            {
                totalWeight = checked(totalWeight + _generationRule.Weights[index]);
            }

            var randomWeight = UnityEngine.Random.Range(0, totalWeight);
            for (var index = 0; index < _generationRule.SpellTypes.Count; index++)
            {
                randomWeight -= _generationRule.Weights[index];
                if (randomWeight < 0)
                {
                    return _generationRule.SpellTypes[index];
                }
            }

            throw new InvalidOperationException("法术生成权重无法选出产物。");
        }

        internal int TransferPendingSpells()
        {
            var transferredCount = 0;
            // 严格按队首移交；合成区满时停止，但不丢弃任何已生成产物。
            while (_store.TryPeekPendingSpell(out var type))
            {
                if (!_spellAssets.TryReceiveGeneratedSpell(type))
                {
                    break;
                }

                _store.ConfirmTransferredSpell(type);
                transferredCount++;
            }

            return transferredCount;
        }

        private float ResolveInterval(int stageId)
        {
            return Math.Max(
                _stageGeneration.Get(stageId).IntervalSeconds,
                _generationRule.MinimumIntervalSeconds);
        }

        private void OnSpellAssetsChanged(SpellAssetsChangedEvent eventData)
        {
            if (eventData.CapacityIncreased)
            {
                TransferPendingSpells();
            }
        }

        private void OnStageProgressChanged(StageProgressChangedEvent eventData)
        {
            // 后台期间冻结间隔，待本次离线收益用旧间隔结算完后再采用新关卡间隔。
            if (_store.HasInactiveAnchor)
            {
                return;
            }

            _store.UpdateInterval(ResolveInterval(eventData.CurrentStageId));
        }

        private void OnFocusChanged(bool hasFocus)
        {
            _hasFocus = hasFocus;
            if (!hasFocus)
            {
                RecordInactive();
            }
            else
            {
                SettleOffline();
            }
        }

        private void OnQuitting()
        {
            RecordInactive();
        }

        private void RecordInactive()
        {
            _store.RecordInactive(_time.ServerTimeSeconds);
        }

        private static long ResolveElapsedSeconds(
            long nowUtcSeconds,
            long inactiveSinceUtcSeconds,
            long offlineLimitSeconds)
        {
            // 设备时间回拨视为零收益，正常离线时间则受配置上限约束。
            if (nowUtcSeconds <= inactiveSinceUtcSeconds)
            {
                return 0L;
            }

            return inactiveSinceUtcSeconds < nowUtcSeconds - offlineLimitSeconds
                ? offlineLimitSeconds
                : nowUtcSeconds - inactiveSinceUtcSeconds;
        }
    }
}
