using System;
using System.Collections.Generic;
using cfg;
using July.Arch;

namespace Game
{
    internal sealed class SpellGenerationStore : StoreBase<SpellGenerationStoreData>
    {
        internal int PendingCount => Data.PendingSpells.Count;
        internal float CycleProgressSeconds => Data.CycleProgressSeconds;
        internal float ActiveIntervalSeconds => Data.ActiveIntervalSeconds;
        internal long InactiveSinceUtcSeconds => Data.InactiveSinceUtcSeconds;
        internal bool HasInactiveAnchor => Data.HasInactiveAnchor;

        internal void Initialize(float initialIntervalSeconds)
        {
            if (Data.Initialized)
            {
                return;
            }

            Data.Initialized = true;
            Data.PendingSpells.Clear();
            Data.CycleProgressSeconds = 0f;
            Data.ActiveIntervalSeconds = initialIntervalSeconds;
            Data.InactiveSinceUtcSeconds = 0L;
            Data.HasInactiveAnchor = false;
            CommitChange();
        }

        internal void CommitOnlineProgress(
            float cycleProgressSeconds,
            IReadOnlyList<SpellType> generatedSpells)
        {
            Data.CycleProgressSeconds = cycleProgressSeconds;
            AppendGeneratedSpells(generatedSpells);
            CommitChange();
        }

        internal void UpdateInterval(float intervalSeconds)
        {
            if (Data.ActiveIntervalSeconds == intervalSeconds)
            {
                return;
            }

            Data.ActiveIntervalSeconds = intervalSeconds;
            CommitChange();
        }

        internal void RecordInactive(long utcSeconds)
        {
            if (Data.HasInactiveAnchor)
            {
                return;
            }

            Data.InactiveSinceUtcSeconds = utcSeconds;
            Data.HasInactiveAnchor = true;
            CommitChange();
        }

        internal void CommitOfflineSettlement(
            float cycleProgressSeconds,
            IReadOnlyList<SpellType> generatedSpells)
        {
            Data.CycleProgressSeconds = cycleProgressSeconds;
            AppendGeneratedSpells(generatedSpells);
            Data.InactiveSinceUtcSeconds = 0L;
            Data.HasInactiveAnchor = false;
            CommitChange();
        }

        internal bool TryPeekPendingSpell(out SpellType type)
        {
            if (Data.PendingSpells.Count == 0)
            {
                type = default;
                return false;
            }

            type = Data.PendingSpells[0];
            return true;
        }

        internal void ConfirmTransferredSpell(SpellType type)
        {
            if (Data.PendingSpells.Count == 0 || Data.PendingSpells[0] != type)
            {
                throw new InvalidOperationException("待领取法术队首与已接收法术不一致。");
            }

            Data.PendingSpells.RemoveAt(0);
            CommitChange();
        }

        private void AppendGeneratedSpells(IReadOnlyList<SpellType> generatedSpells)
        {
            for (var index = 0; index < generatedSpells.Count; index++)
            {
                Data.PendingSpells.Add(generatedSpells[index]);
            }
        }

        private void CommitChange()
        {
            MarkDirty();
            Publish(new SpellGenerationChangedEvent());
        }
    }
}
