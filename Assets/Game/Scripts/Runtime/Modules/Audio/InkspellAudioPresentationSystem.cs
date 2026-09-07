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

        private IAudioSystem _audio;
        private AutoBattleSystem _battle;
        private SpellGenerationStore _generation;
        private long _battleRunId;
        private int _pendingCount;
        private int _hitVariant;
        private int _deathVariant;
        private int _bookHitVariant;
        private int _shieldHitVariant;
        private string _currentBgm;
        private bool _retryPending;

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
            Subscribe<BattleFactsEvent>(OnBattleFacts);
            Subscribe<BattleChallengeEndedEvent>(OnBattleChallengeEnded);

            SwitchBgm(MainBgm);
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            _audio?.StopBGM(0.25f);
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
            if (run == null)
            {
                _battleRunId = 0;
                _retryPending = false;
                SwitchBgm(MainBgm);
                return;
            }

            if (run.BattleRunId != _battleRunId)
            {
                BeginRun(run);
            }

        }

        private void BeginRun(BattleRun run)
        {
            _battleRunId = run.BattleRunId;
            SwitchBgm(run.StageId == 10 ? BossBgm : MainBgm);

            if (_retryPending)
            {
                _retryPending = false;
                Play("SfxRetry", "Stage", 0.54f, 75);
            }
        }

        private void OnBattleFacts(BattleFactsEvent eventData)
        {
            if (eventData.BattleRunId != _battleRunId)
                return; // 其他监听方可能已经同步取消或开始下一局。

            foreach (var fact in eventData.Facts)
            {
                switch (fact.Kind)
                {
                    case BattleFactKind.SpellCast:
                        PlayCast(fact.SpellType);
                        break;
                    case BattleFactKind.SpellImpact:
                        PlayImpact(fact);
                        break;
                    case BattleFactKind.EnemySpawned:
                        if (fact.EnemyType == EnemyType.ChapterBoss)
                            Play("SfxBossEnter", "Stage", 0.72f, 55);
                        break;
                    case BattleFactKind.EnemyDamaged:
                        _hitVariant = _hitVariant % 3 + 1;
                        Play($"SfxEnemyHit{_hitVariant}", "Battle", 0.28f, 155);
                        break;
                    case BattleFactKind.EnemyDied:
                        _deathVariant = _deathVariant % 2 + 1;
                        Play($"SfxEnemyDeath{_deathVariant}", "Battle", 0.42f, 135);
                        break;
                    case BattleFactKind.BookDamaged:
                        _bookHitVariant = _bookHitVariant % 2 + 1;
                        Play($"SfxBookHit{_bookHitVariant}", "Battle", 0.48f, 115);
                        break;
                    case BattleFactKind.ShieldAbsorbed:
                        _shieldHitVariant = _shieldHitVariant % 2 + 1;
                        Play($"SfxShieldAbsorb{_shieldHitVariant}", "Spell", 0.38f, 125);
                        break;
                    case BattleFactKind.ShieldBroken:
                        Play("SfxShieldBreak", "Spell", 0.54f, 95);
                        break;
                }
            }
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

        private void PlayImpact(BattleFact attack)
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
                    Play("SfxFrostImpact", "Spell", 0.48f, 105);
                    break;
                case SpellType.Shield:
                    // 护盾施放、吸收与破碎分别消费对应事实。
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

    }
}
