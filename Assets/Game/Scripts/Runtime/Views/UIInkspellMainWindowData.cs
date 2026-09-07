using July.Arch;
using July.Config;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Inkspell 主界面的唯一显示输入。
    /// </summary>
    public sealed class UIInkspellMainWindowData
    {
        private readonly SpellAssetStore _spellAssetStore;
        private readonly SpellGenerationStore _spellGenerationStore;
        private readonly StageProgressionStore _stageProgressionStore;
        private readonly SpellAssetSystem _spellAssetSystem;
        private readonly AutoBattleSystem _autoBattle;
        private readonly cfg.TbSpellDefinition _spellDefinitions;
        private readonly cfg.TbSpellTier _spellTiers;
        private readonly cfg.TbSpellAssetRule _spellAssetRule;
        private readonly cfg.TbBattleRule _battleRule;
        private readonly cfg.TbSpellCombat _spellCombat;
        private readonly cfg.TbSpellUpgrade _spellUpgrades;

        public UIInkspellMainWindowData()
        {
            _spellAssetStore = ArchContext.Current.GetStore<SpellAssetStore>();
            _spellGenerationStore = ArchContext.Current.GetStore<SpellGenerationStore>();
            _stageProgressionStore = ArchContext.Current.GetStore<StageProgressionStore>();
            _spellAssetSystem = ArchContext.Current.GetSystem<SpellAssetSystem>();
            _autoBattle = ArchContext.Current.GetSystem<AutoBattleSystem>();
            var config = ArchContext.Current.GetSystem<IConfigSystem>();
            _spellDefinitions = config.GetTable<cfg.TbSpellDefinition>();
            _spellTiers = config.GetTable<cfg.TbSpellTier>();
            _spellAssetRule = config.GetTable<cfg.TbSpellAssetRule>();
            _battleRule = config.GetTable<cfg.TbBattleRule>();
            _spellCombat = config.GetTable<cfg.TbSpellCombat>();
            _spellUpgrades = config.GetTable<cfg.TbSpellUpgrade>();
            Status = new MainStatusViewData();
            SpellBoard = new SpellBoardViewData();
            EquipmentBar = new EquipmentBarViewData();
            Battlefield = new BattlefieldViewData();
            RefreshAll();
        }

        public MainStatusViewData Status { get; private set; }
        public SpellBoardViewData SpellBoard { get; private set; }
        public EquipmentBarViewData EquipmentBar { get; private set; }
        public BattlefieldViewData Battlefield { get; private set; }

        public void RefreshStatus()
        {
            var battle = _autoBattle.CurrentRun;
            Status.CurrentStageId = _stageProgressionStore.CurrentStageId;
            Status.MagicInk = _spellAssetStore.MagicInk;
            Status.PendingSpellCount = _spellGenerationStore.PendingCount;
            Status.GenerationProgressSeconds = _spellGenerationStore.CycleProgressSeconds;
            Status.GenerationIntervalSeconds = _spellGenerationStore.ActiveIntervalSeconds;
            Status.BookHealth = battle == null ? _battleRule.BookMaxHealth : battle.Book.Health;
            Status.BookMaxHealth = battle == null ? _battleRule.BookMaxHealth : battle.Book.MaxHealth;
            Status.BookShield = battle == null ? 0f : battle.Book.Shield;
        }

        public void RefreshAll()
        {
            RefreshStatus();
            RefreshSpellBoard();
            RefreshEquipmentBar();
            RefreshBattlefield();
        }

        public void RefreshSpellBoard()
        {
            var spells = _spellAssetSystem.GetSortedCraftingAreaSpells();
            var slots = new SpellCardViewData[_spellAssetStore.CraftingCapacity];
            for (var i = 0; i < spells.Count; i++) slots[i] = CreateSpellCard(spells[i], true);
            SpellBoard.Slots = slots;
        }

        public void RefreshEquipmentBar()
        {
            var slots = new SpellCardViewData[_spellAssetRule.EquipmentSlotCount];
            for (var index = 0; index < slots.Length; index++)
            {
                if (_spellAssetStore.TryGetEquippedSpell(
                        index,
                        out SpellInstance spell))
                {
                    slots[index] = CreateSpellCard(spell, false);
                }
            }

            EquipmentBar.Slots = slots;
        }

        public void RefreshBattlefield()
        {
            var battle = _autoBattle.CurrentRun;
            if (battle == null)
            {
                Battlefield = new BattlefieldViewData
                {
                    BookHealth = _battleRule.BookMaxHealth,
                    BookMaxHealth = _battleRule.BookMaxHealth,
                };
                return;
            }

            Battlefield.BattleRunId = battle.BattleRunId;
            Battlefield.IsRunning = battle.IsRunning;
            Battlefield.BookHealth = battle.Book.Health;
            Battlefield.BookMaxHealth = battle.Book.MaxHealth;
            Battlefield.BookShield = battle.Book.Shield;
            Battlefield.BookShieldMaximum = GetBookShieldMaximum(battle.Book.Shield);

            var enemies = EnsureViewData(Battlefield.Enemies, battle.Enemies.Count);
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = battle.Enemies.Items[index];
                var viewData = enemies[index];
                if (viewData.RuntimeId != 0 && viewData.RuntimeId != enemy.RuntimeId)
                {
                    viewData = enemies[index] = new EnemyBattleViewData();
                }

                viewData.RuntimeId = enemy.RuntimeId;
                viewData.Type = enemy.Type;
                viewData.Health = enemy.Health;
                viewData.MaxHealth = enemy.MaxHealth;
                viewData.PathNormalized = NormalizePath(enemy.PathPosition);
                viewData.SlowRemainingSeconds = enemy.SlowRemainingSeconds;
                viewData.SlowMultiplier = enemy.SlowMultiplier;
            }

            Battlefield.Enemies = enemies;

            var cooldowns = EnsureViewData(
                Battlefield.Cooldowns,
                battle.Cooldowns.Items.Count);
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var cooldown = battle.Cooldowns.Items[index];
                var viewData = cooldowns[index];
                viewData.EquipmentSlot = cooldown.EquipmentSlot;
                viewData.RemainingSeconds = cooldown.RemainingSeconds;
                viewData.TotalSeconds = cooldown.TotalSeconds;
            }

            Battlefield.Cooldowns = cooldowns;

        }

        private SpellCardViewData CreateSpellCard(
            SpellInstance spell,
            bool canDrag)
        {
            return new SpellCardViewData
            {
                InstanceId = spell.InstanceId,
                SpellType = spell.Type,
                Tier = spell.Tier,
                Level = spell.Level,
                IsLocked = spell.IsLocked,
                IconResourceKey = _spellDefinitions.Get(spell.Type).IconResourceKey,
                TierDisplayKey = _spellTiers.Get(spell.Tier).DisplayNameKey,
                CanDrag = canDrag,
            };
        }

        private float GetBookShieldMaximum(float currentShield)
        {
            var maximum = currentShield;
            for (var slot = 0; slot < _spellAssetRule.EquipmentSlotCount; slot++)
            {
                if (!_spellAssetStore.TryGetEquippedSpell(
                        slot,
                        out SpellInstance spell) ||
                    spell.Type != cfg.SpellType.Shield)
                {
                    continue;
                }

                var combat = _spellCombat.Get(spell.Type, spell.Tier);
                var upgrade = _spellUpgrades.Get(
                    spell.Type,
                    spell.Tier,
                    spell.Level);
                maximum = Mathf.Max(
                    maximum,
                    combat.BaseShield * upgrade.CurrentPowerMultiplier);
            }

            return maximum;
        }

        internal float NormalizePath(float pathPosition)
        {
            return Mathf.InverseLerp(
                _battleRule.BookContactPosition,
                _battleRule.EnemySpawnPosition,
                pathPosition);
        }

        private static T[] EnsureViewData<T>(T[] items, int count)
            where T : class, new()
        {
            if (items.Length != count)
            {
                items = new T[count];
            }

            for (var index = 0; index < items.Length; index++)
            {
                items[index] ??= new T();
            }

            return items;
        }
    }
}
