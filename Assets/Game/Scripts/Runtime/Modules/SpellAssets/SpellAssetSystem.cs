using System;
using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    public sealed class SpellAssetSystem : SystemBase
    {
        private SpellAssetStore _store;
        private TbSpellAssetRule _assetRule;
        private TbSpellDefinition _spellDefinitions;

        public int MagicInk => _store.MagicInk;
        public int CraftingCapacity => _store.CraftingCapacity;
        public int RemainingCraftingCapacity => _store.RemainingCraftingCapacity;

        public IReadOnlyList<SpellInfo> GetCraftingAreaSpells()
        {
            var spells = _store.GetCraftingAreaSpells();
            spells.Sort(CompareCraftingAreaSpells);
            return spells.AsReadOnly();
        }

        public SpellInfo? GetSpell(long instanceId)
        {
            return _store.GetSpell(instanceId);
        }

        public SpellInfo? GetEquippedSpell(int equipmentSlot)
        {
            if (equipmentSlot < 0 || equipmentSlot >= _assetRule.EquipmentSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(equipmentSlot), equipmentSlot, null);
            }

            return _store.GetEquippedSpell(equipmentSlot);
        }

        public bool TrySetLocked(long instanceId, bool locked)
        {
            return _store.TrySetLocked(instanceId, locked);
        }

        public bool TryEquip(long instanceId, int equipmentSlot)
        {
            return _store.TryEquip(instanceId, equipmentSlot);
        }

        public bool TryReceiveGeneratedSpell(SpellType type)
        {
            return _store.TryReceiveGeneratedSpell(type);
        }

        public bool TryCommitSynthesisSuccess(
            long firstId,
            long secondId,
            SpellType resultType,
            int resultTier)
        {
            return _store.TryCommitSynthesisSuccess(
                firstId,
                secondId,
                resultType,
                resultTier);
        }

        public bool TryCommitSynthesisFailure(long firstId, long secondId, int inkReward)
        {
            return _store.TryCommitSynthesisFailure(firstId, secondId, inkReward);
        }

        public bool TryCommitUpgrade(long instanceId, int inkCost)
        {
            return _store.TryCommitUpgrade(instanceId, inkCost);
        }

        protected override UniTask OnInitializeAsync()
        {
            _store = GetStore<SpellAssetStore>();

            var config = GetSystem<IConfigSystem>();
            _assetRule = config.GetTable<TbSpellAssetRule>();
            _spellDefinitions = config.GetTable<TbSpellDefinition>();

            _store.Initialize(_assetRule);
            return UniTask.CompletedTask;
        }

        private int CompareCraftingAreaSpells(SpellInfo left, SpellInfo right)
        {
            var priorityComparison = _spellDefinitions
                .Get(left.Type)
                .DisplayPriority
                .CompareTo(_spellDefinitions.Get(right.Type).DisplayPriority);

            return priorityComparison != 0
                ? priorityComparison
                : left.InstanceId.CompareTo(right.InstanceId);
        }
    }
}
