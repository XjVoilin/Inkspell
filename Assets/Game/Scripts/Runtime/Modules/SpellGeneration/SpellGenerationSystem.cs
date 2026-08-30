using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Time;
using UnityEngine;

namespace Game
{
    public sealed class SpellGenerationSystem : SystemBase, IUpdatableSystem
    {
        private SpellGenerationStore _store;
        private SpellAssetSystem _spellAssets;
        private StageProgressionSystem _stageProgression;
        private ITimeSystem _time;
        private SpellGeneration _generationRule;
        private TbStageGeneration _stageGeneration;
        private bool _hasFocus;

        public int PendingCount => _store.PendingCount;
        public float CurrentCycleProgressSeconds => _store.CycleProgressSeconds;
        public float CurrentIntervalSeconds => _store.ActiveIntervalSeconds;

        public async UniTask<OfflineGenerationOutcome> SettleOfflineAsync(
            CancellationToken ct = default)
        {
            var procedure = new OfflineGenerationProcedure();
            await RunProcedure(procedure, ct);
            _store.UpdateInterval(ResolveInterval(_stageProgression.CurrentStageId));
            return procedure.Outcome;
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_hasFocus || _store.HasInactiveAnchor)
            {
                return;
            }

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
            _spellAssets = GetSystem<SpellAssetSystem>();
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

        internal int TransferPendingSpells(CancellationToken ct = default)
        {
            var transferredCount = 0;
            while (_store.TryPeekPendingSpell(out var type))
            {
                ct.ThrowIfCancellationRequested();
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
                SettleOfflineAsync().Forget();
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
    }
}
