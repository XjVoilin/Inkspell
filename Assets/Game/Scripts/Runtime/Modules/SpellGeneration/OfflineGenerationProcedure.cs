using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Time;

namespace Game
{
    public sealed class OfflineGenerationProcedure : ProcedureBase
    {
        public OfflineGenerationOutcome Outcome { get; private set; }

        protected override UniTask OnExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var store = GetStore<SpellGenerationStore>();
            if (!store.HasInactiveAnchor)
            {
                Outcome = new OfflineGenerationOutcome(0L, 0, 0);
                return UniTask.CompletedTask;
            }

            var generation = GetSystem<IConfigSystem>()
                .GetTable<TbSpellGeneration>()
                .Data;
            var nowUtcSeconds = GetSystem<ITimeSystem>().ServerTimeSeconds;
            var elapsedSeconds = ResolveElapsedSeconds(
                nowUtcSeconds,
                store.InactiveSinceUtcSeconds,
                generation.OfflineLimitSeconds);
            var intervalSeconds = Math.Max(
                store.ActiveIntervalSeconds,
                generation.MinimumIntervalSeconds);
            var totalProgressSeconds = store.CycleProgressSeconds + (double)elapsedSeconds;
            var generatedCount = checked(
                (int)Math.Floor(totalProgressSeconds / intervalSeconds));
            var remainingProgressSeconds = (float)(
                totalProgressSeconds - generatedCount * intervalSeconds);

            var generationSystem = GetSystem<SpellGenerationSystem>();
            var generatedSpells = new List<SpellType>(generatedCount);
            for (var index = 0; index < generatedCount; index++)
            {
                ct.ThrowIfCancellationRequested();
                generatedSpells.Add(generationSystem.SelectGeneratedSpell());
            }

            ct.ThrowIfCancellationRequested();
            store.CommitOfflineSettlement(remainingProgressSeconds, generatedSpells);
            var transferredCount = generationSystem.TransferPendingSpells(ct);

            Outcome = new OfflineGenerationOutcome(
                elapsedSeconds,
                generatedCount,
                transferredCount);
            return UniTask.CompletedTask;
        }

        private static long ResolveElapsedSeconds(
            long nowUtcSeconds,
            long inactiveSinceUtcSeconds,
            long offlineLimitSeconds)
        {
            if (nowUtcSeconds <= inactiveSinceUtcSeconds)
            {
                return 0L;
            }

            return inactiveSinceUtcSeconds < nowUtcSeconds - offlineLimitSeconds
                ? offlineLimitSeconds
                : nowUtcSeconds - inactiveSinceUtcSeconds;
        }
    }
}
