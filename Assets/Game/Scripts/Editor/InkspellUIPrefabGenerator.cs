using System;
using System.Collections.Generic;
using Game;
using July.Localization;
using July.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static partial class InkspellUIPrefabGenerator
    {
        private const string PrefabDirectory = "Assets/Game/Res/Prefabs";
        internal const string MainPrefabPath = PrefabDirectory + "/UI/MainWindow/MainWindow.prefab";
        private const string OfflinePrefabPath = PrefabDirectory + "/UIOfflineRewardWindow.prefab";

        private static readonly Color Paper = new(0.96f, 0.92f, 0.82f, 1f);
        private static readonly Color Ink = new(0.11f, 0.09f, 0.08f, 1f);
        private static readonly Color Accent = new(0.78f, 0.32f, 0.18f, 1f);

        [MenuItem("July/Inkspell/Generate Required UI Prefabs")]
        public static void Generate()
        {
            EnsureDirectory();
            GenerateMainWindow();
            GenerateOfflineRewardWindow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedPrefabs();
            Debug.Log("[InkspellUIPrefabGenerator] Required UI prefabs generated and validated.");
        }

        private static void GenerateOfflineRewardWindow()
        {
            var root = CreateRect("UIOfflineRewardWindow", null, Vector2.zero, Vector2.one);
            try
            {
                AddImage(root.gameObject, new Color(0f, 0f, 0f, 0.32f), true);
                var window = root.gameObject.AddComponent<UIOfflineRewardWindow>();
                var card = CreateRect(
                    "RewardCard",
                    root,
                    new Vector2(0.15f, 0.30f),
                    new Vector2(0.85f, 0.70f));
                AddImage(card.gameObject, Paper, false);

                var elapsed = CreateLocalizedText(
                    "Elapsed",
                    card,
                    new Vector2(0.08f, 0.68f),
                    new Vector2(0.92f, 0.86f),
                    34f,
                    TextAlignmentOptions.Center,
                    Ink);
                var generated = CreateLocalizedText(
                    "Generated",
                    card,
                    new Vector2(0.08f, 0.48f),
                    new Vector2(0.92f, 0.66f),
                    34f,
                    TextAlignmentOptions.Center,
                    Ink);
                var transferred = CreateLocalizedText(
                    "Transferred",
                    card,
                    new Vector2(0.08f, 0.28f),
                    new Vector2(0.92f, 0.46f),
                    34f,
                    TextAlignmentOptions.Center,
                    Ink);
                var buttonRoot = CreateRect(
                    "ContinueButton",
                    card,
                    new Vector2(0.25f, 0.06f),
                    new Vector2(0.75f, 0.24f));
                AddImage(buttonRoot.gameObject, Accent, true);
                var button = buttonRoot.gameObject.AddComponent<UISmartButton>();
                button.enableSound = false;
                var continueText = CreateLocalizedText(
                    "ContinueText",
                    buttonRoot,
                    Vector2.zero,
                    Vector2.one,
                    32f,
                    TextAlignmentOptions.Center,
                    Color.white);

                SetObject(window, "_elapsedText", elapsed);
                SetObject(window, "_generatedText", generated);
                SetObject(window, "_transferredText", transferred);
                SetObject(window, "_continueButton", button);
                SetObject(window, "_continueText", continueText);
                SavePrefab(root.gameObject, OfflinePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        private static UIProgressBar CreateProgressBar(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color background,
            Color fill)
        {
            var root = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            AddImage(root.gameObject, background, false);
            var mask = root.gameObject.AddComponent<RectMask2D>();
            var progress = root.gameObject.AddComponent<UIProgressBar>();
            var fillRoot = CreateRect("Fill", root, Vector2.zero, Vector2.one);
            AddImage(fillRoot.gameObject, fill, false);
            SetObject(progress, "_mask", mask);
            return progress;
        }

        private static UILocalizedText CreateLocalizedText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            var text = CreateTMP(name, parent, anchorMin, anchorMax, fontSize, alignment, color);
            return text.gameObject.AddComponent<UILocalizedText>();
        }

        private static TextMeshProUGUI CreateTMP(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            var root = CreateRect(name, parent, anchorMin, anchorMax);
            var text = root.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = GetUIFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private static Text CreateLegacyText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            Color color)
        {
            var root = CreateRect(name, parent, anchorMin, anchorMax);
            var text = root.gameObject.AddComponent<Text>();
            text.font = GetUIFontSource();
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateIndicator(string name, Transform parent, Color color)
        {
            var root = CreateRect(name, parent, Vector2.zero, Vector2.one);
            AddImage(root.gameObject, color, false);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2? offsetMin = null,
            Vector2? offsetMax = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image AddImage(GameObject target, Color color, bool raycastTarget)
        {
            var image = target.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ??
                           throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjects<T>(UnityEngine.Object target, string propertyName, IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ??
                           throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to save prefab: {path}");
            }
        }

        private static void ValidateGeneratedPrefabs()
        {
            var main = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            var offline = AssetDatabase.LoadAssetAtPath<GameObject>(OfflinePrefabPath);
            if (main == null || main.GetComponent<UIInkspellMainWindow>() == null)
            {
                throw new InvalidOperationException("Main window prefab is missing its UIView component.");
            }

            if (offline == null || offline.GetComponent<UIOfflineRewardWindow>() == null)
            {
                throw new InvalidOperationException("Offline reward prefab is missing its UIView component.");
            }

            ValidateReferences(main.GetComponent<UIInkspellMainWindow>());
            ValidateReferences(offline.GetComponent<UIOfflineRewardWindow>());

            foreach (var component in main.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is UIBattlefieldGameView ||
                    component is UIEnemyBattleGameView ||
                    component is UISpellBoardGameView ||
                    component is UIEquipmentBarGameView ||
                    component is UISpellCardGameView)
                {
                    ValidateReferences(component);
                }
            }
        }

        private static void ValidateReferences(UnityEngine.Object component)
        {
            var serialized = new SerializedObject(component);
            var iterator = serialized.GetIterator();
            if (!iterator.NextVisible(true))
            {
                return;
            }

            do
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                    iterator.objectReferenceValue == null &&
                    iterator.name != "m_Script")
                {
                    throw new InvalidOperationException(
                        $"Unbound reference: {component.GetType().Name}.{iterator.propertyPath}");
                }

                if (iterator.isArray && iterator.propertyType != SerializedPropertyType.String)
                {
                    for (var index = 0; index < iterator.arraySize; index++)
                    {
                        var element = iterator.GetArrayElementAtIndex(index);
                        if (element.propertyType == SerializedPropertyType.ObjectReference &&
                            element.objectReferenceValue == null)
                        {
                            throw new InvalidOperationException(
                                $"Unbound reference: {component.GetType().Name}.{iterator.propertyPath}[{index}]");
                        }
                    }
                }
            } while (iterator.NextVisible(false));
        }

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Res"))
            {
                throw new InvalidOperationException("Assets/Game/Res is missing.");
            }

            if (!AssetDatabase.IsValidFolder(PrefabDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Game/Res", "Prefabs");
            }
        }
    }
}
