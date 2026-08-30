using System;
using System.Collections.Generic;
using cfg;
using July.Arch;
using July.Localization;
using July.UI;
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
        private const float SpellFeedbackSeconds = 0.3f;
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
        [SerializeField] private UIProgressBar[] _cooldownProgresses;
        [SerializeField] private Text[] _cooldownTexts;

        [Header("法术表现绑定入口")]
        [SerializeField] private RectTransform _fireballFeedback;
        [SerializeField] private RectTransform _chainLightningFeedback;
        [SerializeField] private RectTransform _frostRingFeedback;
        [SerializeField] private RectTransform _spellShieldFeedback;

        [Header("挑战反馈")]
        [SerializeField] private UILocalizedText _resultFeedback;
        [SerializeField] private UILocalizedText _retryFeedback;

        private readonly Dictionary<long, UIEnemyBattleGameView> _enemyViewsById = new();
        private readonly HashSet<long> _shownAttackIds = new();
        private readonly HashSet<long> _shownEffectIds = new();
        private readonly List<long> _removedEnemyIds = new();

        private bool _hasRenderedChallenge;
        private long _challengeId;
        private float _lastBookHealth;
        private float _lastBookShield;
        private bool _retryPending;
        private float _bookHitFeedbackRemaining;
        private float _shieldFeedbackRemaining;
        private float _fireballFeedbackRemaining;
        private float _chainLightningFeedbackRemaining;
        private float _frostRingFeedbackRemaining;
        private float _spellShieldFeedbackRemaining;
        private float _resultFeedbackRemaining;
        private float _retryFeedbackRemaining;

        public void Render(BattlefieldViewData data)
        {
            var challengeChanged = _hasRenderedChallenge && data.ChallengeId != _challengeId;
            if (challengeChanged)
            {
                if (_retryPending)
                {
                    PlayRetryFeedback();
                }

                ClearEnemyViews();
                _shownAttackIds.Clear();
                _shownEffectIds.Clear();
            }

            RenderBook(data, !challengeChanged && _hasRenderedChallenge);
            RenderEnemies(data.Enemies);
            RenderCooldowns(data.Cooldowns);
            RenderTransientFeedback(data.Attacks, data.Effects);

            _hasRenderedChallenge = true;
            _challengeId = data.ChallengeId;
            _lastBookHealth = data.BookHealth;
            _lastBookShield = data.BookShield;
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
            TickFeedback(_fireballFeedback.gameObject, ref _fireballFeedbackRemaining);
            TickFeedback(
                _chainLightningFeedback.gameObject,
                ref _chainLightningFeedbackRemaining);
            TickFeedback(_frostRingFeedback.gameObject, ref _frostRingFeedbackRemaining);
            TickFeedback(_spellShieldFeedback.gameObject, ref _spellShieldFeedbackRemaining);
            TickFeedback(_resultFeedback.gameObject, ref _resultFeedbackRemaining);
            TickFeedback(_retryFeedback.gameObject, ref _retryFeedbackRemaining);
        }

        private void RenderBook(BattlefieldViewData data, bool comparePrevious)
        {
            if (comparePrevious)
            {
                if (data.BookHealth < _lastBookHealth)
                {
                    Pulse(_bookHitFeedback, ref _bookHitFeedbackRemaining, BookFeedbackSeconds);
                }

                if (data.BookShield > _lastBookShield)
                {
                    Pulse(_shieldFeedback, ref _shieldFeedbackRemaining, BookFeedbackSeconds);
                }
                else if (data.BookShield < _lastBookShield)
                {
                    Pulse(_bookHitFeedback, ref _bookHitFeedbackRemaining, BookFeedbackSeconds);
                }
            }

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
                enemyView.PlayDeath();
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

        private void RenderCooldowns(IReadOnlyList<SpellCooldownViewData> cooldowns)
        {
            foreach (var progress in _cooldownProgresses)
            {
                progress.SetValue(0f, 0f);
            }

            foreach (var text in _cooldownTexts)
            {
                text.text = string.Empty;
            }

            foreach (var cooldown in cooldowns)
            {
                var slot = cooldown.EquipmentSlot;
                _cooldownProgresses[slot].SetValue(
                    cooldown.ReadyProgressSeconds,
                    cooldown.TotalSeconds);
                _cooldownTexts[slot].text = cooldown.RemainingSeconds.ToString("0.0");
            }
        }

        private void RenderTransientFeedback(
            IReadOnlyList<BattleAttackFeedbackViewData> attacks,
            IReadOnlyList<BattleEffectFeedbackViewData> effects)
        {
            foreach (var attack in attacks)
            {
                if (_shownAttackIds.Add(attack.AttackId))
                {
                    PlaySpellFeedback(attack.SpellType, attack.TargetPathNormalized);
                }
            }

            foreach (var effect in effects)
            {
                if (_shownEffectIds.Add(effect.EffectId))
                {
                    PlaySpellFeedback(effect.SpellType, effect.PathNormalized);
                }
            }
        }

        private void PlaySpellFeedback(SpellType spellType, float pathNormalized)
        {
            switch (spellType)
            {
                case SpellType.Fireball:
                    PulseSpell(
                        _fireballFeedback,
                        pathNormalized,
                        ref _fireballFeedbackRemaining);
                    break;
                case SpellType.ChainLightning:
                    PulseSpell(
                        _chainLightningFeedback,
                        pathNormalized,
                        ref _chainLightningFeedbackRemaining);
                    break;
                case SpellType.FrostRing:
                    PulseSpell(
                        _frostRingFeedback,
                        pathNormalized,
                        ref _frostRingFeedbackRemaining);
                    break;
                case SpellType.Shield:
                    PulseSpell(
                        _spellShieldFeedback,
                        pathNormalized,
                        ref _spellShieldFeedbackRemaining);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(spellType), spellType, null);
            }
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
            _shownAttackIds.Clear();
            _shownEffectIds.Clear();
            ClearEnemyViews();

            ResetFeedback(_bookHitFeedback, ref _bookHitFeedbackRemaining);
            ResetFeedback(_shieldFeedback, ref _shieldFeedbackRemaining);
            ResetFeedback(_fireballFeedback.gameObject, ref _fireballFeedbackRemaining);
            ResetFeedback(
                _chainLightningFeedback.gameObject,
                ref _chainLightningFeedbackRemaining);
            ResetFeedback(_frostRingFeedback.gameObject, ref _frostRingFeedbackRemaining);
            ResetFeedback(_spellShieldFeedback.gameObject, ref _spellShieldFeedbackRemaining);
            ResetFeedback(_resultFeedback.gameObject, ref _resultFeedbackRemaining);
            ResetFeedback(_retryFeedback.gameObject, ref _retryFeedbackRemaining);
        }

        private void PulseSpell(
            RectTransform effect,
            float pathNormalized,
            ref float remaining)
        {
            var position = effect.anchoredPosition;
            position.x = Mathf.Lerp(
                _enemyPathRoot.rect.xMin,
                _enemyPathRoot.rect.xMax,
                pathNormalized);
            effect.anchoredPosition = position;
            Pulse(effect.gameObject, ref remaining, SpellFeedbackSeconds);
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
