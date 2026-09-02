using System;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Logging;

namespace Game
{
    /// <summary>
    /// 对一次玩家主动二合进行合法性判定、随机结算与完整资产提交。
    /// </summary>
    public sealed class SpellSynthesisSystem : SystemBase
    {
        // 使用 24 位离散样本，既满足 Unity Random 的整数范围，也便于复用同一次随机结果。
        private const int RandomResolution = 1 << 24;

        private SpellAssetStore _spellAssets;
        private TbSpellTier _spellTiers;
        private TbSynthesisRule _synthesisRules;
        private TbSynthesisReward _synthesisRewards;

        public bool TrySynthesize(long firstSpellId, long secondSpellId)
        {
            if (!_spellAssets.TryGetSpell(firstSpellId, out SpellInstance first) ||
                !_spellAssets.TryGetSpell(secondSpellId, out SpellInstance second))
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.SpellNotFound);
            }

            if (firstSpellId == secondSpellId)
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.SameInstance);
            }

            if (first.Tier != second.Tier)
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.DifferentTier);
            }

            if (first.IsLocked || second.IsLocked)
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.Locked);
            }

            if (first.Location != SpellLocation.CraftingArea ||
                second.Location != SpellLocation.CraftingArea)
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.Equipped);
            }

            if (_spellTiers.Get(first.Tier).IsHighest)
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.HighestTier);
            }

            if (first.Level > 1 || second.Level > 1)
            {
                return Reject(firstSpellId, secondSpellId, SynthesisRejectReason.Cultivated);
            }

            var synthesisRule = _synthesisRules.Get(first.Tier);
            var randomUnit = UnityEngine.Random.Range(0, RandomResolution) / (double)RandomResolution;

            if (randomUnit >= synthesisRule.SuccessRate)
            {
                _spellAssets.CommitSynthesisFailure(
                    firstSpellId,
                    secondSpellId,
                    synthesisRule.FailureInkReward);
                Publish(new SpellSynthesisResolvedEvent(
                    SynthesisOutcomeKind.MagicInk,
                    default,
                    0,
                    synthesisRule.FailureInkReward));
                return true;
            }

            var rewardTier = checked(first.Tier + 1);
            // 成功区间内重新归一化，同一随机样本同时决定成败与产物，不额外消耗随机序列。
            var reward = SelectReward(
                _synthesisRewards,
                rewardTier,
                randomUnit / synthesisRule.SuccessRate);

            _spellAssets.CommitSynthesisSuccess(
                firstSpellId,
                secondSpellId,
                reward.SpellType,
                rewardTier);
            Publish(new SpellSynthesisResolvedEvent(
                SynthesisOutcomeKind.HigherTierSpell,
                reward.SpellType,
                rewardTier,
                0));
            return true;
        }

        protected override UniTask OnInitializeAsync()
        {
            _spellAssets = GetStore<SpellAssetStore>();

            var config = GetSystem<IConfigSystem>();
            _spellTiers = config.GetTable<TbSpellTier>();
            _synthesisRules = config.GetTable<TbSynthesisRule>();
            _synthesisRewards = config.GetTable<TbSynthesisReward>();
            return UniTask.CompletedTask;
        }

        private bool Reject(
            long firstSpellId,
            long secondSpellId,
            SynthesisRejectReason reason)
        {
            JLogger.LogWarning(
                $"[SpellSynthesisSystem] 合成请求被拒绝: firstSpellId={firstSpellId}, secondSpellId={secondSpellId}, reason={reason}");
            Publish(new SpellSynthesisRejectedEvent(
                firstSpellId,
                secondSpellId,
                reason));
            return false;
        }

        private static SynthesisReward SelectReward(
            TbSynthesisReward rewards,
            int outputTier,
            double normalizedRandom)
        {
            var totalWeight = 0;
            foreach (var reward in rewards.DataList)
            {
                if (reward.OutputTier == outputTier && reward.IsOpen)
                {
                    totalWeight = checked(totalWeight + reward.Weight);
                }
            }

            var remainingWeight = normalizedRandom * totalWeight;
            foreach (var reward in rewards.DataList)
            {
                if (reward.OutputTier != outputTier || !reward.IsOpen)
                {
                    continue;
                }

                if (remainingWeight < reward.Weight)
                {
                    return reward;
                }

                remainingWeight -= reward.Weight;
            }

            throw new InvalidOperationException(
                $"TbSynthesisReward 无法为输出阶级 {outputTier} 选择开放产物。");
        }
    }
}
