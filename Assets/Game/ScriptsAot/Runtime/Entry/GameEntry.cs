using July.Arch;
using July.Launch;
using UnityEngine;

namespace Game.Aot
{
    /// <summary>AOT 启动入口，负责组装固定启动步骤并把 Unity 帧循环转交给架构上下文。</summary>
    public class GameEntry : JulyGameEntry
    {
        [SerializeField] private GameConfig _gameConfig = new();

        protected override void ConfigurePipeline(LaunchPipeline pipeline)
        {
            SeedServices.Register(_gameConfig);

#if !JULYGF_DEBUG
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif

            // 顺序不可交换：热更程序集依赖已初始化的资源系统，业务系统又依赖热更程序集。
            pipeline.Add(new InitializeAotSystemsStep());
            pipeline.Add(new InitializeResourceSystemStep());
            pipeline.Add(new HotUpdateStep());
            pipeline.Add(new InitializeGameSystemsStep());
            pipeline.Add(new LaunchGameStep());
        }

        private void Update()
        {
            if (!IsInitialized) return;
            ArchContext.Current.Update(Time.deltaTime);
        }

        protected override void OnDestroy()
        {
            SeedServices.Clear();
            base.OnDestroy();
        }
    }
}
