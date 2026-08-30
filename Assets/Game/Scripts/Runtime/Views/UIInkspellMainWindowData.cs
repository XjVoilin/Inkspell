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
        private readonly SpellAssetSystem _spellAssets;
        private readonly AutoBattleSystem _autoBattle;
        private readonly StageProgressionSystem _stageProgression;
        private readonly SpellGenerationSystem _spellGeneration;
        private readonly cfg.TbSpellDefinition _spellDefinitions;
        private readonly cfg.TbSpellTier _spellTiers;
        private readonly cfg.TbSpellAssetRule _spellAssetRule;
        private readonly cfg.TbBattleRule _battleRule;
        private readonly cfg.TbSpellCombat _spellCombat;
        private readonly cfg.TbSpellUpgrade _spellUpgrades;

        public UIInkspellMainWindowData()
        {
            _spellAssets = ArchContext.Current.GetSystem<SpellAssetSystem>();
            _autoBattle = ArchContext.Current.GetSystem<AutoBattleSystem>();
            _stageProgression = ArchContext.Current.GetSystem<StageProgressionSystem>();
            _spellGeneration = ArchContext.Current.GetSystem<SpellGenerationSystem>();
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
            var battle = _autoBattle.GetBattleViewState();
            Status.CurrentStageId = _stageProgression.CurrentStageId;
            Status.MagicInk = _spellAssets.MagicInk;
            Status.PendingSpellCount = _spellGeneration.PendingCount;
            Status.GenerationProgressSeconds = _spellGeneration.CurrentCycleProgressSeconds;
            Status.GenerationIntervalSeconds = _spellGeneration.CurrentIntervalSeconds;
            Status.BookHealth = battle.BookHealth;
            Status.BookMaxHealth = battle.BookMaxHealth;
            Status.BookShield = battle.BookShield;
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
            var spells = _spellAssets.GetCraftingAreaSpells();
            var slots = new SpellCardViewData[_spellAssets.CraftingCapacity];
            for (var index = 0; index < spells.Count; index++)
            {
                slots[index] = CreateSpellCard(spells[index], true);
            }

            SpellBoard.Slots = slots;
        }

        public void RefreshEquipmentBar()
        {
            var slots = new SpellCardViewData[_spellAssetRule.EquipmentSlotCount];
            for (var index = 0; index < slots.Length; index++)
            {
                var spell = _spellAssets.GetEquippedSpell(index);
                if (spell.HasValue)
                {
                    slots[index] = CreateSpellCard(spell.Value, false);
                }
            }

            EquipmentBar.Slots = slots;
        }

        public void RefreshBattlefield()
        {
            var battle = _autoBattle.GetBattleViewState();
            Battlefield.ChallengeId = battle.ChallengeId;
            Battlefield.StageId = battle.StageId;
            Battlefield.IsRunning = battle.IsRunning;
            Battlefield.BookHealth = battle.BookHealth;
            Battlefield.BookMaxHealth = battle.BookMaxHealth;
            Battlefield.BookShield = battle.BookShield;
            Battlefield.BookShieldMaximum = GetBookShieldMaximum(battle.BookShield);

            var enemies = new EnemyBattleViewData[battle.Enemies.Count];
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = battle.Enemies[index];
                enemies[index] = new EnemyBattleViewData
                {
                    RuntimeId = enemy.RuntimeId,
                    Type = enemy.Type,
                    Health = enemy.Health,
                    MaxHealth = enemy.MaxHealth,
                    PathNormalized = NormalizePath(enemy.PathPosition),
                    SlowRemainingSeconds = enemy.SlowRemainingSeconds,
                    SlowMultiplier = enemy.SlowMultiplier,
                };
            }

            Battlefield.Enemies = enemies;

            var cooldowns = new SpellCooldownViewData[battle.Cooldowns.Count];
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var cooldown = battle.Cooldowns[index];
                var totalSeconds = cooldown.RemainingSeconds;
                var spell = _spellAssets.GetEquippedSpell(cooldown.EquipmentSlot);
                if (spell.HasValue)
                {
                    var combat = _spellCombat.Get(spell.Value.Type, spell.Value.Tier);
                    totalSeconds = Mathf.Max(totalSeconds, combat.CooldownSeconds);
                }

                cooldowns[index] = new SpellCooldownViewData
                {
                    EquipmentSlot = cooldown.EquipmentSlot,
                    RemainingSeconds = cooldown.RemainingSeconds,
                    TotalSeconds = totalSeconds,
                };
            }

            Battlefield.Cooldowns = cooldowns;

            var attacks = new BattleAttackFeedbackViewData[battle.Attacks.Count];
            for (var index = 0; index < attacks.Length; index++)
            {
                var attack = battle.Attacks[index];
                attacks[index] = new BattleAttackFeedbackViewData
                {
                    AttackId = attack.AttackId,
                    SpellType = attack.SpellType,
                    TargetPathNormalized = NormalizePath(attack.TargetPathPosition),
                    RemainingTravelSeconds = attack.RemainingTravelSeconds,
                };
            }

            Battlefield.Attacks = attacks;

            var effects = new BattleEffectFeedbackViewData[battle.Effects.Count];
            for (var index = 0; index < effects.Length; index++)
            {
                var effect = battle.Effects[index];
                effects[index] = new BattleEffectFeedbackViewData
                {
                    EffectId = effect.EffectId,
                    SpellType = effect.SpellType,
                    TargetEnemyId = effect.TargetEnemyId,
                    PathNormalized = NormalizePath(effect.PathPosition),
                    RemainingSeconds = effect.RemainingSeconds,
                };
            }

            Battlefield.Effects = effects;
        }

        private SpellCardViewData CreateSpellCard(SpellInfo spell, bool canDrag)
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
                var spell = _spellAssets.GetEquippedSpell(slot);
                if (!spell.HasValue || spell.Value.Type != cfg.SpellType.Shield)
                {
                    continue;
                }

                var combat = _spellCombat.Get(spell.Value.Type, spell.Value.Tier);
                var upgrade = _spellUpgrades.Get(
                    spell.Value.Type,
                    spell.Value.Tier,
                    spell.Value.Level);
                maximum = Mathf.Max(
                    maximum,
                    combat.BaseShield * upgrade.CurrentPowerMultiplier);
            }

            return maximum;
        }

        private float NormalizePath(float pathPosition)
        {
            return Mathf.InverseLerp(
                _battleRule.BookContactPosition,
                _battleRule.EnemySpawnPosition,
                pathPosition);
        }
    }
}
