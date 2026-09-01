using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Scene;
using July.UI;

namespace Game
{
    internal sealed class EnterMainGameProcedure : ProcedureBase
    {
        protected override async UniTask OnExecuteAsync(CancellationToken ct)
        {
            await GetSystem<ISceneSystem>().SwitchSceneAsync("Main", ct);
            // 主窗口先订阅业务事件，随后离线结算产生的状态变化与奖励提示才能被正常呈现。
            await GetSystem<IUISystem>().OpenAsync(
                UIWindowID.UIInkspellMainWindow,
                new UIInkspellMainWindowData(),
                ct);
            GetSystem<SpellGenerationSystem>().SettleOffline();

            ct.ThrowIfCancellationRequested();
            GetSystem<StageProgressionSystem>().StartContinuousChallenges();
        }
    }
}
