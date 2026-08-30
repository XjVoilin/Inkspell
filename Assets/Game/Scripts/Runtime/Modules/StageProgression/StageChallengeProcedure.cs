using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;

namespace Game
{
    public sealed class StageChallengeProcedure : ProcedureBase
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

            var pauseSeconds = outcome.Victory
                ? _stage.VictoryPauseSeconds
                : _stage.FailurePauseSeconds;
            await UniTask.Delay(
                TimeSpan.FromSeconds(pauseSeconds),
                cancellationToken: ct);
        }
    }
}
