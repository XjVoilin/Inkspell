using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using Game.Aot;
using July.Arch;
using July.Audio;
using July.Launch;
using July.Config;
using July.Fsm;
using July.Input;
using July.Localization;
using July.Logging;
using July.Persistence;
using July.Pooling;
using July.Resource;
using July.Scene;
using July.Time;
using July.UI;
using SimpleJSON;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 热更层的组合根：集中注册业务系统、状态仓库及其持久化边界。
    /// </summary>
    public sealed class HotUpdateRegistrar :
        IHotUpdateRegistrar,
        ICanGetSystem,
        ICanRunProcedure
    {
        public void Register()
        {
            var context = ArchContext.Current;
            var gameConfig = SeedServices.Resolve<GameConfig>();
            context.RegisterSystem(new PoolSystem());

            var uiSystem = new UISystem();
            uiSystem.Configure(gameConfig.UI);
            uiSystem.ConfigureTip(gameConfig.Tip);
            context.RegisterSystem(uiSystem);

            var audioSystem = new AudioSystem();
            audioSystem.Configure(gameConfig.Audio);
            context.RegisterSystem(audioSystem);

            context.RegisterSystem(new ConfigSystem());
            context.RegisterSystem(new JsonSerializeSystem());
            context.RegisterSystem(new NoEncryptionSystem());
            var saveSystem = new LocalFileSaveSystem();
            context.RegisterSystem(saveSystem);
            context.RegisterSystem(new SceneSystem());
            context.RegisterSystem(new UnityInputSystem());
            context.RegisterSystem(new FsmSystem());
            context.RegisterSystem(new TimeSystem());
            context.RegisterSystem(new LocalizationSystem());

            context.RegisterStore(saveSystem.Persist(
                new SpellAssetStore(),
                "inkspell.spell-assets",
                SaveImportance.Important));
            context.RegisterStore(saveSystem.Persist(
                new StageProgressionStore(),
                "inkspell.stage-progression",
                SaveImportance.Important));
            context.RegisterStore(saveSystem.Persist(
                new SpellGenerationStore(),
                "inkspell.spell-generation",
                SaveImportance.Important));
            
            context.RegisterSystem(new SpellAssetSystem());
            context.RegisterSystem(new SpellSynthesisSystem());
            context.RegisterSystem(new SpellProgressionSystem());
            context.RegisterSystem(new AutoBattleSystem());
            context.RegisterSystem(new StageProgressionSystem());
            context.RegisterSystem(new SpellGenerationSystem());
            context.RegisterSystem(new OfflineRewardPresentationSystem());
            context.RegisterSystem(new InkspellAudioPresentationSystem());
        }

        public async UniTask PreInitializeAsync(CancellationToken ct = default)
        {
            await LoadLubanTablesAsync(ct);
            SetupLocalization();
        }
        
        private void SetupLocalization()
        {
            var config = this.GetSystem<IConfigSystem>();
            this.GetSystem<ILocalizationSystem>().SetMainProvider(new LubanLocalizationProvider(config));
        }
        
        private async UniTask LoadLubanTablesAsync(CancellationToken ct)
        {
            var resource = this.GetSystem<IResourceSystem>();
            var names = Tables.TableNames;
            var jsonCache = new Dictionary<string, string>(names.Length);
            var tasks = new UniTask<(string name, string json)>[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                tasks[i] = LoadSingleJsonAsync(resource, name);
            }

            // 表文件并行读取，全部到齐后再一次性构造 Tables，防止系统看到不完整配置。
            var results = await UniTask.WhenAll(tasks);
            foreach (var (name, json) in results)
                jsonCache[name] = json;

            var tables = new Tables(name => jsonCache.TryGetValue(name, out var json)
                ? JSON.Parse(json)
                : throw new Exception($"配置未找到: {name}"));

            var tableDict = new Dictionary<Type, object>();
            tableDict[typeof(Tables)] = tables;
            tables.RegisterTo(tableDict);

            this.GetSystem<IConfigSystem>().SetMainProvider(new DictionaryConfigProvider(tableDict));

            JLogger.Log($"[HotUpdateRegistrar] Luban 配置表加载完成，共 {names.Length} 张表");
        }

        private static async UniTask<(string name, string json)> LoadSingleJsonAsync(
            IResourceSystem resource, string name)
        {
            using var handle = await resource.LoadAssetAsync<TextAsset>(name);
            if (handle?.Asset == null)
                throw new Exception($"配置文件未找到: {name}");
            return (name, handle.Asset.text);
        }

        public async UniTask OnGameLaunch()
        {
            this.GetSystem<IUISystem>().SetMainProvider(new LubanUIWindowProvider());
            await this.RunProcedure(new EnterMainGameProcedure());
        }
    }
}
