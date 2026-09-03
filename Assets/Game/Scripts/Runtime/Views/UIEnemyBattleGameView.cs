using July.Arch;
using July.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 单个敌人的纯显示项；重复项不注册为单例 View。
    /// </summary>
    public sealed class UIEnemyBattleGameView : GameView
    {
        private const float HitFeedbackSeconds = 0.2f;
        private const float DeathFeedbackSeconds = 0.35f;

        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private UIProgressBar _healthProgress;
        [SerializeField] private Text _healthText;
        [SerializeField] private GameObject _slowIndicator;
        [SerializeField] private GameObject _hitFeedback;
        [SerializeField] private GameObject _deathFeedback;

        private float _hitFeedbackRemaining;
        private float _deathFeedbackRemaining;
        private float _lastHealth;

        internal EnemyBattleViewData Data { get; private set; }
        internal bool IsDying => _deathFeedbackRemaining > 0f;

        public void Render(EnemyBattleViewData data)
        {
            if (Data != null &&
                Data.RuntimeId == data.RuntimeId &&
                data.Health < _lastHealth)
            {
                PlayHit();
            }

            Data = data;
            _lastHealth = data.Health;
            gameObject.SetActive(true);
            _healthProgress.SetValue(data.Health, data.MaxHealth);
            _healthText.text = $"{data.Health:0}/{data.MaxHealth:0}";
            _slowIndicator.SetActive(data.IsSlowed);
        }

        internal void SetPathPosition(RectTransform pathRoot, float normalizedPosition)
        {
            var position = _rectTransform.anchoredPosition;
            position.x = Mathf.Lerp(
                pathRoot.rect.xMin,
                pathRoot.rect.xMax,
                normalizedPosition);
            _rectTransform.anchoredPosition = position;
        }

        internal void PlayDeath()
        {
            Data = null;
            _lastHealth = 0f;
            _slowIndicator.SetActive(false);
            _hitFeedback.SetActive(false);
            _hitFeedbackRemaining = 0f;
            _deathFeedback.SetActive(true);
            _deathFeedbackRemaining = DeathFeedbackSeconds;
        }

        internal void Clear()
        {
            Data = null;
            _lastHealth = 0f;
            _hitFeedbackRemaining = 0f;
            _deathFeedbackRemaining = 0f;
            _slowIndicator.SetActive(false);
            _hitFeedback.SetActive(false);
            _deathFeedback.SetActive(false);
            gameObject.SetActive(false);
        }

        protected override void OnViewAwake()
        {
            Clear();
        }

        private void Update()
        {
            if (_hitFeedbackRemaining > 0f)
            {
                _hitFeedbackRemaining -= Time.deltaTime;
                if (_hitFeedbackRemaining <= 0f)
                {
                    _hitFeedback.SetActive(false);
                }
            }

            if (_deathFeedbackRemaining <= 0f)
            {
                return;
            }

            _deathFeedbackRemaining -= Time.deltaTime;
            if (_deathFeedbackRemaining <= 0f)
            {
                _deathFeedback.SetActive(false);
                gameObject.SetActive(false);
            }
        }

        private void PlayHit()
        {
            _hitFeedback.SetActive(true);
            _hitFeedbackRemaining = HitFeedbackSeconds;
        }
    }
}
