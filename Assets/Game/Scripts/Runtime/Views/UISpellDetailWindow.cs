using July.Arch;
using July.Localization;
using July.UI;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 单个法术实例的详情、锁定与升级窗口。
    /// </summary>
    public sealed class UISpellDetailWindow : UIView
    {
        [Header("详情内容")]
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private UILocalizedText _spellNameText;
        [SerializeField] private UILocalizedText _tierLevelText;
        [SerializeField] private UILocalizedText _locationText;
        [SerializeField] private UILocalizedText _mainValueText;
        [SerializeField] private UILocalizedText _powerMultiplierText;
        [SerializeField] private UILocalizedText _magicInkText;
        [SerializeField] private UILocalizedText _upgradeCostText;

        [Header("操作")]
        [SerializeField] private UIToggleButton _lockButton;
        [SerializeField] private UILocalizedText _lockActionText;
        [SerializeField] private UISmartButtonGray _upgradeButton;
        [SerializeField] private UILocalizedText _upgradeActionText;

        private UISpellDetailWindowData _data;
        private SpellAssetSystem _spellAssets;
        private SpellProgressionSystem _spellProgression;
        private ILocalizationSystem _localization;
        private IUISystem _ui;

        protected override void OnBeforeOpen()
        {
            _data = GetData<UISpellDetailWindowData>() ?? new UISpellDetailWindowData();
            _spellAssets = this.GetSystem<SpellAssetSystem>();
            _spellProgression = this.GetSystem<SpellProgressionSystem>();
            _localization = this.GetSystem<ILocalizationSystem>();
            _ui = this.GetSystem<IUISystem>();
            Render();
        }

        protected override void OnOpen()
        {
            this.Subscribe<SpellAssetsChangedEvent>(OnSpellAssetsChanged);
            this.Subscribe<SpellUpgradeRejectedEvent>(OnSpellUpgradeRejected);
            this.Subscribe<SpellUpgradedEvent>(OnSpellUpgraded);
            _lockButton.OnValueChanged.AddListener(OnLockChanged);
            _upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        protected override void OnClose()
        {
            _lockButton.OnValueChanged.RemoveListener(OnLockChanged);
            _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        private void OnLockChanged(bool locked)
        {
            if (!_spellAssets.TrySetLocked(_data.SpellInstanceId, locked))
            {
                throw new System.InvalidOperationException(
                    $"详情窗口锁定已存在法术失败: instanceId={_data.SpellInstanceId}, locked={locked}");
            }

            _ui.ShowTip(_localization.Get(
                locked ? "SPELL_DETAIL_LOCKED" : "SPELL_DETAIL_UNLOCKED"));
        }

        private void OnUpgradeClicked()
        {
            _spellProgression.TryUpgrade(_data.SpellInstanceId);
        }

        private void OnSpellAssetsChanged(SpellAssetsChangedEvent eventData)
        {
            RefreshAndRender();
        }

        private void OnSpellUpgradeRejected(SpellUpgradeRejectedEvent eventData)
        {
            if (eventData.InstanceId != _data.SpellInstanceId)
            {
                return;
            }

            RefreshAndRender();
            switch (eventData.Reason)
            {
                case SpellUpgradeRejectReason.InsufficientInk:
                    _ui.ShowTip(_localization.GetFormat(
                        "SPELL_DETAIL_INSUFFICIENT_INK",
                        _data.InkCost,
                        _data.MissingInk));
                    break;
                case SpellUpgradeRejectReason.MaxLevel:
                    _ui.ShowTip(_localization.Get("SPELL_DETAIL_MAX_LEVEL"));
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException();
            }
        }

        private void OnSpellUpgraded(SpellUpgradedEvent eventData)
        {
            if (eventData.Upgrade.InstanceId != _data.SpellInstanceId)
            {
                return;
            }

            RefreshAndRender();
            _ui.ShowTip(_localization.GetFormat(
                "SPELL_DETAIL_UPGRADED",
                eventData.Upgrade.CurrentLevel,
                eventData.Upgrade.NextLevel,
                eventData.Upgrade.CurrentPowerMultiplier,
                eventData.Upgrade.NextPowerMultiplier,
                eventData.Upgrade.InkCost));
        }

        private void RefreshAndRender()
        {
            _data.Refresh();
            Render();
        }

        private void Render()
        {
            var hasSpell = _data.SpellInstanceId != 0;
            _contentRoot.SetActive(hasSpell);
            _lockButton.interactable = hasSpell;
            _upgradeButton.SetInteractable(hasSpell && !_data.IsMaxLevel);
            if (!hasSpell)
            {
                return;
            }

            _spellNameText.SetKey(_data.DisplayNameKey);
            _tierLevelText.SetKey(
                "SPELL_DETAIL_TIER_LEVEL",
                _localization.Get(_data.TierDisplayKey),
                _data.Level);
            if (_data.Location == SpellLocation.EquipmentSlot)
            {
                _locationText.SetKey(
                    "SPELL_DETAIL_LOCATION_EQUIPPED",
                    _data.EquipmentSlot + 1);
            }
            else
            {
                _locationText.SetKey("SPELL_DETAIL_LOCATION_CRAFTING");
            }

            var mainValueName = _localization.Get(_data.MainValueNameKey);
            if (_data.IsMaxLevel)
            {
                _mainValueText.SetKey(
                    "SPELL_DETAIL_MAIN_VALUE_CURRENT",
                    mainValueName,
                    _data.CurrentMainValue);
                _powerMultiplierText.SetKey(
                    "SPELL_DETAIL_MULTIPLIER_CURRENT",
                    _data.CurrentPowerMultiplier);
                _upgradeCostText.SetKey("SPELL_DETAIL_MAX_LEVEL");
            }
            else
            {
                _mainValueText.SetKey(
                    "SPELL_DETAIL_MAIN_VALUE_CHANGE",
                    mainValueName,
                    _data.CurrentMainValue,
                    _data.NextMainValue);
                _powerMultiplierText.SetKey(
                    "SPELL_DETAIL_MULTIPLIER_CHANGE",
                    _data.CurrentPowerMultiplier,
                    _data.NextPowerMultiplier);
                _upgradeCostText.SetKey(
                    "SPELL_DETAIL_UPGRADE_COST",
                    _data.InkCost);
            }

            _magicInkText.SetKey("SPELL_DETAIL_MAGIC_INK", _data.CurrentInk);
            _lockButton.SetWithoutNotify(_data.IsLocked);
            _lockActionText.SetKey(
                _data.IsLocked ? "SPELL_DETAIL_UNLOCK" : "SPELL_DETAIL_LOCK");
            _upgradeActionText.SetKey(
                _data.IsMaxLevel ? "SPELL_DETAIL_MAX_LEVEL" : "SPELL_DETAIL_UPGRADE");
        }
    }
}
