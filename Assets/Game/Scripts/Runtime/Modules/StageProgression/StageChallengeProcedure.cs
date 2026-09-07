using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;

namespace Game
{
    /// <summary>一次挑战、进度提交和结果停顿的异步先后顺序。</summary>
    internal sealed class StageChallengeProcedure : ProcedureBase
    {
        private readonly StageProgression _stage;

        internal StageChallengeProcedure(StageProgression stage)
        {
            _stage = stage;
        }

        protected override async UniTask OnExecuteAsync(CancellationToken ct)
        {
            var battle = GetSystem<AutoBattleSystem>();
            var outcome = await battle.RunChallengeAsync(_stage.StageId, ct);
            GetSystem<StageProgressionSystem>().CommitChallenge(outcome);

            var pauseSeconds = outcome.Victory
                ? _stage.VictoryPauseSeconds
                : _stage.FailurePauseSeconds;
            var resumeAt = battle.ForegroundElapsedSeconds + pauseSeconds;
            await UniTask.WaitUntil(
                () => battle.ForegroundElapsedSeconds >= resumeAt,
                cancellationToken: ct);
        }
    }
}
