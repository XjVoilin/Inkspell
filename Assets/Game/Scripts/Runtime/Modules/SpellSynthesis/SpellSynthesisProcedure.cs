using System;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Logging;

namespace Game
{
    internal sealed class SpellSynthesisProcedure : ProcedureBase
    {
        private const int RandomResolution = 1 << 24;

        private readonly long _firstSpellId;
        private readonly long _secondSpellId;

        internal SpellSynthesisProcedure(long firstSpellId, long secondSpellId)
        {
            _firstSpellId = firstSpellId;
            _secondSpellId = secondSpellId;
        }

        internal bool Succeeded { get; private set; }

        protected override UniTask OnExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var spellAssets = GetSystem<SpellAssetSystem>();
            var firstSpell = spellAssets.GetSpell(_firstSpellId);
            var secondSpell = spellAssets.GetSpell(_secondSpellId);

            if (!firstSpell.HasValue || !secondSpell.HasValue)
            {
                return Reject(SynthesisRejectReason.SpellNotFound);
            }

            if (_firstSpellId == _secondSpellId)
            {
                return Reject(SynthesisRejectReason.SameInstance);
            }

            var first = firstSpell.Value;
            var second = secondSpell.Value;

            if (first.Tier != second.Tier)
            {
                return Reject(SynthesisRejectReason.DifferentTier);
            }

            if (first.IsLocked || second.IsLocked)
            {
                return Reject(SynthesisRejectReason.Locked);
            }

            if (first.Location != SpellLocation.CraftingArea ||
                second.Location != SpellLocation.CraftingArea)
            {
                return Reject(SynthesisRejectReason.Equipped);
            }

            var config = GetSystem<IConfigSystem>();
            var spellTiers = config.GetTable<TbSpellTier>();
            if (spellTiers.Get(first.Tier).IsHighest)
            {
                return Reject(SynthesisRejectReason.HighestTier);
            }

            if (first.Level > 1 || second.Level > 1)
            {
                return Reject(SynthesisRejectReason.Cultivated);
            }

            var synthesisRule = config.GetTable<TbSynthesisRule>().Get(first.Tier);
            var randomUnit = UnityEngine.Random.Range(0, RandomResolution) / (double)RandomResolution;

            if (randomUnit >= synthesisRule.SuccessRate)
            {
                return ResolveFailure(spellAssets, synthesisRule.FailureInkReward);
            }

            var rewardTier = checked(first.Tier + 1);
            var reward = SelectReward(
                config.GetTable<TbSynthesisReward>(),
                rewardTier,
                randomUnit / synthesisRule.SuccessRate);

            if (!spellAssets.TryCommitSynthesisSuccess(
                    _firstSpellId,
                    _secondSpellId,
                    reward.SpellType,
                    rewardTier))
            {
                JLogger.LogWarning(
                    $"[SpellSynthesisProcedure] 合成成功结果提交失败: firstSpellId={_firstSpellId}, secondSpellId={_secondSpellId}, rewardType={reward.SpellType}, rewardTier={rewardTier}");
                return UniTask.CompletedTask;
            }

            Succeeded = true;
            Publish(new SpellSynthesisResolvedEvent(
                SynthesisOutcomeKind.HigherTierSpell,
                reward.SpellType,
                rewardTier,
                0));
            return UniTask.CompletedTask;
        }

        private UniTask ResolveFailure(SpellAssetSystem spellAssets, int inkReward)
        {
            if (!spellAssets.TryCommitSynthesisFailure(
                    _firstSpellId,
                    _secondSpellId,
                    inkReward))
            {
                JLogger.LogWarning(
                    $"[SpellSynthesisProcedure] 合成失败结果提交失败: firstSpellId={_firstSpellId}, secondSpellId={_secondSpellId}, inkReward={inkReward}");
                return UniTask.CompletedTask;
            }

            Succeeded = true;
            Publish(new SpellSynthesisResolvedEvent(
                SynthesisOutcomeKind.MagicInk,
                default,
                0,
                inkReward));
            return UniTask.CompletedTask;
        }

        private UniTask Reject(SynthesisRejectReason reason)
        {
            JLogger.LogWarning(
                $"[SpellSynthesisProcedure] 合成请求被拒绝: firstSpellId={_firstSpellId}, secondSpellId={_secondSpellId}, reason={reason}");
            Publish(new SpellSynthesisRejectedEvent(
                _firstSpellId,
                _secondSpellId,
                reason));
            return UniTask.CompletedTask;
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
