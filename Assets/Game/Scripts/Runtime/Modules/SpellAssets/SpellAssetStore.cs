using System;
using System.Collections.Generic;
using cfg;
using July.Arch;
using July.Logging;

namespace Game
{
    internal sealed class SpellAssetStore : StoreBase<SpellAssetStoreData>
    {
        // 下列索引均由 Data 派生，不写入存档；换档后由 OnDataReplaced 统一重建。
        private readonly Dictionary<long, SpellInstance> _spellsById = new();
        private readonly Dictionary<int, SpellInstance> _equippedBySlot = new();

        private int _craftingCount;
        private int _craftingCapacity;
        private int _equipmentSlotCount;

        internal int MagicInk => Data.MagicInk;
        internal int CraftingCapacity => _craftingCapacity;
        internal int RemainingCraftingCapacity => _craftingCapacity - _craftingCount;

        internal void Initialize(SpellAssetRule rule)
        {
            // 配置派生的运行时限制不写入存档，每次恢复后都要重新建立。
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

        internal IReadOnlyList<SpellInstance> GetCraftingAreaSpells()
        {
            var spells = new List<SpellInstance>(_craftingCount);
            foreach (var spell in Data.Spells)
            {
                if (spell.Location == SpellLocation.CraftingArea)
                {
                    spells.Add(spell);
                }
            }

            return spells;
        }

        internal bool TryGetSpell(
            long instanceId,
            out SpellInstance spell)
        {
            return _spellsById.TryGetValue(instanceId, out spell);
        }

        internal bool TryGetEquippedSpell(
            int equipmentSlot,
            out SpellInstance spell)
        {
            return _equippedBySlot.TryGetValue(equipmentSlot, out spell);
        }

        internal bool TrySetLocked(long instanceId, bool locked)
        {
            if (!_spellsById.TryGetValue(instanceId, out var spell))
            {
                JLogger.LogWarning($"[SpellAssetStore] 无法设置锁定，法术实例不存在: {instanceId}");
                return false;
            }

            if (spell.IsLocked == locked)
            {
                return true;
            }

            spell.IsLocked = locked;
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

            // 替换装备时先把旧法术放回合成区，再把新法术移入槽位；整个交换只发布一次变更。
            var craftingSpaceFreed = !_equippedBySlot.TryGetValue(equipmentSlot, out var replaced);
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

            CommitChange(craftingSpaceFreed);
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

        internal void CommitSynthesisSuccess(
            long firstId,
            long secondId,
            SpellType resultType,
            int resultTier)
        {
            var first = _spellsById[firstId];
            var second = _spellsById[secondId];

            var resultId = Data.NextInstanceId;
            var nextInstanceId = checked(resultId + 1);
            var result = new SpellInstance
            {
                InstanceId = resultId,
                Type = resultType,
                Tier = resultTier,
                Level = 1,
                Location = SpellLocation.CraftingArea,
                EquipmentSlot = -1,
                IsLocked = false,
            };

            // 输入消耗与产物创建共享一次提交，外部不会观察到合成中间态。
            RemoveSpell(first);
            RemoveSpell(second);
            Data.NextInstanceId = nextInstanceId;
            AddSpell(result);

            CommitChange(true);
        }

        internal void CommitSynthesisFailure(
            long firstId,
            long secondId,
            int inkReward)
        {
            if (inkReward < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(inkReward),
                    inkReward,
                    "合成失败的墨水奖励不能为负数。");
            }

            var first = _spellsById[firstId];
            var second = _spellsById[secondId];
            var magicInk = checked(Data.MagicInk + inkReward);
            RemoveSpell(first);
            RemoveSpell(second);
            Data.MagicInk = magicInk;

            CommitChange(true);
        }

        internal void CommitUpgrade(long instanceId, int inkCost)
        {
            var spell = _spellsById[instanceId];
            var remainingInk = checked(Data.MagicInk - inkCost);
            var nextLevel = checked(spell.Level + 1);

            Data.MagicInk = remainingInk;
            spell.Level = nextLevel;
            CommitChange(false);
        }

        protected override void OnDataReplaced()
        {
            // 反序列化只恢复 Data，运行时查询索引必须随之重建。
            RebuildIndexes();
        }

        private SpellInstance AddNewSpell(
            SpellType type,
            int tier,
            int level,
            SpellLocation location,
            int equipmentSlot)
        {
            var instanceId = Data.NextInstanceId;
            Data.NextInstanceId = checked(instanceId + 1);

            var spell = new SpellInstance
            {
                InstanceId = instanceId,
                Type = type,
                Tier = tier,
                Level = level,
                Location = location,
                EquipmentSlot = equipmentSlot,
                IsLocked = false,
            };
            AddSpell(spell);
            return spell;
        }

        private void AddSpell(SpellInstance spell)
        {
            Data.Spells.Add(spell);
            _spellsById.Add(spell.InstanceId, spell);

            if (spell.Location == SpellLocation.CraftingArea)
            {
                _craftingCount++;
            }
            else
            {
                _equippedBySlot.Add(spell.EquipmentSlot, spell);
            }
        }

        private void RemoveSpell(SpellInstance spell)
        {
            Data.Spells.Remove(spell);
            _spellsById.Remove(spell.InstanceId);

            if (spell.Location == SpellLocation.CraftingArea)
            {
                _craftingCount--;
            }
            else
            {
                _equippedBySlot.Remove(spell.EquipmentSlot);
            }
        }

        private void RebuildIndexes()
        {
            _spellsById.Clear();
            _equippedBySlot.Clear();
            _craftingCount = 0;

            foreach (var spell in Data.Spells)
            {
                _spellsById.Add(spell.InstanceId, spell);
                if (spell.Location == SpellLocation.CraftingArea)
                {
                    _craftingCount++;
                }
                else
                {
                    _equippedBySlot.Add(spell.EquipmentSlot, spell);
                }
            }
        }

        private void CommitChange(bool craftingSpaceFreed)
        {
            MarkDirty();
            Publish(new SpellAssetsChangedEvent(craftingSpaceFreed));
        }

    }
}
