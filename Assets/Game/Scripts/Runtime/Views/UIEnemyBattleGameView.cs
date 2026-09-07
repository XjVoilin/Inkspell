using System;
using cfg;
using July.Arch;
using July.UI;
using TMPro;
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
        [SerializeField] private Image _art;
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _swiftSprite;
        [SerializeField] private Sprite _eliteSprite;
        [SerializeField] private Sprite _bossSprite;
        [SerializeField] private UIProgressBar _healthProgress;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private GameObject _slowIndicator;
        [SerializeField] private GameObject _hitFeedback;
        [SerializeField] private GameObject _deathFeedback;

        private float _hitFeedbackRemaining;
        private float _deathFeedbackRemaining;
        private Vector2 _artRest;
        private bool _capturedRest;
        private float _motionTime;
        private TMP_Text _damageNumber;
        private float _damageRemaining;
        private float _damageTotal;

        internal EnemyBattleViewData Data { get; private set; }
        internal bool IsDying => _deathFeedbackRemaining > 0f;

        public void Render(EnemyBattleViewData data)
        {
            if (!_capturedRest) { _artRest = _art.rectTransform.anchoredPosition; _capturedRest = true; }
            Data = data;
            _art.sprite = data.Type switch
            {
                EnemyType.NormalInkling => _normalSprite,
                EnemyType.SwiftInkling => _swiftSprite,
                EnemyType.ThickInkElite => _eliteSprite,
                EnemyType.ChapterBoss => _bossSprite,
                _ => throw new ArgumentOutOfRangeException(nameof(data.Type), data.Type, null),
            };
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
            _slowIndicator.SetActive(false);
            _hitFeedback.SetActive(false);
            _hitFeedbackRemaining = 0f;
            _deathFeedback.SetActive(true);
            var deathArt = _deathFeedback.GetComponent<Image>();
            deathArt.sprite = _art.sprite;
            deathArt.color = new Color(.12f, .1f, .08f, .4f);
            _deathFeedbackRemaining = DeathFeedbackSeconds;
            _healthProgress.gameObject.SetActive(false);
            _healthText.gameObject.SetActive(false);
        }

        internal void Clear()
        {
            Data = null;
            _hitFeedbackRemaining = 0f;
            _deathFeedbackRemaining = 0f;
            _slowIndicator.SetActive(false);
            _hitFeedback.SetActive(false);
            _deathFeedback.SetActive(false);
            _art.color = Color.white;
            _art.rectTransform.localScale = Vector3.one;
            _art.rectTransform.localRotation = Quaternion.identity;
            if (_capturedRest) _art.rectTransform.anchoredPosition = _artRest;
            _healthProgress.gameObject.SetActive(true);
            _healthText.gameObject.SetActive(true);
            _damageRemaining = 0;
            _damageTotal = 0;
            if (_damageNumber != null) _damageNumber.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            _motionTime += Time.deltaTime;
            if (_damageRemaining > 0 && _damageNumber != null)
            {
                _damageRemaining -= Time.deltaTime;
                var t = 1 - Mathf.Clamp01(_damageRemaining / .65f);
                _damageNumber.rectTransform.anchoredPosition = new Vector2(0, 245 + 45 * t);
                _damageNumber.color = new Color(.95f, .52f, .1f, 1 - t * t);
                _damageNumber.gameObject.SetActive(_damageRemaining > 0);
            }
            var hit = Mathf.Clamp01(_hitFeedbackRemaining / HitFeedbackSeconds);
            var stride = Mathf.Sin(_motionTime * (Data != null && Data.IsSlowed ? 4 : 9) + (Data?.RuntimeId ?? 0));
            if (!IsDying)
            {
                _art.rectTransform.anchoredPosition = _artRest + new Vector2(hit * 15, Mathf.Abs(stride) * 5);
                _art.rectTransform.localRotation = Quaternion.Euler(0, 0, stride * 3 - hit * 8);
                _art.rectTransform.localScale = new Vector3(1 + hit * .14f, 1 - hit * .10f, 1);
                _art.color = Color.Lerp(Color.white, new Color(1, .45f, .3f), hit);
            }
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
            var death = 1 - Mathf.Clamp01(_deathFeedbackRemaining / DeathFeedbackSeconds);
            _art.color = new Color(.3f, .25f, .22f, 1 - death);
            _art.rectTransform.localRotation = Quaternion.Euler(0, 0, -death * 65);
            _art.rectTransform.localScale = Vector3.one * (1 - death * .65f);
            _art.rectTransform.anchoredPosition = _artRest + new Vector2(20 * death, -25 * death);
            _deathFeedback.GetComponent<Image>().color = new Color(.12f, .1f, .08f, .4f * (1 - death));
            if (_deathFeedbackRemaining <= 0f)
            {
                Clear();
            }
        }

        internal void PlayHit(float damage = 0f)
        {
            _hitFeedback.SetActive(true);
            _hitFeedbackRemaining = HitFeedbackSeconds;
            var hitArt = _hitFeedback.GetComponent<Image>();
            hitArt.sprite = _art.sprite;
            hitArt.preserveAspect = true;
            hitArt.color = new Color(1, .75f, .35f, .35f);
            if (damage <= 0) return;
            if (_damageNumber == null)
            {
                _damageNumber = Instantiate(_healthText, transform);
                _damageNumber.name = "DamageNumber";
                _damageNumber.fontSize = 30;
                _damageNumber.fontStyle = FontStyles.Bold;
                _damageNumber.raycastTarget = false;
                _damageNumber.rectTransform.anchorMin = _damageNumber.rectTransform.anchorMax = new Vector2(.5f, 0);
                _damageNumber.rectTransform.sizeDelta = new Vector2(180, 50);
            }
            _damageTotal = _damageRemaining > .35f ? _damageTotal + damage : damage;
            _damageNumber.text = $"-{_damageTotal:0.#}";
            _damageRemaining = .65f;
            _damageNumber.gameObject.SetActive(true);
        }
    }
}
