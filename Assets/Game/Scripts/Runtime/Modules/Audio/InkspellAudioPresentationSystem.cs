using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Audio;

namespace Game
{
    /// <summary>把已经提交的游戏事实映射为一次性音频反馈。</summary>
    internal sealed class InkspellAudioPresentationSystem : SystemBase
    {
        private const string MainBgm = "BgmMainLoop";
        private const string BossBgm = "BgmBossLoop";

        private readonly Dictionary<long, AttackAudioState> _activeAttacks = new();
        private readonly HashSet<long> _seenEffects = new();
        private readonly Dictionary<long, float> _enemyHealth = new();
        private readonly List<long> _removedIds = new();

        private IAudioSystem _audio;
        private AutoBattleSystem _battle;
        private SpellGenerationStore _generation;
        private long _battleRunId;
        private float _bookHealth;
        private float _bookShield;
        private int _pendingCount;
        private int _hitVariant;
        private int _deathVariant;
        private int _bookHitVariant;
        private int _shieldHitVariant;
        private string _currentBgm;
        private bool _retryPending;

        private readonly struct AttackAudioState
        {
            internal AttackAudioState(SpellType spellType, int targetCount)
            {
                SpellType = spellType;
                TargetCount = targetCount;
            }

            internal SpellType SpellType { get; }
            internal int TargetCount { get; }
        }

        protected override UniTask OnInitializeAsync()
        {
            _audio = GetSystem<IAudioSystem>();
            _battle = GetSystem<AutoBattleSystem>();
            _generation = GetStore<SpellGenerationStore>();
            _pendingCount = _generation.PendingCount;

            Subscribe<SpellGenerationChangedEvent>(OnSpellGenerationChanged);
            Subscribe<OfflineGenerationSettledEvent>(OnOfflineGenerationSettled);
            Subscribe<SpellSynthesisResolvedEvent>(OnSpellSynthesisResolved);
            Subscribe<SpellSynthesisRejectedEvent>(OnSpellSynthesisRejected);
            Subscribe<SpellUpgradedEvent>(OnSpellUpgraded);
            Subscribe<SpellUpgradeRejectedEvent>(OnSpellUpgradeRejected);
            Subscribe<BattleStateChangedEvent>(OnBattleStateChanged);
            Subscribe<BattleChallengeEndedEvent>(OnBattleChallengeEnded);

            SwitchBgm(MainBgm);
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            _audio?.StopBGM(0.25f);
            _activeAttacks.Clear();
            _seenEffects.Clear();
            _enemyHealth.Clear();
            _removedIds.Clear();
        }

        private void OnSpellGenerationChanged(SpellGenerationChangedEvent eventData)
        {
            var current = _generation.PendingCount;
            if (current > _pendingCount)
            {
                Play("SfxSpellGenerated", "System", 0.45f, 150);
            }

            _pendingCount = current;
        }

        private void OnOfflineGenerationSettled(OfflineGenerationSettledEvent eventData)
        {
            if (eventData.Outcome.GeneratedCount > 0)
            {
                Play("SfxOfflineReward", "System", 0.60f, 90);
            }
        }

        private void OnSpellSynthesisResolved(SpellSynthesisResolvedEvent eventData)
        {
            Play(
                eventData.Kind == SynthesisOutcomeKind.HigherTierSpell
                    ? "SfxSynthesisSuccess"
                    : "SfxSynthesisInk",
                "Synthesis",
                0.72f,
                70);
        }

        private void OnSpellSynthesisRejected(SpellSynthesisRejectedEvent eventData)
        {
            Play("SfxUiInvalid", "UI", 0.50f, 80);
        }

        private void OnSpellUpgraded(SpellUpgradedEvent eventData)
        {
            Play("SfxUpgradeSuccess", "System", 0.68f, 75);
        }

        private void OnSpellUpgradeRejected(SpellUpgradeRejectedEvent eventData)
        {
            Play("SfxUiInvalid", "UI", 0.50f, 80);
        }

        private void OnBattleChallengeEnded(BattleChallengeEndedEvent eventData)
        {
            _retryPending = !eventData.Outcome.Victory;
            Play(
                eventData.Outcome.Victory ? "SfxVictory" : "SfxDefeat",
                "Stage",
                eventData.Outcome.Victory ? 0.66f : 0.58f,
                60);
        }

        private void OnBattleStateChanged(BattleStateChangedEvent eventData)
        {
            var run = _battle.CurrentRun;
            if (run.BattleRunId != _battleRunId)
            {
                BeginRun(run);
            }

            RenderAttacks(run);
            RenderEffects(run);
            RenderEnemies(run);
            RenderBook(run);
        }

        private void BeginRun(IReadOnlyBattleRun run)
        {
            _battleRunId = run.BattleRunId;
            _activeAttacks.Clear();
            _seenEffects.Clear();
            _enemyHealth.Clear();
            _bookHealth = run.Book.Health;
            _bookShield = run.Book.Shield;
            SwitchBgm(run.StageId == 10 ? BossBgm : MainBgm);

            if (_retryPending)
            {
                _retryPending = false;
                Play("SfxRetry", "Stage", 0.54f, 75);
            }
        }

        private void RenderAttacks(IReadOnlyBattleRun run)
        {
            _removedIds.Clear();
            foreach (var pair in _activeAttacks)
            {
                if (!ContainsAttack(run.Attacks, pair.Key))
                {
                    _removedIds.Add(pair.Key);
                }
            }

            foreach (var attack in run.Attacks)
            {
                if (_activeAttacks.ContainsKey(attack.AttackId))
                {
                    continue;
                }

                _activeAttacks.Add(
                    attack.AttackId,
                    new AttackAudioState(attack.SpellType, attack.TargetEnemyIds.Count));
                PlayCast(attack.SpellType);
            }

            foreach (var attackId in _removedIds)
            {
                var attack = _activeAttacks[attackId];
                _activeAttacks.Remove(attackId);
                PlayImpact(attack);
            }
        }

        private void RenderEffects(IReadOnlyBattleRun run)
        {
            foreach (var effect in run.Effects)
            {
                if (!_seenEffects.Add(effect.EffectId))
                {
                    continue;
                }

                if (effect.SpellType == SpellType.FrostRing)
                {
                    Play("SfxFrostImpact", "Spell", 0.48f, 105);
                }
            }
        }

        private void RenderEnemies(IReadOnlyBattleRun run)
        {
            _removedIds.Clear();
            foreach (var pair in _enemyHealth)
            {
                if (!ContainsEnemy(run.Enemies.Items, pair.Key))
                {
                    _removedIds.Add(pair.Key);
                }
            }

            var damaged = false;
            foreach (var enemy in run.Enemies.Items)
            {
                if (!_enemyHealth.TryGetValue(enemy.RuntimeId, out var previous))
                {
                    _enemyHealth.Add(enemy.RuntimeId, enemy.Health);
                    if (enemy.Type == EnemyType.ChapterBoss)
                    {
                        Play("SfxBossEnter", "Stage", 0.72f, 55);
                    }

                    continue;
                }

                if (enemy.Health < previous)
                {
                    damaged = true;
                }

                _enemyHealth[enemy.RuntimeId] = enemy.Health;
            }

            if (damaged)
            {
                _hitVariant = _hitVariant % 3 + 1;
                Play($"SfxEnemyHit{_hitVariant}", "Battle", 0.28f, 155);
            }

            if (_removedIds.Count > 0)
            {
                _deathVariant = _deathVariant % 2 + 1;
                Play($"SfxEnemyDeath{_deathVariant}", "Battle", 0.42f, 135);
                foreach (var runtimeId in _removedIds)
                {
                    _enemyHealth.Remove(runtimeId);
                }
            }
        }

        private void RenderBook(IReadOnlyBattleRun run)
        {
            if (run.Book.Health < _bookHealth)
            {
                _bookHitVariant = _bookHitVariant % 2 + 1;
                Play($"SfxBookHit{_bookHitVariant}", "Battle", 0.48f, 115);
            }

            if (run.Book.Shield < _bookShield)
            {
                if (_bookShield > 0f && run.Book.Shield <= 0f)
                {
                    Play("SfxShieldBreak", "Spell", 0.54f, 95);
                }
                else
                {
                    _shieldHitVariant = _shieldHitVariant % 2 + 1;
                    Play($"SfxShieldAbsorb{_shieldHitVariant}", "Spell", 0.38f, 125);
                }
            }

            _bookHealth = run.Book.Health;
            _bookShield = run.Book.Shield;
        }

        private void PlayCast(SpellType spellType)
        {
            var address = spellType switch
            {
                SpellType.Fireball => "SfxFireballCast",
                SpellType.ChainLightning => "SfxChainCast",
                SpellType.FrostRing => "SfxFrostCast",
                SpellType.Shield => "SfxShieldCast",
                _ => null,
            };
            Play(address, "Spell", 0.42f, 135);
        }

        private void PlayImpact(AttackAudioState attack)
        {
            switch (attack.SpellType)
            {
                case SpellType.Fireball:
                    _hitVariant = _hitVariant % 2 + 1;
                    Play($"SfxFireballImpact{_hitVariant}", "Spell", 0.50f, 120);
                    break;
                case SpellType.ChainLightning:
                    Play("SfxChainJump1", "Spell", 0.44f, 120);
                    if (attack.TargetCount > 2)
                    {
                        Play("SfxChainJump2", "Spell", 0.38f, 125, 0.08f);
                    }
                    break;
                case SpellType.FrostRing:
                    // Frost 的持续效果 ID 是更准确的命中事实，避免重复播放。
                    break;
                case SpellType.Shield:
                    // 护盾施放音在攻击提交时播放，吸收与破碎由数值变化播放。
                    break;
            }
        }

        private void SwitchBgm(string address)
        {
            if (_currentBgm == address)
            {
                return;
            }

            _currentBgm = address;
            _audio.PlayBGM(address, new BGMPlayOptions
            {
                Loop = true,
                Volume = 0.42f,
                FadeInDuration = 0.8f,
                FadeOutDuration = 0.5f,
            });
        }

        private void Play(
            string address,
            string group,
            float volume,
            int priority,
            float delay = 0f)
        {
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            _audio.PlaySfx(address, new SfxPlayOptions
            {
                Group = group,
                Volume = volume,
                Priority = priority,
                Delay = delay,
            });
        }

        private static bool ContainsAttack(
            IReadOnlyList<IReadOnlyBattleAttack> attacks,
            long attackId)
        {
            for (var index = 0; index < attacks.Count; index++)
            {
                if (attacks[index].AttackId == attackId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsEnemy(
            IReadOnlyList<IReadOnlyBattleEnemy> enemies,
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
    }
}
