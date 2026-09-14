using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Aot
{
    /// <summary>Scene-owned, bundled startup UI. Does not depend on hot-update UI or localization services.</summary>
    public sealed class LaunchPresentation : MonoBehaviour, IBootstrapView
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _book;
        [SerializeField] private Image _halo;
        [SerializeField] private Image _progress;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private RectTransform[] _motes;
        [SerializeField] private string[] _stages;
        [SerializeField] private string _failure;
        private Vector2 _bookOrigin;
        private Vector2[] _moteOrigins;
        private float _elapsed;
        private bool _failed;
        private Button _actionButton;
        private TMP_Text _actionText;
        private Color _statusColor;
        private UniTaskCompletionSource<LaunchFailureAction> _pendingFailure;

        private void Awake()
        {
            _bookOrigin = _book.anchoredPosition;
            _moteOrigins = new Vector2[_motes.Length];
            for (var i = 0; i < _motes.Length; i++) _moteOrigins[i] = _motes[i].anchoredPosition;
            _statusColor = _status.color;
            CreateFailureControls();
        }

        public void SetStepInfo(int completed, int total)
        {
            _progress.fillAmount = (float)completed / total;
            // 表现按启动阶段分类；不再包装和重复维护 Bootstrap 的步骤列表。
            var stage = completed switch
            {
                <= 1 => 0,
                <= 4 => 1,
                5 => 2,
                <= 7 => 3,
                _ => 4,
            };
            _status.text = _stages[stage];
        }

        // 视图挂在 BootstrapGameEntry 的跨场景根节点下。
        public void PrepareForGame() { }

        public void Complete() => FinishAsync(this.GetCancellationTokenOnDestroy()).Forget();

        private void Update()
        {
            if (_failed) return;
            _elapsed += Time.unscaledDeltaTime;
            _book.anchoredPosition = _bookOrigin + Vector2.up * (Mathf.Sin(_elapsed * 1.6f) * 12f);
            _book.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_elapsed * .8f) * 1.5f);
            var tint = _halo.color;
            tint.a = .10f + .06f * (1 + Mathf.Sin(_elapsed * 1.7f));
            _halo.color = tint;
            for (var i = 0; i < _motes.Length; i++)
            {
                var phase = _elapsed * .6f + i * 1.7f;
                _motes[i].anchoredPosition = _moteOrigins[i] + new Vector2(Mathf.Sin(phase) * 9, Mathf.Cos(phase) * 18);
                _motes[i].localRotation = Quaternion.Euler(0, 0, 45 + _elapsed * (i % 2 == 0 ? 8 : -8));
            }
        }

        private async UniTask FinishAsync(CancellationToken ct)
        {
            _progress.fillAmount = 1;
            // Give the newly opened main window a layout/render frame before revealing it.
            await UniTask.NextFrame(ct);
            var elapsed = 0f;
            while (elapsed < .3f)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                _group.alpha = 1 - Mathf.Clamp01(elapsed / .3f);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            Destroy(gameObject);
        }

        public async UniTask<LaunchFailureAction> ShowFailureAsync(LaunchFailure failure, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var action = failure.CanRetry ? LaunchFailureAction.Retry : LaunchFailureAction.Restart;
            var completion = new UniTaskCompletionSource<LaunchFailureAction>();
            _pendingFailure = completion;
            _failed = true;
            _status.text = failure.CanRetry ? "书页未能展开，请检查网络后重试" : _failure;
            _status.color = new Color(.48f, .19f, .13f);
            _actionText.text = failure.CanRetry ? "重试" : "重新启动";
            _actionButton.onClick.AddListener(OnClick);
            _actionButton.gameObject.SetActive(true);
            using var cancellation = ct.Register(() => completion.TrySetCanceled(ct));
            try { return await completion.Task; }
            finally
            {
                _pendingFailure = null;
                // 退出时可能先销毁画面，再完成等待中的取消。
                if (_actionButton != null)
                {
                    _actionButton.onClick.RemoveListener(OnClick);
                    _actionButton.gameObject.SetActive(false);
                    _status.color = _statusColor;
                    _failed = false;
                }
            }
            void OnClick() => completion.TrySetResult(action);
        }

        private void OnDestroy() => _pendingFailure?.TrySetCanceled();

        private void CreateFailureControls()
        {
            var rect = (RectTransform)new GameObject("LaunchAction", typeof(RectTransform)).transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(360, 90);
            rect.anchoredPosition = new Vector2(0, -720);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.60f, .43f, .20f);
            _actionButton = rect.gameObject.AddComponent<Button>();
            _actionButton.targetGraphic = background;
            _actionText = Instantiate(_status, rect);
            _actionText.name = "Label";
            _actionText.rectTransform.anchoredPosition = Vector2.zero;
            _actionText.rectTransform.sizeDelta = rect.sizeDelta;
            _actionText.color = new Color(.93f, .87f, .73f);
            _actionButton.gameObject.SetActive(false);

            // 网络失败可发生在 UISystem 初始化之前，之后由 UISystem 接管。
            var events = new GameObject("LaunchEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
    }
}
