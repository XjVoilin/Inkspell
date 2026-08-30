using System;
using System.Collections.Generic;
using cfg;
using July.Arch;
using July.Logging;

namespace Game
{
    public sealed class SpellAssetStore : StoreBase<SpellAssetStoreData>
    {
        private readonly Dictionary<long, SpellInstanceState> _spellsById = new();
        private readonly Dictionary<int, SpellInstanceState> _equippedBySlot = new();

        private int _craftingCount;
        private int _craftingCapacity;
        private int _equipmentSlotCount;

        internal int MagicInk => Data.MagicInk;
        internal int CraftingCapacity => _craftingCapacity;
        internal int RemainingCraftingCapacity => _craftingCapacity - _craftingCount;

        internal void Initialize(TbSpellAssetRule rule)
        {
            _craftingCapacity = checked(rule.CraftingRows * rule.CraftingColumns);
            _equipmentSlotCount = rule.EquipmentSlotCount;

            if (Data.Initialized)
            {
                return;
            }

            Data.NextInstanceId = 1;
            Data.MagicInk = 0;
            Data.Spells.Clear();
            _spellsById.Clear();
            _equippedBySlot.Clear();
            _craftingCount = 0;

            AddNewSpell(
                rule.InitialSpellType,
                rule.InitialTier,
                rule.InitialLevel,
                SpellLocation.EquipmentSlot,
                rule.InitialEquipmentSlot);
            Data.Initialized = true;

            CommitChange(false);
        }

        internal List<SpellInfo> GetCraftingAreaSpells()
        {
            var spells = new List<SpellInfo>(_craftingCount);
            foreach (var state in Data.Spells)
            {
                if (state.Location == SpellLocation.CraftingArea)
                {
                    spells.Add(ToSpellInfo(state));
                }
            }

            return spells;
        }

        internal SpellInfo? GetSpell(long instanceId)
        {
            return _spellsById.TryGetValue(instanceId, out var state)
                ? ToSpellInfo(state)
                : null;
        }

        internal SpellInfo? GetEquippedSpell(int equipmentSlot)
        {
            return _equippedBySlot.TryGetValue(equipmentSlot, out var state)
                ? ToSpellInfo(state)
                : null;
        }

        internal bool TrySetLocked(long instanceId, bool locked)
        {
            if (!_spellsById.TryGetValue(instanceId, out var state))
            {
                JLogger.LogWarning($"[SpellAssetStore] 无法设置锁定，法术实例不存在: {instanceId}");
                return false;
            }

            if (state.IsLocked == locked)
            {
                return true;
            }

            state.IsLocked = locked;
            CommitChange(false);
            return true;
        }

        internal bool TryEquip(long instanceId, int equipmentSlot)
        {
            if (equipmentSlot < 0 || equipmentSlot >= _equipmentSlotCount)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法装备法术，槽位越界: instanceId={instanceId}, slot={equipmentSlot}");
                return false;
            }

            if (!_spellsById.TryGetValue(instanceId, out var incoming))
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法装备法术，实例不存在: instanceId={instanceId}, slot={equipmentSlot}");
                return false;
            }

            if (incoming.Location != SpellLocation.CraftingArea)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法装备法术，实例不在合成区: instanceId={instanceId}, slot={equipmentSlot}");
                return false;
            }

            var capacityIncreased = !_equippedBySlot.TryGetValue(equipmentSlot, out var replaced);
            if (replaced != null)
            {
                replaced.Location = SpellLocation.CraftingArea;
                replaced.EquipmentSlot = -1;
                _craftingCount++;
            }

            incoming.Location = SpellLocation.EquipmentSlot;
            incoming.EquipmentSlot = equipmentSlot;
            _craftingCount--;
            _equippedBySlot[equipmentSlot] = incoming;

            CommitChange(capacityIncreased);
            return true;
        }

        internal bool TryReceiveGeneratedSpell(SpellType type)
        {
            if (_craftingCount >= _craftingCapacity)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法接收生成法术，合成区已满: type={type}, capacity={_craftingCapacity}");
                return false;
            }

            AddNewSpell(type, 1, 1, SpellLocation.CraftingArea, -1);
            CommitChange(false);
            return true;
        }

        internal bool TryCommitSynthesisSuccess(
            long firstId,
            long secondId,
            SpellType resultType,
            int resultTier)
        {
            if (!TryGetSynthesisInputs(firstId, secondId, out var first, out var second))
            {
                return false;
            }

            var resultId = Data.NextInstanceId;
            var nextInstanceId = checked(resultId + 1);
            var result = CreateSpellState(
                resultId,
                resultType,
                resultTier,
                1,
                SpellLocation.CraftingArea,
                -1);

            RemoveSpell(first);
            RemoveSpell(second);
            Data.NextInstanceId = nextInstanceId;
            AddSpell(result);

            CommitChange(true);
            return true;
        }

        internal bool TryCommitSynthesisFailure(long firstId, long secondId, int inkReward)
        {
            if (inkReward < 0)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法提交合成失败，墨水奖励为负数: firstId={firstId}, secondId={secondId}, inkReward={inkReward}");
                return false;
            }

            if (!TryGetSynthesisInputs(firstId, secondId, out var first, out var second))
            {
                return false;
            }

            var magicInk = checked(Data.MagicInk + inkReward);
            RemoveSpell(first);
            RemoveSpell(second);
            Data.MagicInk = magicInk;

            CommitChange(true);
            return true;
        }

        internal bool TryCommitUpgrade(long instanceId, int inkCost)
        {
            if (inkCost < 0)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法提交升级，墨水消耗为负数: instanceId={instanceId}, inkCost={inkCost}");
                return false;
            }

            if (!_spellsById.TryGetValue(instanceId, out var spell))
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法提交升级，实例不存在: instanceId={instanceId}, inkCost={inkCost}");
                return false;
            }

            if (Data.MagicInk < inkCost)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法提交升级，墨水不足: instanceId={instanceId}, inkCost={inkCost}, currentInk={Data.MagicInk}");
                return false;
            }

            var nextLevel = checked(spell.Level + 1);
            Data.MagicInk -= inkCost;
            spell.Level = nextLevel;

            CommitChange(false);
            return true;
        }

        protected override void OnDataReplaced()
        {
            RebuildIndexes();
        }

        private bool TryGetSynthesisInputs(
            long firstId,
            long secondId,
            out SpellInstanceState first,
            out SpellInstanceState second)
        {
            if (firstId == secondId ||
                !_spellsById.TryGetValue(firstId, out first) ||
                !_spellsById.TryGetValue(secondId, out second) ||
                first.Location != SpellLocation.CraftingArea ||
                second.Location != SpellLocation.CraftingArea)
            {
                JLogger.LogWarning(
                    $"[SpellAssetStore] 无法提交合成，输入实例不可用: firstId={firstId}, secondId={secondId}");
                first = null;
                second = null;
                return false;
            }

            return true;
        }

        private SpellInstanceState AddNewSpell(
            SpellType type,
            int tier,
            int level,
            SpellLocation location,
            int equipmentSlot)
        {
            var instanceId = Data.NextInstanceId;
            Data.NextInstanceId = checked(instanceId + 1);

            var state = CreateSpellState(
                instanceId,
                type,
                tier,
                level,
                location,
                equipmentSlot);
            AddSpell(state);
            return state;
        }

        private void AddSpell(SpellInstanceState state)
        {
            Data.Spells.Add(state);
            _spellsById.Add(state.InstanceId, state);

            if (state.Location == SpellLocation.CraftingArea)
            {
                _craftingCount++;
            }
            else
            {
                _equippedBySlot.Add(state.EquipmentSlot, state);
            }
        }

        private void RemoveSpell(SpellInstanceState state)
        {
            Data.Spells.Remove(state);
            _spellsById.Remove(state.InstanceId);

            if (state.Location == SpellLocation.CraftingArea)
            {
                _craftingCount--;
            }
            else
            {
                _equippedBySlot.Remove(state.EquipmentSlot);
            }
        }

        private void RebuildIndexes()
        {
            _spellsById.Clear();
            _equippedBySlot.Clear();
            _craftingCount = 0;

            foreach (var state in Data.Spells)
            {
                _spellsById.Add(state.InstanceId, state);
                if (state.Location == SpellLocation.CraftingArea)
                {
                    _craftingCount++;
                }
                else
                {
                    _equippedBySlot.Add(state.EquipmentSlot, state);
                }
            }
        }

        private void CommitChange(bool capacityIncreased)
        {
            MarkDirty();
            Publish(new SpellAssetsChangedEvent(capacityIncreased));
        }

        private static SpellInstanceState CreateSpellState(
            long instanceId,
            SpellType type,
            int tier,
            int level,
            SpellLocation location,
            int equipmentSlot)
        {
            return new SpellInstanceState
            {
                InstanceId = instanceId,
                Type = type,
                Tier = tier,
                Level = level,
                Location = location,
                EquipmentSlot = equipmentSlot,
                IsLocked = false,
            };
        }

        private static SpellInfo ToSpellInfo(SpellInstanceState state)
        {
            return new SpellInfo(
                state.InstanceId,
                state.Type,
                state.Tier,
                state.Level,
                state.Location,
                state.EquipmentSlot,
                state.IsLocked);
        }
    }
}
