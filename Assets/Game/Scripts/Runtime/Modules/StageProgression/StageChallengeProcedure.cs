using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;

namespace Game
{
    internal sealed class StageChallengeProcedure : ProcedureBase
    {
        private readonly StageProgression _stage;

        internal StageChallengeProcedure(StageProgression stage)
        {
            _stage = stage;
        }

        protected override async UniTask OnExecuteAsync(CancellationToken ct)
        {
            var outcome = await GetSystem<AutoBattleSystem>()
                .RunChallengeAsync(_stage.StageId, ct);

            if (outcome.Victory && !_stage.IsMaxStage)
            {
                GetStore<StageProgressionStore>().AdvanceOneStage();
            }

            // 进度先提交再等待表现停顿；停顿结束后外层循环才会发起下一场挑战。
            var pauseSeconds = outcome.Victory
                ? _stage.VictoryPauseSeconds
                : _stage.FailurePauseSeconds;
            await UniTask.Delay(
                TimeSpan.FromSeconds(pauseSeconds),
                cancellationToken: ct);
        }
    }
}
