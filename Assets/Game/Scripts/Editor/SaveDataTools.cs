using System.IO;
using UnityEditor;
using UnityEngine;

namespace CozyYard.Editor
{
    public static class SaveDataTools
    {
        [MenuItem("JulyGF/存档/打开本地缓存路径")]
        private static void OpenPersistentDataPath()
        {
            var path = Application.persistentDataPath;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("JulyGF/存档/打开旧版文件存档目录")]
        private static void OpenSaveDataPath()
        {
            var path = Path.Combine(Application.persistentDataPath, "Save");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("JulyGF/存档/清除所有存档")]
        private static void DeleteAllSaveData()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("请先退出播放模式，避免内存中的游戏进度再次写回存档。");
            var path = Path.Combine(Application.persistentDataPath, "Save");
            if (!EditorUtility.DisplayDialog("清除存档", "确定要清除 Inkspell 的法术、关卡与生成进度，以及对应旧文件和备份吗？此操作不可撤销。", "确定", "取消"))
                return;
            foreach (var key in new[] { "inkspell.spell-assets", "inkspell.stage-progression", "inkspell.spell-generation" })
            {
                PlayerPrefs.DeleteKey("Save_" + key);
                File.Delete(Path.Combine(path, key + ".dat"));
                File.Delete(Path.Combine(path, "Backup", key + ".dat.bak"));
            }
            PlayerPrefs.Save();
            Debug.Log("[SaveDataTools] 已清除 Inkspell 平台存档与对应旧文件，其他偏好设置保留。");
        }
    }
}
