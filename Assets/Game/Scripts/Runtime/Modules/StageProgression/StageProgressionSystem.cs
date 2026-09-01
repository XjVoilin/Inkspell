using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>串行编排当前关卡的挑战、结果提交、停顿与下一次挑战。</summary>
    public sealed class StageProgressionSystem : SystemBase
    {
        private StageProgressionStore _store;
        private TbStageProgression _stages;
        private CancellationTokenSource _continuousCancellation;

        public void StartContinuousChallenges()
        {
            if (_continuousCancellation != null)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _continuousCancellation = cancellation;
            RunContinuousChallengesAsync(cancellation).Forget();
        }

        public void StopContinuousChallenges()
        {
            _continuousCancellation?.Cancel();
        }

        protected override UniTask OnInitializeAsync()
        {
            _store = GetStore<StageProgressionStore>();
            _stages = GetSystem<IConfigSystem>().GetTable<TbStageProgression>();
            _store.Initialize(_stages);
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            StopContinuousChallenges();
        }

        private async UniTask RunContinuousChallengesAsync(
            CancellationTokenSource cancellation)
        {
            try
            {
                while (true)
                {
                    // 每轮重新读取 CurrentStageId，确保胜利提交的进度在下一轮立即生效。
                    cancellation.Token.ThrowIfCancellationRequested();
                    var stage = _stages.Get(_store.CurrentStageId);
                    await RunProcedure(
                        new StageChallengeProcedure(stage),
                        cancellation.Token);
                }
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(_continuousCancellation, cancellation))
                {
                    _continuousCancellation = null;
                }

                cancellation.Dispose();
            }
        }
    }
}
