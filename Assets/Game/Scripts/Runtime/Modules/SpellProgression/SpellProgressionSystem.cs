using System;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Logging;

namespace Game
{
    /// <summary>解释升级配置，并编排墨水扣除与法术等级提升的一次原子提交。</summary>
    public sealed class SpellProgressionSystem : SystemBase
    {
        private SpellAssetStore _spellAssets;
        private TbSpellUpgrade _spellUpgrades;

        public SpellUpgradeInfo GetUpgradeInfo(long instanceId)
        {
            if (!_spellAssets.TryGetSpell(
                    instanceId,
                    out SpellInstance currentSpell))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(instanceId),
                    instanceId,
                    "法术实例不存在。");
            }

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

            _spellAssets.CommitUpgrade(instanceId, upgrade.InkCost);

            Publish(new SpellUpgradedEvent(upgrade));
            return true;
        }

        protected override UniTask OnInitializeAsync()
        {
            _spellAssets = GetStore<SpellAssetStore>();
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
