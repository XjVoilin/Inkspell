using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>决定长期关卡进度，并管理逐轮挑战流程的启停。</summary>
    public sealed class StageProgressionSystem : SystemBase
    {
        private StageProgressionStore _store;
        private TbStageProgression _stages;
        private AutoBattleSystem _battle;
        private CancellationTokenSource _continuousCancellation;
        private long _lastCommittedBattleRunId;

        internal StageProgression CurrentStage => _stages.Get(_store.CurrentStageId);

        public void StartContinuousChallenges()
        {
            if (_continuousCancellation != null)
                return;
            var cancellation = new CancellationTokenSource();
            _continuousCancellation = cancellation;
            RunContinuousChallengesAsync(cancellation).Forget();
        }

        public void StopContinuousChallenges()
        {
            _continuousCancellation?.Cancel();
        }

        internal void CommitChallenge(BattleOutcome outcome)
        {
            var run = _battle.CurrentRun;
            if (run == null || !run.Outcome.HasValue ||
                run.BattleRunId != outcome.BattleRunId ||
                run.StageId != outcome.StageId ||
                run.Outcome.Value.Victory != outcome.Victory ||
                outcome.BattleRunId <= _lastCommittedBattleRunId)
                throw new InvalidOperationException("只能提交当前已完成且尚未提交的挑战结果。");

            var stage = CurrentStage;
            if (stage.StageId != outcome.StageId)
                throw new InvalidOperationException("挑战关卡与当前进度不一致。");

            _lastCommittedBattleRunId = outcome.BattleRunId;
            if (outcome.Victory && !stage.IsMaxStage)
            {
                var nextIndex = _stages.DataList.IndexOf(stage) + 1;
                _store.SetCurrentStage(_stages.DataList[nextIndex].StageId);
            }
        }

        protected override UniTask OnInitializeAsync()
        {
            _store = GetStore<StageProgressionStore>();
            _stages = GetSystem<IConfigSystem>().GetTable<TbStageProgression>();
            _battle = GetSystem<AutoBattleSystem>();
            _store.Initialize(_stages.DataList[0].StageId);
            // 存档恢复边界：不存在的关卡不能被当作成功恢复。
            _ = CurrentStage;
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            StopContinuousChallenges();
        }

        private async UniTask RunContinuousChallengesAsync(CancellationTokenSource cancellation)
        {
            try
            {
                while (true)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    await RunProcedure(new StageChallengeProcedure(CurrentStage), cancellation.Token);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // 退出玩法是正常取消，不提交失败结果。
            }
            finally
            {
                if (ReferenceEquals(_continuousCancellation, cancellation))
                {
                    _continuousCancellation = null;
                    _battle.ClearFinishedChallenge();
                }
                cancellation.Dispose();
            }
        }
    }
}
