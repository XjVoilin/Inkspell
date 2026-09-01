using System.Threading;
using Cysharp.Threading.Tasks;
using July.Launch;

namespace Game.Aot
{
    /// <summary>启动管线最后一步：把控制权交给已加载的热更业务入口。</summary>
    public sealed class LaunchGameStep : ILaunchStep
    {
        public string Name => "Launch Game";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await SeedServices.Resolve<IHotUpdateRegistrar>().OnGameLaunch();
            return true;
        }
    }
}
