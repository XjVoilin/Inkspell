using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Logging;
using July.Persistence;
using UnityEngine;

namespace Game
{
    /// <summary>首次接入平台存储时导入旧文件，保留原文件作为备份，绝不覆盖新存档。</summary>
    internal static class InkspellSaveMigration
    {
        internal const string SpellAssetsKey = "inkspell.spell-assets";
        internal const string StageProgressionKey = "inkspell.stage-progression";
        internal const string SpellGenerationKey = "inkspell.spell-generation";

        internal static UniTask MigrateAsync(CancellationToken ct) => MigrateAsync(
            Path.Combine(Application.persistentDataPath, "Save"), new PlatformPreferencesKeyValueStore(), ct);

        internal static async UniTask MigrateAsync(string legacyDirectory, IKeyValueStore target, CancellationToken ct)
        {
            var serializer = new JsonSerializeSystem();
            await ImportAsync<SpellAssetStoreData>(SpellAssetsKey, legacyDirectory, target, serializer, ct);
            await ImportAsync<StageProgressionStoreData>(StageProgressionKey, legacyDirectory, target, serializer, ct);
            await ImportAsync<SpellGenerationStoreData>(SpellGenerationKey, legacyDirectory, target, serializer, ct);
        }

        private static async UniTask ImportAsync<T>(string key, string directory, IKeyValueStore target,
            JsonSerializeSystem serializer, CancellationToken ct) where T : class
        {
            ct.ThrowIfCancellationRequested();
            var storageKey = "Save_" + key;
            if (target.HasKey(storageKey)) return;
            var path = Path.Combine(directory, key + ".dat");
            if (!File.Exists(path)) return;

            var bytes = await File.ReadAllBytesAsync(path, ct);
            // 文件是外部边界：损坏或不支持的存档应中止启动，不能变成空白进度。
            if (bytes.Length < 6 || bytes[0] != 1 || BitConverter.ToInt32(bytes, 1) != bytes.Length - 5)
                throw new InvalidDataException($"旧存档格式无效，未迁移：{path}");
            var payload = new byte[bytes.Length - 5];
            Array.Copy(bytes, 5, payload, 0, payload.Length);
            if (serializer.Deserialize<T>(payload) == null)
                throw new InvalidDataException($"旧存档内容为空，未迁移：{path}");

            ct.ThrowIfCancellationRequested();
            var encoded = Convert.ToBase64String(bytes);
            try
            {
                target.SetString(storageKey, encoded);
                target.Save();
                if (target.GetString(storageKey) != encoded)
                    throw new IOException($"平台存档写入校验失败，旧文件已保留：{path}");
            }
            catch
            {
                // 外部存储可能在部分写入后失败；撤销新键，避免下次把不完整导入当成已有进度。
                target.DeleteKey(storageKey);
                target.Save();
                throw;
            }
            JLogger.Log($"[SaveMigration] 已导入 {key}，旧文件保留在 {path}");
        }
    }
}
