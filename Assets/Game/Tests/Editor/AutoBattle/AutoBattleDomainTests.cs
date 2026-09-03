using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AutoBattleDomainTests
    {
        [Test]
        public void SimulationClock_ProducesSameStepsAtThirtyAndSixtyFps()
        {
            var thirtyFpsClock = new BattleSimulationClock();
            var sixtyFpsClock = new BattleSimulationClock();
            var thirtyFpsSteps = 0;
            var sixtyFpsSteps = 0;

            for (var frame = 0; frame < 30; frame++)
            {
                thirtyFpsSteps += thirtyFpsClock.TakeSteps(1f / 30f);
            }

            for (var frame = 0; frame < 60; frame++)
            {
                sixtyFpsSteps += sixtyFpsClock.TakeSteps(1f / 60f);
            }

            Assert.That(thirtyFpsSteps, Is.EqualTo(30));
            Assert.That(sixtyFpsSteps, Is.EqualTo(thirtyFpsSteps));
        }

        [Test]
        public void SimulationClock_CapsCatchUpAfterLongFrame()
        {
            var clock = new BattleSimulationClock();

            var steps = clock.TakeSteps(10f);

            Assert.That(steps, Is.EqualTo(10));
        }

        [Test]
        public void Cooldown_KeepsCastDurationWhileRemainingTimeTicks()
        {
            var cooldown = new SpellSlotCooldown(2);

            cooldown.Set(4f);
            cooldown.Tick(1.25f);

            Assert.That(cooldown.EquipmentSlot, Is.EqualTo(2));
            Assert.That(cooldown.TotalSeconds, Is.EqualTo(4f));
            Assert.That(cooldown.RemainingSeconds, Is.EqualTo(2.75f));
        }

        [Test]
        public void Shield_RecastKeepsLargerValueAndRefreshesDuration()
        {
            var book = new BattleBook(100f);
            book.ApplyShield(40f, 3f);
            book.Tick(1f);

            book.ApplyShield(20f, 3f);

            Assert.That(book.Shield, Is.EqualTo(40f));
            Assert.That(book.ShieldRemainingSeconds, Is.EqualTo(3f));
        }

        [Test]
        public void ChainTargets_UseDistanceThenPathThenRuntimeId()
        {
            var enemies = new EnemyRoster();
            var primary = enemies.Spawn(default, 10f, 0.5f, 1f);
            var closerToBook = enemies.Spawn(default, 10f, 0.25f, 1f);
            enemies.Spawn(default, 10f, 0.75f, 1f);

            var selected = enemies.SelectChainTargets(primary, 2, 0.5f);

            Assert.That(
                selected,
                Is.EqualTo(new List<long> { primary.RuntimeId, closerToBook.RuntimeId }));
        }

        [Test]
        public void BattleRun_CanOnlyCompleteOnce()
        {
            var run = new BattleRun(7, 3, 100f, 4);
            var outcome = run.Complete(true);

            Assert.That(outcome.BattleRunId, Is.EqualTo(7));
            Assert.That(outcome.StageId, Is.EqualTo(3));
            Assert.That(outcome.Victory, Is.True);
            Assert.That(run.IsRunning, Is.False);
            Assert.Throws<InvalidOperationException>(() => run.Complete(false));
        }
    }
}
