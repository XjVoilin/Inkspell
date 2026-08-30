using July.Localization;
using July.UI;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 展示一次已提交的离线生成结算。
    /// </summary>
    public sealed class UIOfflineRewardWindow : UIView
    {
        [Header("离线收益")]
        [SerializeField] private UILocalizedText _elapsedText;
        [SerializeField] private UILocalizedText _generatedText;
        [SerializeField] private UILocalizedText _transferredText;

        [Header("操作")]
        [SerializeField] private UISmartButton _continueButton;
        [SerializeField] private UILocalizedText _continueText;

        private UIOfflineRewardWindowData _data;

        protected override void OnBeforeOpen()
        {
            _data = GetData<UIOfflineRewardWindowData>() ?? new UIOfflineRewardWindowData();
            Render();
        }

        protected override void OnOpen()
        {
            _continueButton.onClick.AddListener(OnContinueClicked);
        }

        protected override void OnClose()
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        private void OnContinueClicked()
        {
            CloseWindow();
        }

        private void Render()
        {
            _elapsedText.SetKey("OFFLINE_REWARD_ELAPSED", _data.ElapsedSeconds);
            _generatedText.SetKey("OFFLINE_REWARD_GENERATED", _data.GeneratedCount);
            _transferredText.SetKey(
                "OFFLINE_REWARD_TRANSFERRED",
                _data.TransferredCount);
            _continueText.SetKey("OFFLINE_REWARD_CONTINUE");
        }
    }
}
