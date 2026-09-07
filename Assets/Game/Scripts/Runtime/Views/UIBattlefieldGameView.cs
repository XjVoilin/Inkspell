using System;
using System.Collections.Generic;
using cfg;
using July.Arch;
using July.Localization;
using July.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 主界面独立战场区域，只渲染 WindowData 并播放已提交业务事实。
    /// </summary>
    public sealed class UIBattlefieldGameView : GameView
    {
        private const float BookFeedbackSeconds = 0.25f;
        private const float ResultFeedbackSeconds = 1f;
        private const float RetryFeedbackSeconds = 0.8f;

        [Header("魔法书")]
        [SerializeField] private UIProgressBar _bookHealthProgress;
        [SerializeField] private UILocalizedText _bookHealthText;
        [SerializeField] private UIProgressBar _bookShieldProgress;
        [SerializeField] private UILocalizedText _bookShieldText;
        [SerializeField] private GameObject _bookHitFeedback;
        [SerializeField] private GameObject _shieldFeedback;

        [Header("敌人")]
        [SerializeField] private RectTransform _enemyPathRoot;
        [SerializeField] private UIEnemyBattleGameView[] _enemyViews;

        [Header("四槽冷却")]
        [SerializeField] private TMP_Text[] _cooldownTexts;

        [Header("法术表现绑定入口")]
        [SerializeField] private RectTransform _fireballFeedback;
        [SerializeField] private RectTransform _chainLightningFeedback;
        [SerializeField] private RectTransform _frostRingFeedback;
        [SerializeField] private RectTransform _spellShieldFeedback;

        [Header("挑战反馈")]
        [SerializeField] private UILocalizedText _resultFeedback;
        [SerializeField] private UILocalizedText _retryFeedback;

        private readonly Dictionary<long, UIEnemyBattleGameView> _enemyViewsById = new();
        private readonly List<long> _removedEnemyIds = new();

        private bool _hasRenderedChallenge;
        private long _battleRunId;
        private bool _retryPending;
        private float _bookHitFeedbackRemaining;
        private float _shieldFeedbackRemaining;
        private float _resultFeedbackRemaining;
        private float _retryFeedbackRemaining;

        private BattlePresentationEffects _effects;
        private RectTransform _bookArt;
        private Vector2 _bookRest;
        private float _presentationTime;
        private float _castKick;

        public void Render(BattlefieldViewData data)
        {
            if (data.BattleRunId == 0)
            {
                ResetPresentation();
                RenderBook(data);
                RenderCooldowns(data.Cooldowns);
                return;
            }

            var challengeChanged =
                _hasRenderedChallenge && data.BattleRunId != _battleRunId;
            if (challengeChanged)
            {
                if (_retryPending)
                {
                    PlayRetryFeedback();
                }

                ClearEnemyViews();
                _effects?.Clear();
            }

            RenderBook(data);
            RenderEnemies(data.Enemies);
            RenderCooldowns(data.Cooldowns);

            _hasRenderedChallenge = true;
            _battleRunId = data.BattleRunId;
        }

        public void PlayChallengeResult(bool victory)
        {
            _retryPending = !victory;
            _resultFeedback.SetKey(victory ? "BATTLE_VICTORY" : "BATTLE_DEFEAT");
            _resultFeedback.gameObject.SetActive(true);
            _resultFeedbackRemaining = ResultFeedbackSeconds;
        }

        protected override void OnViewAwake()
        {
            _bookArt = (RectTransform)_bookHitFeedback.transform.parent;
            _bookRest = _bookArt.anchoredPosition;
            ResetPresentation();
        }

        protected override void OnViewDisable()
        {
            ResetPresentation();
        }

        private void Update()
        {
            TickFeedback(_bookHitFeedback, ref _bookHitFeedbackRemaining);
            TickFeedback(_shieldFeedback, ref _shieldFeedbackRemaining);
            _effects?.Tick(Time.deltaTime);
            _presentationTime += Time.deltaTime;
            _castKick = Mathf.MoveTowards(_castKick, 0, Time.deltaTime * 5);
            if (_bookArt != null)
            {
                _bookArt.anchoredPosition = _bookRest + new Vector2(-_castKick * 12, Mathf.Sin(_presentationTime * 2) * 4);
                _bookArt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_presentationTime) * 1.5f - _castKick * 4);
            }
            TickFeedback(_resultFeedback.gameObject, ref _resultFeedbackRemaining);
            TickFeedback(_retryFeedback.gameObject, ref _retryFeedbackRemaining);
        }

        private void RenderBook(BattlefieldViewData data)
        {
            _bookHealthProgress.SetValue(data.BookHealth, data.BookMaxHealth);
            _bookHealthText.SetKey(
                "MAIN_BOOK_HEALTH",
                data.BookHealth,
                data.BookMaxHealth);
            _bookShieldProgress.SetValue(data.BookShield, data.BookShieldMaximum);
            _bookShieldText.SetKey(
                data.BookShield > 0f ? "BATTLE_SHIELD_ACTIVE" : "MAIN_BOOK_SHIELD",
                data.BookShield);
        }

        private void RenderEnemies(IReadOnlyList<EnemyBattleViewData> enemies)
        {
            _removedEnemyIds.Clear();
            foreach (var pair in _enemyViewsById)
            {
                if (!ContainsEnemy(enemies, pair.Key))
                {
                    _removedEnemyIds.Add(pair.Key);
                }
            }

            foreach (var runtimeId in _removedEnemyIds)
            {
                var enemyView = _enemyViewsById[runtimeId];
                _enemyViewsById.Remove(runtimeId);
                enemyView.Clear();
            }

            foreach (var enemy in enemies)
            {
                if (!_enemyViewsById.TryGetValue(enemy.RuntimeId, out var enemyView))
                {
                    enemyView = AcquireEnemyView();
                    _enemyViewsById.Add(enemy.RuntimeId, enemyView);
                }

                enemyView.SetPathPosition(_enemyPathRoot, enemy.PathNormalized);
                enemyView.Render(enemy);
            }
        }

        [SerializeField] private Image[] _cooldownCovers;

        private void RenderCooldowns(IReadOnlyList<SpellCooldownViewData> cooldowns)
        {
            for (var i = 0; i < _cooldownCovers.Length; i++)
            {
                SpellCooldownViewData state = null;
                foreach (var cooldown in cooldowns) if (cooldown.EquipmentSlot == i) { state = cooldown; break; }
                var remaining = state?.RemainingSeconds ?? 0;
                _cooldownCovers[i].fillAmount = state != null && state.TotalSeconds > 0 ? Mathf.Clamp01(remaining / state.TotalSeconds) : 0;
                if (remaining > .01f) _cooldownTexts[i].SetText("{0:1}", remaining);
                else _cooldownTexts[i].text = string.Empty;
            }
        }

        internal void PlayBookFeedback(BattleFactKind kind)
        {
            if (kind == BattleFactKind.ShieldApplied)
                Pulse(_shieldFeedback, ref _shieldFeedbackRemaining, BookFeedbackSeconds);
            else
                Pulse(_bookHitFeedback, ref _bookHitFeedbackRemaining, BookFeedbackSeconds);
        }

        internal void PlayEnemyFeedback(BattleFactKind kind, EnemyBattleViewData data, float damage = 0f)
        {
            if (!_enemyViewsById.TryGetValue(data.RuntimeId, out var view))
            {
                view = AcquireEnemyView();
                _enemyViewsById.Add(data.RuntimeId, view);
            }
            view.SetPathPosition(_enemyPathRoot, data.PathNormalized);
            view.Render(data);
            if (kind == BattleFactKind.EnemyDamaged)
                view.PlayHit(damage);
            else if (kind == BattleFactKind.EnemyDied)
            {
                _enemyViewsById.Remove(data.RuntimeId);
                view.PlayDeath();
            }
        }

        internal void PlaySpellFeedback(BattleFactKind kind, SpellType spellType, float pathNormalized, float travelSeconds)
        {
            _effects ??= new BattlePresentationEffects(_enemyPathRoot);
            var book = (RectTransform)_bookHitFeedback.transform.parent;
            var origin = (Vector2)_enemyPathRoot.InverseTransformPoint(book.TransformPoint(book.rect.center));
            var target = new Vector2(Mathf.Lerp(_enemyPathRoot.rect.xMin, _enemyPathRoot.rect.xMax, pathNormalized),
                _enemyPathRoot.rect.yMin + 90);
            var template = spellType switch
            {
                SpellType.Fireball => _fireballFeedback,
                SpellType.ChainLightning => _chainLightningFeedback,
                SpellType.FrostRing => _frostRingFeedback,
                SpellType.Shield => _spellShieldFeedback,
                _ => throw new ArgumentOutOfRangeException(nameof(spellType))
            };
            var impact = kind == BattleFactKind.SpellImpact;
            if (!impact) _castKick = 1;
            _effects.Play(spellType, template.GetComponent<Image>().sprite, origin, target, impact, travelSeconds);
        }

        private void PlayRetryFeedback()
        {
            _retryPending = false;
            _resultFeedback.gameObject.SetActive(false);
            _resultFeedbackRemaining = 0f;
            _retryFeedback.SetKey("BATTLE_RETRY");
            _retryFeedback.gameObject.SetActive(true);
            _retryFeedbackRemaining = RetryFeedbackSeconds;
        }

        private UIEnemyBattleGameView AcquireEnemyView()
        {
            foreach (var enemyView in _enemyViews)
            {
                if (enemyView.Data == null && !enemyView.IsDying)
                {
                    return enemyView;
                }
            }

            // A new wave can arrive while all spare views are still fading out.
            foreach (var enemyView in _enemyViews)
                if (enemyView.Data == null) { enemyView.Clear(); return enemyView; }
            throw new InvalidOperationException("战场敌人显示项数量不足。请补充 Prefab Inspector 绑定。");
        }

        private void ClearEnemyViews()
        {
            _enemyViewsById.Clear();
            foreach (var enemyView in _enemyViews)
            {
                enemyView.Clear();
            }
        }

        private void ResetPresentation()
        {
            _hasRenderedChallenge = false;
            _retryPending = false;
            ClearEnemyViews();

            ResetFeedback(_bookHitFeedback, ref _bookHitFeedbackRemaining);
            ResetFeedback(_shieldFeedback, ref _shieldFeedbackRemaining);
            _effects?.Clear();
            _castKick = 0;
            if (_bookArt != null)
            {
                _bookArt.anchoredPosition = _bookRest;
                _bookArt.localRotation = Quaternion.identity;
            }
            _fireballFeedback.gameObject.SetActive(false);
            _chainLightningFeedback.gameObject.SetActive(false);
            _frostRingFeedback.gameObject.SetActive(false);
            _spellShieldFeedback.gameObject.SetActive(false);
            ResetFeedback(_resultFeedback.gameObject, ref _resultFeedbackRemaining);
            ResetFeedback(_retryFeedback.gameObject, ref _retryFeedbackRemaining);
        }

        private static bool ContainsEnemy(
            IReadOnlyList<EnemyBattleViewData> enemies,
            long runtimeId)
        {
            for (var index = 0; index < enemies.Count; index++)
            {
                if (enemies[index].RuntimeId == runtimeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Pulse(GameObject target, ref float remaining, float duration)
        {
            target.SetActive(true);
            remaining = duration;
        }

        private static void TickFeedback(GameObject target, ref float remaining)
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                target.SetActive(false);
            }
        }

        private static void ResetFeedback(GameObject target, ref float remaining)
        {
            remaining = 0f;
            target.SetActive(false);
        }
    }
}
