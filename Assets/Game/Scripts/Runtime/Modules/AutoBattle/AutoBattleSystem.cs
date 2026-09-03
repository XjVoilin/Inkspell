using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using UnityEngine;

namespace Game
{
    /// <summary>编排一次有界自动挑战的生命周期，并把逐帧推进交给战斗模拟。</summary>
    public sealed class AutoBattleSystem : SystemBase, IUpdatableSystem
    {
        private BattleSimulation _simulation;
        private readonly BattleSimulationClock _clock = new();
        private UniTaskCompletionSource<BattleOutcome> _battleCompletion;
        private long _nextBattleRunId = 1;
        private bool _hasFocus;

        internal IReadOnlyBattleRun CurrentRun => _simulation.CurrentRun;

        public async UniTask<BattleOutcome> RunChallengeAsync(
            int stageId,
            CancellationToken ct = default)
        {
            if (_battleCompletion != null)
            {
                throw new InvalidOperationException("同一时间只能运行一次自动战斗挑战。");
            }

            var completion = new UniTaskCompletionSource<BattleOutcome>();
            _battleCompletion = completion;

            try
            {
                _clock.Reset();
                _simulation.Begin(_nextBattleRunId++, stageId);
                Publish(new BattleStateChangedEvent());
                return await completion.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                CleanupChallenge(completion);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_battleCompletion == null || !CurrentRun.IsRunning || !_hasFocus)
            {
                return;
            }

            try
            {
                var stepCount = _clock.TakeSteps(deltaTime);
                if (stepCount == 0)
                {
                    return;
                }

                BattleOutcome? outcome = null;
                for (var step = 0; step < stepCount && CurrentRun.IsRunning; step++)
                {
                    outcome = _simulation.Advance(BattleSimulationClock.StepSeconds);
                    // 每个固定步都发布一次，避免长帧内短暂产生又消失的攻击反馈被跳过。
                    Publish(new BattleStateChangedEvent());
                    if (outcome.HasValue)
                    {
                        break;
                    }
                }

                if (!outcome.HasValue)
                {
                    return;
                }

                Publish(new BattleChallengeEndedEvent(outcome.Value));
                (_battleCompletion ?? throw new InvalidOperationException("当前战斗缺少完成源。"))
                    .TrySetResult(outcome.Value);
            }
            catch (Exception exception)
            {
                if (CurrentRun.IsRunning)
                {
                    _simulation.Stop();
                    Publish(new BattleStateChangedEvent());
                }

                // 通过 RunChallengeAsync 的单一异步通道向调用方报告错误，
                // 避免同一异常又从 Unity Update 重复抛出。
                _battleCompletion?.TrySetException(exception);
            }
        }

        protected override UniTask OnInitializeAsync()
        {
            var config = GetSystem<IConfigSystem>();
            _simulation = new BattleSimulation(
                GetStore<SpellAssetStore>(),
                config.GetTable<TbBattleRule>(),
                config.GetTable<TbSpellAssetRule>(),
                config.GetTable<TbSpellCombat>(),
                config.GetTable<TbSpellUpgrade>(),
                config.GetTable<TbEnemy>(),
                config.GetTable<TbStageBattle>());
            _hasFocus = Application.isFocused;
            Application.focusChanged += OnFocusChanged;
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            Application.focusChanged -= OnFocusChanged;
            _clock.Reset();

            if (CurrentRun.IsRunning)
            {
                _simulation.Stop();
            }

            _battleCompletion?.TrySetCanceled();
            _battleCompletion = null;
        }

        private void CleanupChallenge(UniTaskCompletionSource<BattleOutcome> completion)
        {
            if (!ReferenceEquals(_battleCompletion, completion))
            {
                return;
            }

            if (CurrentRun.IsRunning)
            {
                _simulation.Stop();
                Publish(new BattleStateChangedEvent());
            }

            _battleCompletion = null;
        }

        private void OnFocusChanged(bool hasFocus)
        {
            _hasFocus = hasFocus;
            if (!hasFocus)
            {
                // 丢弃不足一个固定步的余量，恢复前台时不补算后台战斗。
                _clock.Reset();
            }
        }
    }
}
