using System;
using July.Arch;
using July.Bootstrap;
using July.Launch;
using UnityEngine;

namespace Game.Aot
{
    /// <summary>组装标准启动流程，业务模块仍由热更入口注册。</summary>
    public class GameEntry : BootstrapGameEntry
    {
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private LaunchPresentation _presentation;

        protected override void ConfigurePipeline(LaunchPipeline pipeline)
        {
            if (_presentation == null) throw new InvalidOperationException("GameEntry 未指定启动画面。");
            ArchContext.Current.RegisterStore(new LaunchStore(_gameConfig));

#if !JULYGF_DEBUG
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif

            Bootstrap.Configure(pipeline, _gameConfig.Bootstrap, _presentation,
                AOTGenericReferences.PatchedAOTAssemblyList);
        }

        protected override void OnShutdown()
        {
            base.OnShutdown();
            if (_presentation != null) Destroy(_presentation.gameObject);
        }
    }
}
