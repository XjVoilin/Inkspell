using July.Arch;
using July.Audio;
using July.Localization;
using July.UI;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Inkspell 唯一主界面窗口。
    /// 协调装备、合成和战场子区域。
    /// </summary>
    public sealed class UIInkspellMainWindow : UIView
    {
        [Header("常驻状态")]
        [SerializeField] private UILocalizedText _stageText;
        [SerializeField] private UILocalizedText _magicInkText;
        [SerializeField] private UILocalizedText _pendingText;
        [SerializeField] private UILocalizedText _generationText;
        [SerializeField] private UIProgressBar _generationProgress;
        [SerializeField] private UILocalizedText _bookHealthText;
        [SerializeField] private UILocalizedText _bookShieldText;

        [Header("法术区")]
        [SerializeField] private UISpellBoardGameView _spellBoard;
        [SerializeField] private UIEquipmentBarGameView _equipmentBar;

        [Header("战场")]
        [SerializeField] private UIBattlefieldGameView _battlefield;

        private UIInkspellMainWindowData _data;
        private SpellAssetStore _spellAssets;
        private SpellSynthesisSystem _spellSynthesis;
        private ILocalizationSystem _localization;
        private IUISystem _ui;
        private IAudioSystem _audio;

        protected override void OnBeforeOpen()
        {
            _data = GetData<UIInkspellMainWindowData>() ?? new UIInkspellMainWindowData();
            _spellAssets = this.GetStore<SpellAssetStore>();
            _spellSynthesis = this.GetSystem<SpellSynthesisSystem>();
            _localization = this.GetSystem<ILocalizationSystem>();
            _ui = this.GetSystem<IUISystem>();
            _audio = this.GetSystem<IAudioSystem>();
            RenderStatus();
            RenderSpellViews();
            RenderBattlefield();
        }

        protected override void OnOpen()
        {
            this.Subscribe<SpellAssetsChangedEvent>(OnSpellAssetsChanged);
            this.Subscribe<SpellGenerationChangedEvent>(OnSpellGenerationChanged);
            this.Subscribe<StageProgressChangedEvent>(OnStageProgressChanged);
            this.Subscribe<BattleStateChangedEvent>(OnBattleStateChanged);
            this.Subscribe<BattleChallengeEndedEvent>(OnBattleChallengeEnded);
            this.Subscribe<SpellSynthesisRejectedEvent>(OnSpellSynthesisRejected);
            this.Subscribe<SpellSynthesisResolvedEvent>(OnSpellSynthesisResolved);
            _spellBoard.SpellClicked += OnSpellClicked;
            _spellBoard.SynthesisRequested += OnSynthesisRequested;
            _equipmentBar.EquipRequested += OnEquipRequested;
        }

        protected override void OnClose()
        {
            _spellBoard.SpellClicked -= OnSpellClicked;
            _spellBoard.SynthesisRequested -= OnSynthesisRequested;
            _equipmentBar.EquipRequested -= OnEquipRequested;
        }

        private void OnSpellAssetsChanged(SpellAssetsChangedEvent eventData)
        {
            _data.RefreshStatus();
            _data.RefreshSpellBoard();
            _data.RefreshEquipmentBar();
            _data.RefreshBattlefield();
            RenderStatus();
            RenderSpellViews();
            RenderBattlefield();
        }

        private void OnSpellGenerationChanged(SpellGenerationChangedEvent eventData)
        {
            RefreshAndRenderStatus();
        }

        private void OnStageProgressChanged(StageProgressChangedEvent eventData)
        {
            RefreshAndRenderStatus();
        }

        private void OnBattleStateChanged(BattleStateChangedEvent eventData)
        {
            _data.RefreshStatus();
            _data.RefreshBattlefield();
            RenderStatus();
            RenderBattlefield();
        }

        private void OnBattleChallengeEnded(BattleChallengeEndedEvent eventData)
        {
            // AutoBattleSystem 会先发布当帧最终状态，这里只播放一次结果反馈。
            _battlefield.PlayChallengeResult(eventData.Outcome.Victory);
        }

        private void RefreshAndRenderStatus()
        {
            _data.RefreshStatus();
            RenderStatus();
        }

        private void OnSynthesisRequested(long firstSpellId, long secondSpellId)
        {
            _spellSynthesis.TrySynthesize(firstSpellId, secondSpellId);
        }

        private void OnSpellClicked(long spellInstanceId)
        {
            _ui.Open(
                UIWindowID.UISpellDetailWindow,
                new UISpellDetailWindowData(spellInstanceId));
        }

        private void OnEquipRequested(long spellInstanceId, int equipmentSlot)
        {
            _audio.PlaySfx(
                _spellAssets.TryEquip(spellInstanceId, equipmentSlot)
                    ? "SfxUiEquip"
                    : "SfxUiInvalid",
                new SfxPlayOptions
                {
                    Group = "UI",
                    Volume = 0.52f,
                    Priority = 85,
                });
        }

        private void OnSpellSynthesisRejected(SpellSynthesisRejectedEvent eventData)
        {
            var key = eventData.Reason switch
            {
                SynthesisRejectReason.SpellNotFound => "SYNTHESIS_REJECT_NOT_FOUND",
                SynthesisRejectReason.SameInstance => "SYNTHESIS_REJECT_SAME_INSTANCE",
                SynthesisRejectReason.DifferentTier => "SYNTHESIS_REJECT_DIFFERENT_TIER",
                SynthesisRejectReason.Locked => "SYNTHESIS_REJECT_LOCKED",
                SynthesisRejectReason.Equipped => "SYNTHESIS_REJECT_EQUIPPED",
                SynthesisRejectReason.HighestTier => "SYNTHESIS_REJECT_HIGHEST_TIER",
                SynthesisRejectReason.Cultivated => "SYNTHESIS_REJECT_CULTIVATED",
                _ => throw new System.ArgumentOutOfRangeException(),
            };
            _ui.ShowTip(_localization.Get(key));
        }

        private void OnSpellSynthesisResolved(SpellSynthesisResolvedEvent eventData)
        {
            if (eventData.Kind == SynthesisOutcomeKind.HigherTierSpell)
            {
                _ui.ShowTip(_localization.Get("SYNTHESIS_RESULT_HIGHER_TIER"));
                return;
            }

            _ui.ShowTip(_localization.GetFormat(
                "SYNTHESIS_RESULT_MAGIC_INK",
                eventData.InkReward));
        }

        private void RenderStatus()
        {
            var status = _data.Status;
            _stageText.SetKey("MAIN_STAGE", status.CurrentStageId);
            _magicInkText.SetKey("MAIN_MAGIC_INK", status.MagicInk);
            _pendingText.SetKey("MAIN_PENDING", status.PendingSpellCount);
            _generationText.SetKey(
                "MAIN_GENERATION",
                status.GenerationProgressSeconds,
                status.GenerationIntervalSeconds);
            _generationProgress.SetValue(
                status.GenerationProgressSeconds,
                status.GenerationIntervalSeconds);
            _bookHealthText.SetKey(
                "MAIN_BOOK_HEALTH",
                status.BookHealth,
                status.BookMaxHealth);
            _bookShieldText.SetKey("MAIN_BOOK_SHIELD", status.BookShield);
        }

        private void RenderSpellViews()
        {
            _spellBoard.Render(_data.SpellBoard);
            _equipmentBar.Render(_data.EquipmentBar);
        }

        private void RenderBattlefield()
        {
            _battlefield.Render(_data.Battlefield);
        }
    }
}
