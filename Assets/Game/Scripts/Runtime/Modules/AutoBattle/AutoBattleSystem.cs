using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using UnityEngine;

namespace Game
{
    /// <summary>拥有一局战斗及其时间、等待和结束；内部模拟负责战斗规则。</summary>
    public sealed class AutoBattleSystem : SystemBase, IUpdatableSystem
    {
        private BattleSimulation _simulation;
        private readonly BattleSimulationClock _clock = new();
        private UniTaskCompletionSource<BattleOutcome> _battleCompletion;
        private long _nextBattleRunId = 1;
        private bool _hasFocus;
        private bool _discardResumeFrame;
        
        // 无挑战时为 null；正常胜负后保留末帧，供结果停顿期间读取。
        internal BattleRun CurrentRun { get; private set; }
        internal double ForegroundElapsedSeconds { get; private set; }

        public async UniTask<BattleOutcome> RunChallengeAsync(
            int stageId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (_battleCompletion != null)
                throw new InvalidOperationException("同一时间只能运行一次自动战斗挑战。");

            var run = _simulation.CreateRun(_nextBattleRunId++, stageId);
            var completion = new UniTaskCompletionSource<BattleOutcome>();
            CurrentRun = run;
            _battleCompletion = completion;
            _clock.Reset();

            try
            {
                Publish(new BattleStateChangedEvent());
                return await completion.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                // 取消或关闭可以同步触发 continuation；旧调用不能清理下一局。
                if (ReferenceEquals(_battleCompletion, completion))
                {
                    _battleCompletion = null;
                    if (!run.Outcome.HasValue)
                    {
                        run.Stop();
                        CurrentRun = null;
                        _clock.Reset();
                        Publish(new BattleStateChangedEvent());
                    }
                }
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_hasFocus)
                return;
            if (_discardResumeFrame)
            {
                _discardResumeFrame = false;
                return;
            }

            var stepCount = _clock.TakeSteps(deltaTime);
            // 结算停顿也使用这条前台时间轴；切后台不会跳过结果停顿。
            ForegroundElapsedSeconds += stepCount * (double)BattleSimulationClock.StepSeconds;
            var completion = _battleCompletion;
            if (completion == null)
                return;

            var run = CurrentRun;
            for (var step = 0; step < stepCount; step++)
            {
                BattleOutcome? outcome;
                try
                {
                    outcome = _simulation.Advance(run, BattleSimulationClock.StepSeconds);
                }
                catch (Exception exception)
                {
                    // 将逐帧模拟异常交给等待这局的调用方，finally 释放战况及占用。
                    completion.TrySetException(exception);
                    return;
                }

                if (outcome.HasValue)
                {
                    try
                    {
                        Publish(new BattleStateChangedEvent());
                        Publish(new BattleChallengeEndedEvent(outcome.Value));
                    }
                    finally
                    {
                        // 即使表现监听失败，已结束的业务等待也必须完成。
                        completion.TrySetResult(outcome.Value);
                    }
                    return;
                }

                // 每步交付状态，保证同一长帧内发起又命中的攻击也被表现消费。
                Publish(new BattleStateChangedEvent());
                if (!ReferenceEquals(_battleCompletion, completion))
                    return;
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
            var completion = _battleCompletion;
            _battleCompletion = null;
            CurrentRun?.Stop();
            CurrentRun = null;
            _clock.Reset();
            completion?.TrySetCanceled();
        }

        internal void ClearFinishedChallenge()
        {
            if (_battleCompletion != null)
                throw new InvalidOperationException("尚未结束的挑战不能作为结算画面清理。");
            if (CurrentRun == null)
                return;
            CurrentRun = null;
            Publish(new BattleStateChangedEvent());
        }

        internal void OnFocusChanged(bool hasFocus)
        {
            _hasFocus = hasFocus;
            _clock.Reset();
            // Unity 返回前台的首帧 deltaTime 可能包含后台时长。
            _discardResumeFrame = hasFocus;
        }
    }
}
