using Cysharp.Threading.Tasks;
using July.Arch;
using July.UI;

namespace Game
{
    /// <summary>把已提交的离线收益事实转换为 UI 展示，不参与收益计算。</summary>
    public sealed class OfflineRewardPresentationSystem : SystemBase
    {
        private IUISystem _uiSystem;

        protected override UniTask OnInitializeAsync()
        {
            _uiSystem = GetSystem<IUISystem>();
            Subscribe<OfflineGenerationSettledEvent>(OnOfflineGenerationSettled);
            return UniTask.CompletedTask;
        }

        private void OnOfflineGenerationSettled(OfflineGenerationSettledEvent eventData)
        {
            if (eventData.Outcome.GeneratedCount <= 0)
            {
                return;
            }

            _uiSystem.Open(
                UIWindowID.UIOfflineRewardWindow,
                new UIOfflineRewardWindowData(eventData.Outcome));
        }
    }
}
