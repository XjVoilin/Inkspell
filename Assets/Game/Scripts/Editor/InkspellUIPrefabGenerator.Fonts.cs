using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static partial class InkspellUIPrefabGenerator
    {
        private const string UIFontSourcePath = "Assets/Game/Arts/Fonts/LXGWWenKai-Regular.ttf";
        private const string UIFontPath = "Assets/Game/Arts/Fonts/LXGW WenKai SDF.asset";

        private static Font GetUIFontSource() => AssetDatabase.LoadAssetAtPath<Font>(UIFontSourcePath) ??
            throw new InvalidOperationException($"UI font source is missing: {UIFontSourcePath}");

        private static TMP_FontAsset GetUIFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIFontPath);
            if (font != null) return font;

            font = TMP_FontAsset.CreateFontAsset(GetUIFontSource(), 48, 5,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
            font.name = "LXGW WenKai SDF";
            font.material.name = font.name + " Material";
            var characters = new string((string.Concat(Enumerable.Range(32, 95).Select(c => (char)c)) +
                File.ReadAllText("Assets/Game/Res/Configs/tblanguage.json"))
                .Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException($"UI font is missing localization glyphs: {missing}");

            AssetDatabase.CreateAsset(font, UIFontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
                AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void ApplyUIFont(GameObject root)
        {
            var font = GetUIFont();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
        }

        [MenuItem("July/Inkspell/Apply Project UI Font")]
        public static void ApplyProjectUIFont()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDirectory }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyUIFont(prefab);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }

            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = GetUIFont();
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
