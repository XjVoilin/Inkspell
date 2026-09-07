using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;

namespace Game
{
    /// <summary>
    /// 负责法术资产恢复后的新档初始化与配置派生的稳定排列。
    /// </summary>
    public sealed partial class SpellAssetSystem : SystemBase
    {
        private SpellAssetStore _store;
        private TbSpellDefinition _spellDefinitions;


        internal IReadOnlyList<SpellInstance> GetSortedCraftingAreaSpells()
        {
            var spells = new List<SpellInstance>(
                _store.GetCraftingAreaSpells());
            spells.Sort(CompareCraftingAreaSpells);
            return spells;
        }

        protected override UniTask OnInitializeAsync()
        {
            _store = GetStore<SpellAssetStore>();

            var config = GetSystem<IConfigSystem>();
            _spellDefinitions = config.GetTable<TbSpellDefinition>();

            _store.Initialize(config.GetTable<TbSpellAssetRule>().Data);
            return UniTask.CompletedTask;
        }

        private int CompareCraftingAreaSpells(
            SpellInstance left,
            SpellInstance right)
        {
            var tierComparison = right.Tier.CompareTo(left.Tier);
            if (tierComparison != 0) return tierComparison;
            var levelComparison = right.Level.CompareTo(left.Level);
            if (levelComparison != 0) return levelComparison;
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
