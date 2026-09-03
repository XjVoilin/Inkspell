using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>编排一次有界自动挑战的生命周期，并把逐帧推进交给战斗模拟。</summary>
    public sealed class AutoBattleSystem : SystemBase, IUpdatableSystem
    {
        private BattleSimulation _simulation;
        private UniTaskCompletionSource<BattleOutcome> _battleCompletion;
        private long _nextBattleRunId = 1;

        internal BattleRun CurrentRun => _simulation.CurrentRun;

        public async UniTask<BattleOutcome> RunChallengeAsync(
            int stageId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (_battleCompletion != null)
            {
                throw new InvalidOperationException("同一时间只能运行一次自动战斗挑战。");
            }

            var completion = new UniTaskCompletionSource<BattleOutcome>();
            _battleCompletion = completion;

            try
            {
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
            if (_battleCompletion == null || !CurrentRun.IsRunning)
            {
                return;
            }

            try
            {
                var outcome = _simulation.Advance(deltaTime);
                Publish(new BattleStateChangedEvent());
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
            return UniTask.CompletedTask;
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
    }
}
