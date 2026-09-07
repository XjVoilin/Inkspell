using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Launch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Aot
{
    /// <summary>Scene-owned, bundled startup UI. Does not depend on hot-update UI or localization services.</summary>
    public sealed class LaunchPresentation : MonoBehaviour
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

        private void Awake()
        {
            _bookOrigin = _book.anchoredPosition;
            _moteOrigins = new Vector2[_motes.Length];
            for (var i = 0; i < _motes.Length; i++) _moteOrigins[i] = _motes[i].anchoredPosition;
        }

        public ILaunchStep Present(ILaunchStep step, int index, int count) =>
            new PresentedStep(this, step, index, count);

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

        private void Fail()
        {
            _failed = true;
            _status.text = _failure;
            _status.color = new Color(.48f, .19f, .13f);
        }

        private sealed class PresentedStep : ILaunchStep
        {
            private readonly LaunchPresentation _view;
            private readonly ILaunchStep _step;
            private readonly int _index;
            private readonly int _count;
            public string Name => _step.Name;

            public PresentedStep(LaunchPresentation view, ILaunchStep step, int index, int count)
            { _view = view; _step = step; _index = index; _count = count; }

            public async UniTask<bool> ExecuteAsync(CancellationToken ct)
            {
                _view._status.text = _view._stages[_index];
                _view._progress.fillAmount = (float)_index / _count;
                try
                {
                    // Render bundled art before potentially synchronous initialization starts.
                    if (_index == 0) await UniTask.NextFrame(ct);
                    if (!await _step.ExecuteAsync(ct))
                    { _view.Fail(); return false; }
                    _view._progress.fillAmount = (float)(_index + 1) / _count;
                    if (_index == _count - 1) await _view.FinishAsync(ct);
                    return true;
                }
                catch (OperationCanceledException) { throw; }
                catch { _view.Fail(); throw; }
            }
        }
    }
}
