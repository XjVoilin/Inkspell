using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Persistence;
using July.Arch;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class InkspellSaveMigrationTests
    {
        private string _directory;
        private MemoryStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "InkspellMigration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _storage = new MemoryStorage();
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [UnityTest]
        public IEnumerator ImportsAllThreeFilesWithoutChangingBytesAndKeepsOriginals() => UniTask.ToCoroutine(async () =>
        {
            var keys = new[] { InkspellSaveMigration.SpellAssetsKey, InkspellSaveMigration.StageProgressionKey,
                InkspellSaveMigration.SpellGenerationKey };
            var data = MakeSave();
            foreach (var key in keys) File.WriteAllBytes(PathFor(key), data);
            await InkspellSaveMigration.MigrateAsync(_directory, _storage, CancellationToken.None);
            foreach (var key in keys)
            {
                Assert.That(Convert.FromBase64String(_storage.GetString("Save_" + key)), Is.EqualTo(data));
                Assert.That(File.ReadAllBytes(PathFor(key)), Is.EqualTo(data));
            }
            Assert.That(_storage.SaveCount, Is.EqualTo(3));
            await InkspellSaveMigration.MigrateAsync(_directory, _storage, CancellationToken.None);
            Assert.That(_storage.SaveCount, Is.EqualTo(3), "重复启动不得重新导入旧进度。");
        });

        [UnityTest]
        public IEnumerator MigratedProgressRestoresThroughTheSaveSystem() => UniTask.ToCoroutine(async () =>
        {
            File.WriteAllBytes(PathFor(InkspellSaveMigration.SpellAssetsKey), MakeSave("{\"Initialized\":true,\"MagicInk\":731,\"NextInstanceId\":19,\"Spells\":[]}"));
            File.WriteAllBytes(PathFor(InkspellSaveMigration.StageProgressionKey), MakeSave("{\"Initialized\":true,\"CurrentHighestStageId\":27}"));
            File.WriteAllBytes(PathFor(InkspellSaveMigration.SpellGenerationKey), MakeSave("{\"Initialized\":true,\"CycleProgressSeconds\":4.5,\"ActiveIntervalSeconds\":10,\"HasInactiveAnchor\":true,\"InactiveSinceUtcSeconds\":1789380000,\"PendingSpells\":[]}"));
            await InkspellSaveMigration.MigrateAsync(_directory, _storage, CancellationToken.None);
            var context = new ArchContext();
            try
            {
                context.RegisterSystem(new JsonSerializeSystem());
                context.RegisterSystem(new NoEncryptionSystem());
                var save = new KeyValueSaveSystem(_storage);
                context.RegisterSystem(save);
                var assets = new SpellAssetStore();
                var stages = new StageProgressionStore();
                var generation = new SpellGenerationStore();
                context.RegisterStore(save.Persist(assets, InkspellSaveMigration.SpellAssetsKey, SaveImportance.Important));
                context.RegisterStore(save.Persist(stages, InkspellSaveMigration.StageProgressionKey, SaveImportance.Important));
                context.RegisterStore(save.Persist(generation, InkspellSaveMigration.SpellGenerationKey, SaveImportance.Important));
                await context.InitializeAsync();
                Assert.That(assets.MagicInk, Is.EqualTo(731));
                Assert.That(stages.CurrentStageId, Is.EqualTo(27));
                Assert.That(generation.CycleProgressSeconds, Is.EqualTo(4.5f));
                Assert.That(generation.HasInactiveAnchor, Is.True);
                Assert.That(generation.InactiveSinceUtcSeconds, Is.EqualTo(1789380000L));
            }
            finally { context.Shutdown(); }
        });

        [UnityTest]
        public IEnumerator ExistingPlatformSaveWinsEvenWhenLegacyFileIsBroken() => UniTask.ToCoroutine(async () =>
        {
            _storage.SetString("Save_" + InkspellSaveMigration.SpellAssetsKey, "new-progress");
            File.WriteAllBytes(PathFor(InkspellSaveMigration.SpellAssetsKey), new byte[] { 99 });
            await InkspellSaveMigration.MigrateAsync(_directory, _storage, CancellationToken.None);
            Assert.That(_storage.GetString("Save_" + InkspellSaveMigration.SpellAssetsKey), Is.EqualTo("new-progress"));
            Assert.That(_storage.SaveCount, Is.Zero);
        });

        [UnityTest]
        public IEnumerator MissingFilesLeaveNewPlayerUntouched() => UniTask.ToCoroutine(async () =>
        {
            await InkspellSaveMigration.MigrateAsync(_directory, _storage, CancellationToken.None);
            Assert.That(_storage.Values, Is.Empty);
        });

        [UnityTest]
        public IEnumerator InvalidLegacyEnvelopeFailsBeforeWriting() => UniTask.ToCoroutine(async () =>
        {
            File.WriteAllBytes(PathFor(InkspellSaveMigration.SpellAssetsKey), new byte[] { 1, 20, 0, 0, 0, 0 });
            var error = await CaptureFailure();
            Assert.That(error, Is.TypeOf<InvalidDataException>());
            Assert.That(_storage.Values, Is.Empty);
        });

        [UnityTest]
        public IEnumerator FailedWriteIsReportedAndOriginalFileSurvives() => UniTask.ToCoroutine(async () =>
        {
            File.WriteAllBytes(PathFor(InkspellSaveMigration.SpellAssetsKey), MakeSave());
            _storage.FailWrite = true;
            Assert.That(await CaptureFailure(), Is.TypeOf<IOException>());
            Assert.That(File.Exists(PathFor(InkspellSaveMigration.SpellAssetsKey)), Is.True);
            Assert.That(_storage.Values, Is.Empty);
        });

        [UnityTest]
        public IEnumerator CorruptReadbackRollsBackNewKeyAndRetainsOriginal() => UniTask.ToCoroutine(async () =>
        {
            File.WriteAllBytes(PathFor(InkspellSaveMigration.SpellAssetsKey), MakeSave());
            _storage.CorruptReadback = true;
            Assert.That(await CaptureFailure(), Is.TypeOf<IOException>());
            Assert.That(_storage.Values, Is.Empty);
            Assert.That(File.Exists(PathFor(InkspellSaveMigration.SpellAssetsKey)), Is.True);
        });

        [Test]
        public void CancellationDoesNotWrite()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => InkspellSaveMigration.MigrateAsync(
                _directory, _storage, cancellation.Token).GetAwaiter().GetResult());
            Assert.That(_storage.Values, Is.Empty);
        }

        private async UniTask<Exception> CaptureFailure()
        {
            try { await InkspellSaveMigration.MigrateAsync(_directory, _storage, CancellationToken.None); }
            catch (Exception error) { return error; }
            return null;
        }

        private string PathFor(string key) => Path.Combine(_directory, key + ".dat");
        private static byte[] MakeSave(string json = "{}")
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(json);
            var bytes = new byte[payload.Length + 5];
            bytes[0] = 1;
            Array.Copy(BitConverter.GetBytes(payload.Length), 0, bytes, 1, 4);
            Array.Copy(payload, 0, bytes, 5, payload.Length);
            return bytes;
        }

        private sealed class MemoryStorage : IKeyValueStore
        {
            internal readonly Dictionary<string, string> Values = new();
            internal int SaveCount;
            internal bool FailWrite;
            internal bool CorruptReadback;
            public bool HasKey(string key) => Values.ContainsKey(key);
            public string GetString(string key) => CorruptReadback ? "corrupt" : Values[key];
            public void SetString(string key, string value)
            {
                if (FailWrite) throw new IOException("Storage quota exhausted");
                Values[key] = value;
            }
            public void DeleteKey(string key) => Values.Remove(key);
            public void Save() => SaveCount++;
        }
    }
}
