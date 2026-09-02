using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>
    /// 单个法术详情窗口的唯一显示输入。
    /// </summary>
    public sealed class UISpellDetailWindowData
    {
        private SpellAssetStore _spellAssets;
        private SpellProgressionSystem _spellProgression;
        private cfg.TbSpellDefinition _spellDefinitions;
        private cfg.TbSpellTier _spellTiers;
        private cfg.TbSpellCombat _spellCombat;

        public UISpellDetailWindowData()
        {
        }

        public UISpellDetailWindowData(long spellInstanceId)
        {
            SpellInstanceId = spellInstanceId;
            BindSources();
            Refresh();
        }

        public long SpellInstanceId { get; set; }
        public cfg.SpellType SpellType { get; set; }
        public string DisplayNameKey { get; set; }
        public string TierDisplayKey { get; set; }
        public int Tier { get; set; }
        public int Level { get; set; }
        public SpellLocation Location { get; set; }
        public int EquipmentSlot { get; set; }
        public bool IsLocked { get; set; }
        public int CurrentInk { get; set; }
        public int InkCost { get; set; }
        public int MissingInk { get; set; }
        public bool IsMaxLevel { get; set; }
        public string MainValueNameKey { get; set; }
        public float CurrentMainValue { get; set; }
        public float NextMainValue { get; set; }
        public float CurrentPowerMultiplier { get; set; }
        public float NextPowerMultiplier { get; set; }

        public void Refresh()
        {
            if (SpellInstanceId == 0)
            {
                return;
            }

            if (_spellAssets == null)
            {
                BindSources();
            }

            if (!_spellAssets.TryGetSpell(
                    SpellInstanceId,
                    out SpellInstance spell))
            {
                throw new System.InvalidOperationException(
                    $"详情窗口绑定的法术实例不存在: {SpellInstanceId}");
            }

            var upgrade = _spellProgression.GetUpgradeInfo(SpellInstanceId);
            var definition = _spellDefinitions.Get(spell.Type);
            var tier = _spellTiers.Get(spell.Tier);
            var combat = _spellCombat.Get(spell.Type, spell.Tier);
            var baseMainValue = spell.Type == cfg.SpellType.Shield
                ? combat.BaseShield
                : combat.BaseDamage;

            SpellType = spell.Type;
            DisplayNameKey = definition.DisplayNameKey;
            TierDisplayKey = tier.DisplayNameKey;
            Tier = spell.Tier;
            Level = spell.Level;
            Location = spell.Location;
            EquipmentSlot = spell.EquipmentSlot;
            IsLocked = spell.IsLocked;
            CurrentInk = _spellAssets.MagicInk;
            InkCost = upgrade.InkCost;
            MissingInk = upgrade.IsMaxLevel
                ? 0
                : System.Math.Max(0, upgrade.InkCost - CurrentInk);
            IsMaxLevel = upgrade.IsMaxLevel;
            MainValueNameKey = spell.Type == cfg.SpellType.Shield
                ? "SPELL_DETAIL_SHIELD_VALUE_NAME"
                : "SPELL_DETAIL_DAMAGE_VALUE_NAME";
            CurrentPowerMultiplier = upgrade.CurrentPowerMultiplier;
            NextPowerMultiplier = upgrade.NextPowerMultiplier;
            CurrentMainValue = baseMainValue * upgrade.CurrentPowerMultiplier;
            NextMainValue = baseMainValue * upgrade.NextPowerMultiplier;
        }

        private void BindSources()
        {
            _spellAssets = ArchContext.Current.GetStore<SpellAssetStore>();
            _spellProgression = ArchContext.Current.GetSystem<SpellProgressionSystem>();
            var config = ArchContext.Current.GetSystem<IConfigSystem>();
            _spellDefinitions = config.GetTable<cfg.TbSpellDefinition>();
            _spellTiers = config.GetTable<cfg.TbSpellTier>();
            _spellCombat = config.GetTable<cfg.TbSpellCombat>();
        }
    }
}
