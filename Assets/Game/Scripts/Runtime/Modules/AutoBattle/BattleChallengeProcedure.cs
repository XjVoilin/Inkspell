using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;

namespace Game
{
    internal sealed class BattleChallengeProcedure : ProcedureBase
    {
        private readonly int _stageId;

        // Procedure 等待帧更新驱动的 AutoBattleSystem 回填最终结果。
        private readonly UniTaskCompletionSource<BattleOutcome> _completion = new();
        private BattleOutcome _outcome;
        private bool _hasOutcome;

        internal BattleChallengeProcedure(int stageId)
        {
            _stageId = stageId;
        }

        internal BattleOutcome Outcome => _hasOutcome
            ? _outcome
            : throw new InvalidOperationException("挑战尚未产生最终结果。");

        protected override async UniTask OnExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetSystem<AutoBattleSystem>().StartChallenge(this, _stageId);
            _outcome = await _completion.Task.AttachExternalCancellation(ct);
            _hasOutcome = true;
        }

        internal void Complete(BattleOutcome outcome)
        {
            _completion.TrySetResult(outcome);
        }

        internal void Fail(Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
