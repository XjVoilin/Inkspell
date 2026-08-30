using System;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Logging;

namespace Game
{
    public sealed class SpellProgressionSystem : SystemBase
    {
        private SpellAssetSystem _spellAssets;
        private TbSpellUpgrade _spellUpgrades;

        public SpellUpgradeInfo GetUpgradeInfo(long instanceId)
        {
            var spell = _spellAssets.GetSpell(instanceId);
            if (!spell.HasValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(instanceId),
                    instanceId,
                    "法术实例不存在。");
            }

            var currentSpell = spell.Value;
            var rule = _spellUpgrades.Get(
                currentSpell.Type,
                currentSpell.Tier,
                currentSpell.Level);

            return new SpellUpgradeInfo(
                instanceId,
                currentSpell.Level,
                rule.NextLevel,
                rule.InkCost,
                rule.CurrentPowerMultiplier,
                rule.NextPowerMultiplier,
                rule.IsMaxLevel,
                _spellAssets.MagicInk);
        }

        public bool TryUpgrade(long instanceId)
        {
            var upgrade = GetUpgradeInfo(instanceId);
            if (upgrade.IsMaxLevel)
            {
                return Reject(upgrade, SpellUpgradeRejectReason.MaxLevel);
            }

            if (upgrade.CurrentInk < upgrade.InkCost)
            {
                return Reject(upgrade, SpellUpgradeRejectReason.InsufficientInk);
            }

            if (!_spellAssets.TryCommitUpgrade(instanceId, upgrade.InkCost))
            {
                throw new InvalidOperationException(
                    $"法术升级预检通过后资产提交失败: instanceId={instanceId}, inkCost={upgrade.InkCost}, currentInk={upgrade.CurrentInk}");
            }

            Publish(new SpellUpgradedEvent(upgrade));
            return true;
        }

        protected override UniTask OnInitializeAsync()
        {
            _spellAssets = GetSystem<SpellAssetSystem>();
            _spellUpgrades = GetSystem<IConfigSystem>().GetTable<TbSpellUpgrade>();
            return UniTask.CompletedTask;
        }

        private bool Reject(
            SpellUpgradeInfo upgrade,
            SpellUpgradeRejectReason reason)
        {
            JLogger.LogWarning(
                $"[SpellProgressionSystem] 升级请求被拒绝: instanceId={upgrade.InstanceId}, currentLevel={upgrade.CurrentLevel}, inkCost={upgrade.InkCost}, currentInk={upgrade.CurrentInk}, reason={reason}");
            Publish(new SpellUpgradeRejectedEvent(upgrade.InstanceId, reason));
            return false;
        }
    }
}
