using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>
    /// 负责法术资产恢复后的新档初始化、容量解释与配置派生的稳定排列。
    /// </summary>
    public sealed class SpellAssetSystem : SystemBase
    {
        private SpellAssetStore _store;
        private TbSpellAssetRule _assetRule;
        private TbSpellDefinition _spellDefinitions;

        public int CraftingCapacity => _store.CraftingCapacity;

        internal IReadOnlyList<SpellInstanceState> GetSortedCraftingAreaSpells()
        {
            var spells = new List<SpellInstanceState>(
                _store.GetCraftingAreaSpellStates());
            spells.Sort(CompareCraftingAreaSpells);
            return spells.AsReadOnly();
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

        private int CompareCraftingAreaSpells(
            SpellInstanceState left,
            SpellInstanceState right)
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
