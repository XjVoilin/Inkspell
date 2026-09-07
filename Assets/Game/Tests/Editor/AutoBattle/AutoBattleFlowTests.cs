using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Config;
using July.Events;
using NUnit.Framework;
using SimpleJSON;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class AutoBattleFlowTests
    {
        private BattleFixture _game;

        [TearDown]
        public void TearDown() => _game?.Dispose();

        [Test]
        public void PreCanceledRequest_DoesNotCreateOrPublishBattle()
        {
            _game = new BattleFixture();
            var changes = 0;
            _game.Subscribe<BattleStateChangedEvent>(_ => changes++);
            var request = _game.Battle.RunChallengeAsync(1, new CancellationToken(true));
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
            Assert.That(_game.Battle.CurrentRun, Is.Null);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ConcurrentRequest_IsRejectedWithoutReplacingCurrentRun()
        {
            _game = new BattleFixture();
            using var cancellation = new CancellationTokenSource();
            var first = _game.Battle.RunChallengeAsync(1, cancellation.Token);
            var run = _game.Battle.CurrentRun;
            var second = _game.Battle.RunChallengeAsync(1);
            Assert.Throws<InvalidOperationException>(() => second.GetAwaiter().GetResult());
            Assert.That(_game.Battle.CurrentRun, Is.SameAs(run));
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => first.GetAwaiter().GetResult());
        }

        [Test]
        public void Cancellation_ClearsStateAndAllowsFreshChallenge()
        {
            _game = new BattleFixture();
            using var cancellation = new CancellationTokenSource();
            var first = _game.Battle.RunChallengeAsync(1, cancellation.Token);
            var old = _game.Battle.CurrentRun;
            _game.Tick();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => first.GetAwaiter().GetResult());
            Assert.That(old.IsRunning, Is.False);
            Assert.That(_game.Battle.CurrentRun, Is.Null);

            var second = _game.Battle.RunChallengeAsync(1);
            var current = _game.Battle.CurrentRun;
            Assert.That(current.BattleRunId, Is.GreaterThan(old.BattleRunId));
            Assert.That(current.SpawnElapsedSeconds, Is.Zero);
            Assert.That(current.Attacks, Is.Empty);
            Assert.That(current.Book.Health, Is.EqualTo(current.Book.MaxHealth));
            Assert.That(current.Cooldowns.Items[0].RemainingSeconds, Is.Zero);
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => second.GetAwaiter().GetResult());
        }

        [Test]
        public void CancelFromStateListener_CannotAdvanceReplacementRunInOldFrame()
        {
            _game = new BattleFixture();
            using var cancellation = new CancellationTokenSource();
            var first = _game.Battle.RunChallengeAsync(1, cancellation.Token);
            var old = _game.Battle.CurrentRun;
            UniTask<BattleOutcome> replacement = default;
            var replacing = false;
            _game.Subscribe<BattleStateChangedEvent>(_ =>
            {
                if (!replacing && old.SpawnElapsedSeconds > 0f)
                {
                    replacing = true;
                    cancellation.Cancel();
                }
                else if (replacing && _game.Battle.CurrentRun == null)
                {
                    replacement = _game.Battle.RunChallengeAsync(1);
                }
            });
            _game.Battle.OnUpdate(0.3f);
            Assert.Throws<OperationCanceledException>(() => first.GetAwaiter().GetResult());
            Assert.That(_game.Battle.CurrentRun, Is.Not.SameAs(old));
            Assert.That(_game.Battle.CurrentRun.SpawnElapsedSeconds, Is.Zero);
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => replacement.GetAwaiter().GetResult());
        }

        [Test]
        public void FocusResume_KeepsSameRunAndDiscardsBackgroundDelta()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            _game.Tick();
            var run = _game.Battle.CurrentRun;
            var elapsed = run.SpawnElapsedSeconds;
            var foreground = _game.Battle.ForegroundElapsedSeconds;
            _game.Battle.OnFocusChanged(false);
            _game.Battle.OnUpdate(600f);
            _game.Battle.OnFocusChanged(true);
            _game.Battle.OnUpdate(600f);
            Assert.That(_game.Battle.CurrentRun, Is.SameAs(run));
            Assert.That(run.SpawnElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(_game.Battle.ForegroundElapsedSeconds, Is.EqualTo(foreground));
            _game.Tick();
            Assert.That(run.SpawnElapsedSeconds, Is.EqualTo(elapsed + BattleSimulationClock.StepSeconds).Within(0.00001f));
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }

        [Test]
        public void FinalHit_WinsBeforeEnemyCanAttackBook()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            var run = _game.Battle.CurrentRun;
            var enemy = run.Enemies.Items[0];
            enemy.MoveTowards(0f, 100f, 1f);
            enemy.Tick(100f);
            run.Book.ApplyDamage(99f);
            run.AddAttack(
                spellType: SpellType.Fireball,
                targetEnemyIds: new[] { enemy.RuntimeId },
                targetPathPosition: 0f,
                travelSeconds: 0f,
                damage: 10000f,
                shield: 0f,
                effectRange: 0f,
                effectDurationSeconds: 0f,
                slowMultiplier: 1f);

            var observations = new List<string>();
            _game.Subscribe<BattleFactsEvent>(e =>
            {
                foreach (var fact in e.Facts) observations.Add(fact.Kind.ToString());
            });
            _game.Subscribe<BattleStateChangedEvent>(_ =>
                observations.Add(run.IsRunning ? "running" : "final"));
            _game.Subscribe<BattleChallengeEndedEvent>(_ => observations.Add("ended"));
            _game.Tick();
            var outcome = request.GetAwaiter().GetResult();
            Assert.That(outcome.Victory, Is.True);
            Assert.That(run.Book.Health, Is.EqualTo(1f));
            Assert.That(observations, Is.EqualTo(new[]
            {
                "EnemyDamaged", "SpellImpact", "EnemyDied", "final", "ended",
            }));
            Assert.That(_game.Battle.CurrentRun, Is.SameAs(run));
            _game.Tick();
            Assert.That(observations.Count, Is.EqualTo(5));
        }

        [Test]
        public void EmptyFieldWithFutureSpawn_DoesNotWinOrCastShield()
        {
            _game = new BattleFixture(delayedSpawn: true);
            Assert.That(_game.Assets.TryReceiveGeneratedSpell(SpellType.Shield), Is.True);
            var shield = _game.Assets.GetCraftingAreaSpells()[0];
            Assert.That(_game.Assets.TryEquip(shield.InstanceId, 0), Is.True);
            var request = _game.Battle.RunChallengeAsync(1);
            _game.Tick();
            var run = _game.Battle.CurrentRun;
            Assert.That(run.Enemies.Count, Is.Zero);
            Assert.That(run.IsRunning, Is.True);
            Assert.That(run.Attacks, Is.Empty);
            Assert.That(run.Cooldowns.Items[0].RemainingSeconds, Is.Zero);
            for (var i = 0; i < 31; i++) _game.Tick();
            Assert.That(run.Enemies.Count, Is.EqualTo(1));
            Assert.That(run.Attacks.Count, Is.EqualTo(1));
            Assert.That(run.Attacks[0].SpellType, Is.EqualTo(SpellType.Shield));
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }

        [Test]
        public void UpgradeAndReplacement_OnlyChangeNextCast()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            _game.Tick();
            var run = _game.Battle.CurrentRun;
            var first = run.Attacks[0];
            var cooldown = run.Cooldowns.Items[0];
            var remaining = cooldown.RemainingSeconds;
            var frozenDamage = first.Damage;
            _game.Assets.TryGetEquippedSpell(0, out var original);
            _game.Assets.CommitUpgrade(original.InstanceId, 0);
            Assert.That(first.Damage, Is.EqualTo(frozenDamage));

            Assert.That(_game.Assets.TryReceiveGeneratedSpell(SpellType.Fireball), Is.True);
            var incoming = _game.Assets.GetCraftingAreaSpells()[0];
            _game.Assets.CommitUpgrade(incoming.InstanceId, 0);
            Assert.That(_game.Assets.TryEquip(incoming.InstanceId, 0), Is.True);
            Assert.That(cooldown.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(first.Damage, Is.EqualTo(frozenDamage));
            for (var i = 0; i < 46; i++) _game.Tick();
            Assert.That(run.Attacks.Count, Is.EqualTo(1));
            Assert.That(run.Attacks[0].AttackId, Is.GreaterThan(first.AttackId));
            Assert.That(run.Attacks[0].Damage, Is.EqualTo(frozenDamage * 1.15f).Within(0.00001f));
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }

        [Test]
        public void LongFrame_PublishesCastBeforeItsImpact()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            var facts = new List<BattleFactKind>();
            var changes = 0;
            _game.Subscribe<BattleStateChangedEvent>(_ => changes++);
            _game.Subscribe<BattleFactsEvent>(e =>
            {
                foreach (var fact in e.Facts) facts.Add(fact.Kind);
            });
            _game.Battle.OnUpdate(0.3f);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(facts, Is.EqualTo(new[]
            {
                BattleFactKind.SpellCast, BattleFactKind.EnemyDamaged, BattleFactKind.SpellImpact,
            }));
            Assert.That(_game.Battle.CurrentRun.Attacks, Is.Empty);
            _game.Tick();
            Assert.That(facts.Count, Is.EqualTo(3), "已交付事实不能在下一帧重复播放");
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }

        [Test]
        public void NoSimulationStep_DoesNotPublishStateOrFacts()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            var notifications = 0;
            _game.Subscribe<BattleStateChangedEvent>(_ => notifications++);
            _game.Subscribe<BattleFactsEvent>(_ => notifications++);
            _game.Battle.OnUpdate(0f);
            Assert.That(notifications, Is.Zero);
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }

        [Test]
        public void CancelFromFactsListener_KeepsBatchAndReplacementIndependent()
        {
            _game = new BattleFixture();
            using var cancellation = new CancellationTokenSource();
            var request = _game.Battle.RunChallengeAsync(1, cancellation.Token);
            var old = _game.Battle.CurrentRun;
            IReadOnlyList<BattleFact> oldFacts = null;
            UniTask<BattleOutcome> replacement = default;
            var changes = 0;
            _game.Subscribe<BattleStateChangedEvent>(_ => changes++);
            _game.Subscribe<BattleFactsEvent>(e =>
            {
                if (e.BattleRunId != old.BattleRunId) return;
                oldFacts = e.Facts;
                cancellation.Cancel();
                replacement = _game.Battle.RunChallengeAsync(1);
            });
            _game.Battle.OnUpdate(0.3f);
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
            Assert.That(changes, Is.EqualTo(2), "只有取消清理和新局初始状态");
            Assert.That(oldFacts.Count, Is.EqualTo(3));
            Assert.That(oldFacts[0].Kind, Is.EqualTo(BattleFactKind.SpellCast));
            Assert.That(_game.Battle.CurrentRun.SpawnElapsedSeconds, Is.Zero);
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => replacement.GetAwaiter().GetResult());
        }

        [Test]
        public void ShieldAppliedAndBrokenInOneFrame_PreservesBothFacts()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            var run = _game.Battle.CurrentRun;
            var enemy = run.Enemies.Items[0];
            enemy.MoveTowards(0f, 100f, 1f);
            enemy.Tick(100f);
            run.AddAttack(SpellType.Shield, Array.Empty<long>(), 0f, 0f, 0f, 1f, 0f, 10f, 1f);
            var facts = new List<BattleFactKind>();
            _game.Subscribe<BattleFactsEvent>(e =>
            {
                foreach (var fact in e.Facts) facts.Add(fact.Kind);
            });
            _game.Battle.OnUpdate(0.3f);
            Assert.That(run.Book.Shield, Is.Zero);
            Assert.That(facts.IndexOf(BattleFactKind.ShieldApplied), Is.GreaterThanOrEqualTo(0));
            Assert.That(facts.IndexOf(BattleFactKind.ShieldBroken),
                Is.GreaterThan(facts.IndexOf(BattleFactKind.ShieldApplied)));
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
        }

        [Test]
        public void SimulationFault_ReachesCallerAndReleasesRun()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            var old = _game.Battle.CurrentRun;
            _game.Tables.TbStageBattle.DataMap.Remove(1);
            _game.Tick();
            Assert.Throws<KeyNotFoundException>(() => request.GetAwaiter().GetResult());
            Assert.That(old.IsRunning, Is.False);
            Assert.That(_game.Battle.CurrentRun, Is.Null);
        }

        [Test]
        public void ThrowingEndListener_CannotLeaveCompletedBattleOccupied()
        {
            _game = new BattleFixture(emptyStages: true);
            var previous = EventBus.ErrorHandler;
            var fault = new InvalidOperationException("presentation fault");
            EventBus.ErrorHandler = exception => throw exception;
            try
            {
                _game.Subscribe<BattleChallengeEndedEvent>(_ => throw fault);
                var request = _game.Battle.RunChallengeAsync(1);
                Assert.That(Assert.Throws<InvalidOperationException>(() => _game.Tick()), Is.SameAs(fault));
                Assert.That(request.GetAwaiter().GetResult().Victory, Is.True);
                using var cancellation = new CancellationTokenSource();
                var next = _game.Battle.RunChallengeAsync(1, cancellation.Token);
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => next.GetAwaiter().GetResult());
            }
            finally
            {
                EventBus.ErrorHandler = previous;
            }
        }

        [Test]
        public void Progression_SubmitsVictoryOnceAndRejectsDuplicate()
        {
            _game = new BattleFixture(emptyStages: true);
            var request = _game.Battle.RunChallengeAsync(1);
            _game.Tick();
            var result = request.GetAwaiter().GetResult();
            _game.Progress.CommitChallenge(result);
            Assert.That(_game.ProgressStore.CurrentStageId, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => _game.Progress.CommitChallenge(result));
            Assert.That(_game.ProgressStore.CurrentStageId, Is.EqualTo(2));
        }

        [Test]
        public void MaximumStage_VictoryDoesNotAdvanceOrNotifyProgress()
        {
            _game = new BattleFixture(emptyStages: true, startingStage: 10);
            var changes = 0;
            _game.Subscribe<StageProgressChangedEvent>(_ => changes++);
            var request = _game.Battle.RunChallengeAsync(10);
            _game.Tick();
            _game.Progress.CommitChallenge(request.GetAwaiter().GetResult());
            Assert.That(_game.ProgressStore.CurrentStageId, Is.EqualTo(10));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void Defeat_DoesNotAdvanceAndNextAttemptRestoresBook()
        {
            _game = new BattleFixture();
            var request = _game.Battle.RunChallengeAsync(1);
            _game.Battle.CurrentRun.Book.ApplyDamage(100f);
            _game.Tick();
            var outcome = request.GetAwaiter().GetResult();
            Assert.That(outcome.Victory, Is.False);
            _game.Progress.CommitChallenge(outcome);
            Assert.That(_game.ProgressStore.CurrentStageId, Is.EqualTo(1));
            var next = _game.Battle.RunChallengeAsync(1);
            Assert.That(_game.Battle.CurrentRun.Book.Health, Is.EqualTo(100f));
            _game.Context.Shutdown();
            Assert.Throws<OperationCanceledException>(() => next.GetAwaiter().GetResult());
        }

        [UnityTest]
        public IEnumerator ContinuousChallenges_PauseOnBackgroundAndStopDuringFeedback()
        {
            _game = new BattleFixture(emptyStages: true);
            _game.Progress.StartContinuousChallenges();
            _game.Tick();
            yield return null;
            Assert.That(_game.ProgressStore.CurrentStageId, Is.EqualTo(2));
            var finished = _game.Battle.CurrentRun;
            _game.Battle.OnFocusChanged(false);
            _game.Battle.OnUpdate(600f);
            yield return null;
            Assert.That(_game.Battle.CurrentRun, Is.SameAs(finished));

            _game.Battle.OnFocusChanged(true);
            _game.Battle.OnUpdate(600f);
            yield return null;
            Assert.That(_game.Battle.CurrentRun, Is.SameAs(finished));
            for (var frame = 0; frame < 35; frame++)
            {
                _game.Tick();
                yield return null;
            }
            Assert.That(_game.Battle.CurrentRun.BattleRunId, Is.GreaterThan(finished.BattleRunId));
            _game.Progress.StopContinuousChallenges();
            yield return null;
            Assert.That(_game.Battle.CurrentRun, Is.Null);
            var stage = _game.ProgressStore.CurrentStageId;
            for (var i = 0; i < 40; i++) _game.Tick();
            yield return null;
            Assert.That(_game.Battle.CurrentRun, Is.Null);
            Assert.That(_game.ProgressStore.CurrentStageId, Is.EqualTo(stage));
        }

        private sealed class BattleFixture : IDisposable
        {
            internal readonly ArchContext Context = new();
            internal readonly SpellAssetStore Assets = new();
            internal readonly StageProgressionStore ProgressStore = new();
            internal readonly AutoBattleSystem Battle = new();
            internal readonly StageProgressionSystem Progress = new();
            internal readonly Tables Tables;

            internal BattleFixture(bool emptyStages = false, bool delayedSpawn = false, int startingStage = 1)
            {
                Tables = new Tables(name =>
                {
                    var json = JSON.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Res/Configs", name + ".json")));
                    if (name == "tbstagebattle")
                    {
                        foreach (var stage in json.Children)
                            stage["spawns"] = JSON.Parse(emptyStages ? "[]" :
                                "[{\"enemyType\":1,\"spawnTimeSeconds\":" + (delayedSpawn ? "1" : "0") + "}]");
                    }
                    if (name == "tbenemy")
                    {
                        foreach (var enemy in json.Children)
                        {
                            enemy["maxHealth"] = 1000;
                            enemy["moveSpeedPerSecond"] = 0;
                        }
                    }
                    return json;
                });
                var configs = new Dictionary<Type, object>();
                Tables.RegisterTo(configs);
                var config = new ConfigSystem();
                config.SetMainProvider(new DictionaryConfigProvider(configs));
                Context.RegisterSystem(config);
                Context.RegisterStore(Assets);
                Context.RegisterStore(ProgressStore);
                Assets.Initialize(Tables.TbSpellAssetRule.Data);
                ProgressStore.ReplaceData(new StageProgressionStoreData
                {
                    Initialized = true,
                    CurrentHighestStageId = startingStage,
                });
                Context.RegisterSystem(Battle);
                Context.RegisterSystem(Progress);
                Context.InitializeAsync().GetAwaiter().GetResult();
                Battle.OnFocusChanged(true);
                Battle.OnUpdate(0f);
            }

            internal void Subscribe<T>(Action<T> handler) => Context.Event.Subscribe(handler, this);
            internal void Tick() => Battle.OnUpdate(BattleSimulationClock.StepSeconds);
            public void Dispose() => Context.Shutdown();
        }
    }
}
