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
    public static class InkspellUIPrefabGenerator
    {
        private const string PrefabDirectory = "Assets/Game/Res/Prefabs";
        private const string MainPrefabPath = PrefabDirectory + "/UIInkspellMainWindow.prefab";
        private const string OfflinePrefabPath = PrefabDirectory + "/UIOfflineRewardWindow.prefab";

        private static readonly Color Paper = new(0.96f, 0.92f, 0.82f, 1f);
        private static readonly Color Ink = new(0.11f, 0.09f, 0.08f, 1f);
        private static readonly Color Panel = new(0.84f, 0.78f, 0.65f, 0.96f);
        private static readonly Color Slot = new(0.25f, 0.20f, 0.16f, 0.85f);
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

        private static void GenerateMainWindow()
        {
            var root = CreateRect("UIInkspellMainWindow", null, Vector2.zero, Vector2.one);
            try
            {
                AddImage(root.gameObject, Paper, false);
                var window = root.gameObject.AddComponent<UIInkspellMainWindow>();

                var statusRoot = CreateRect(
                    "Status",
                    root,
                    new Vector2(0f, 0.90f),
                    Vector2.one,
                    new Vector2(24f, 8f),
                    new Vector2(-24f, -8f));
                AddImage(statusRoot.gameObject, Panel, false);

                var statusTexts = new UILocalizedText[7];
                var placeholders = new[]
                {
                    "Stage", "Ink", "Pending", "Generation", "GenerationBar", "Health", "Shield"
                };
                for (var index = 0; index < statusTexts.Length; index++)
                {
                    if (index == 4)
                    {
                        continue;
                    }

                    var column = index < 4 ? index : index - 1;
                    var minX = column / 6f;
                    var maxX = (column + 1f) / 6f;
                    statusTexts[index] = CreateLocalizedText(
                        placeholders[index],
                        statusRoot,
                        new Vector2(minX, 0.55f),
                        new Vector2(maxX, 1f),
                        25f,
                        TextAlignmentOptions.Center,
                        Ink);
                }

                var generationProgress = CreateProgressBar(
                    "GenerationProgress",
                    statusRoot,
                    new Vector2(0.50f, 0.08f),
                    new Vector2(0.66f, 0.45f),
                    new Color(0.24f, 0.18f, 0.14f, 1f),
                    new Color(0.28f, 0.62f, 0.35f, 1f));

                var battlefield = CreateBattlefield(
                    CreateRect(
                        "Battlefield",
                        root,
                        new Vector2(0.02f, 0.50f),
                        new Vector2(0.98f, 0.89f)));
                var equipment = CreateEquipmentBar(
                    CreateRect(
                        "Equipment",
                        root,
                        new Vector2(0.02f, 0.42f),
                        new Vector2(0.98f, 0.49f)));
                var board = CreateSpellBoard(
                    CreateRect(
                        "SpellBoard",
                        root,
                        new Vector2(0.02f, 0.02f),
                        new Vector2(0.98f, 0.41f)));

                SetObject(window, "_stageText", statusTexts[0]);
                SetObject(window, "_magicInkText", statusTexts[1]);
                SetObject(window, "_pendingText", statusTexts[2]);
                SetObject(window, "_generationText", statusTexts[3]);
                SetObject(window, "_generationProgress", generationProgress);
                SetObject(window, "_bookHealthText", statusTexts[5]);
                SetObject(window, "_bookShieldText", statusTexts[6]);
                SetObject(window, "_spellBoard", board);
                SetObject(window, "_equipmentBar", equipment);
                SetObject(window, "_battlefield", battlefield);

                SavePrefab(root.gameObject, MainPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        private static UIBattlefieldGameView CreateBattlefield(RectTransform root)
        {
            AddImage(root.gameObject, new Color(0.73f, 0.69f, 0.61f, 1f), false);
            var view = root.gameObject.AddComponent<UIBattlefieldGameView>();

            var health = CreateProgressBar(
                "BookHealth",
                root,
                new Vector2(0.02f, 0.86f),
                new Vector2(0.34f, 0.96f),
                new Color(0.20f, 0.12f, 0.10f, 1f),
                new Color(0.78f, 0.18f, 0.14f, 1f));
            var healthText = CreateLocalizedText(
                "BookHealthText",
                root,
                new Vector2(0.02f, 0.76f),
                new Vector2(0.34f, 0.86f),
                22f,
                TextAlignmentOptions.Center,
                Ink);
            var shield = CreateProgressBar(
                "BookShield",
                root,
                new Vector2(0.36f, 0.86f),
                new Vector2(0.68f, 0.96f),
                new Color(0.20f, 0.12f, 0.10f, 1f),
                new Color(0.78f, 0.62f, 0.18f, 1f));
            var shieldText = CreateLocalizedText(
                "BookShieldText",
                root,
                new Vector2(0.36f, 0.76f),
                new Vector2(0.68f, 0.86f),
                22f,
                TextAlignmentOptions.Center,
                Ink);
            var bookHit = CreateIndicator("BookHitFeedback", root, Accent);
            var shieldFeedback = CreateIndicator("ShieldFeedback", root, new Color(0.9f, 0.72f, 0.18f, 0.8f));

            var enemyPath = CreateRect(
                "EnemyPath",
                root,
                new Vector2(0.08f, 0.24f),
                new Vector2(0.92f, 0.72f));
            AddImage(enemyPath.gameObject, new Color(0.36f, 0.30f, 0.25f, 0.30f), false);
            var enemyViews = new UIEnemyBattleGameView[8];
            for (var index = 0; index < enemyViews.Length; index++)
            {
                enemyViews[index] = CreateEnemyView(enemyPath, index);
            }

            var cooldownProgresses = new UIProgressBar[4];
            var cooldownTexts = new Text[4];
            for (var index = 0; index < 4; index++)
            {
                var minX = 0.08f + index * 0.22f;
                cooldownProgresses[index] = CreateProgressBar(
                    $"Cooldown{index}",
                    root,
                    new Vector2(minX, 0.04f),
                    new Vector2(minX + 0.18f, 0.18f),
                    new Color(0.20f, 0.16f, 0.14f, 1f),
                    new Color(0.35f, 0.55f, 0.85f, 1f));
                cooldownTexts[index] = CreateLegacyText(
                    $"CooldownText{index}",
                    cooldownProgresses[index].transform,
                    Vector2.zero,
                    Vector2.one,
                    18,
                    Color.white);
            }

            var fireball = CreateEffect("FireballFeedback", enemyPath, new Color(1f, 0.26f, 0.08f, 0.9f));
            var chain = CreateEffect("ChainLightningFeedback", enemyPath, new Color(0.35f, 0.35f, 1f, 0.9f));
            var frost = CreateEffect("FrostRingFeedback", enemyPath, new Color(0.22f, 0.78f, 0.9f, 0.9f));
            var spellShield = CreateEffect("SpellShieldFeedback", enemyPath, new Color(0.9f, 0.68f, 0.18f, 0.9f));

            var result = CreateLocalizedText(
                "ResultFeedback",
                root,
                new Vector2(0.30f, 0.40f),
                new Vector2(0.70f, 0.62f),
                48f,
                TextAlignmentOptions.Center,
                Accent);
            var retry = CreateLocalizedText(
                "RetryFeedback",
                root,
                new Vector2(0.30f, 0.28f),
                new Vector2(0.70f, 0.42f),
                34f,
                TextAlignmentOptions.Center,
                Ink);

            SetObject(view, "_bookHealthProgress", health);
            SetObject(view, "_bookHealthText", healthText);
            SetObject(view, "_bookShieldProgress", shield);
            SetObject(view, "_bookShieldText", shieldText);
            SetObject(view, "_bookHitFeedback", bookHit);
            SetObject(view, "_shieldFeedback", shieldFeedback);
            SetObject(view, "_enemyPathRoot", enemyPath);
            SetObjects(view, "_enemyViews", enemyViews);
            SetObjects(view, "_cooldownProgresses", cooldownProgresses);
            SetObjects(view, "_cooldownTexts", cooldownTexts);
            SetObject(view, "_fireballFeedback", fireball);
            SetObject(view, "_chainLightningFeedback", chain);
            SetObject(view, "_frostRingFeedback", frost);
            SetObject(view, "_spellShieldFeedback", spellShield);
            SetObject(view, "_resultFeedback", result);
            SetObject(view, "_retryFeedback", retry);
            return view;
        }

        private static UIEnemyBattleGameView CreateEnemyView(RectTransform parent, int index)
        {
            var root = CreateRect($"Enemy{index}", parent, new Vector2(0f, 0.2f), new Vector2(0f, 0.8f));
            root.sizeDelta = new Vector2(84f, 110f);
            AddImage(root.gameObject, new Color(0.18f, 0.12f, 0.16f, 1f), true);
            var view = root.gameObject.AddComponent<UIEnemyBattleGameView>();
            var progress = CreateProgressBar(
                "Health",
                root,
                new Vector2(0f, 0.76f),
                new Vector2(1f, 1f),
                Color.black,
                new Color(0.72f, 0.12f, 0.16f, 1f));
            var healthText = CreateLegacyText(
                "HealthText",
                root,
                new Vector2(0f, 0.48f),
                new Vector2(1f, 0.76f),
                13,
                Color.white);
            var slow = CreateIndicator("Slow", root, new Color(0.18f, 0.72f, 0.92f, 0.8f));
            var hit = CreateIndicator("Hit", root, new Color(1f, 0.38f, 0.18f, 0.8f));
            var death = CreateIndicator("Death", root, new Color(0.06f, 0.05f, 0.05f, 0.9f));

            SetObject(view, "_rectTransform", root);
            SetObject(view, "_healthProgress", progress);
            SetObject(view, "_healthText", healthText);
            SetObject(view, "_slowIndicator", slow);
            SetObject(view, "_hitFeedback", hit);
            SetObject(view, "_deathFeedback", death);
            root.gameObject.SetActive(false);
            return view;
        }

        private static UISpellBoardGameView CreateSpellBoard(RectTransform root)
        {
            AddImage(root.gameObject, Panel, false);
            var view = root.gameObject.AddComponent<UISpellBoardGameView>();
            var grid = CreateRect("Grid", root, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(150f, 150f);
            layout.spacing = new Vector2(12f, 12f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var slots = new UISpellCardGameView[24];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = CreateSpellCard(grid, $"SpellSlot{index}");
            }

            var dragShadow = CreateSpellCard(root, "DragShadow");
            var dragRect = (RectTransform)dragShadow.transform;
            dragRect.anchorMin = dragRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragRect.sizeDelta = new Vector2(150f, 150f);
            var canvasGroup = dragShadow.gameObject.AddComponent<CanvasGroup>();
            dragShadow.gameObject.SetActive(false);

            SetObjects(view, "_slots", slots);
            SetObject(view, "_dragCoordinateRoot", root);
            SetObject(view, "_dragShadowRoot", dragRect);
            SetObject(view, "_dragShadowCard", dragShadow);
            SetObject(view, "_dragShadowCanvasGroup", canvasGroup);
            return view;
        }

        private static UIEquipmentBarGameView CreateEquipmentBar(RectTransform root)
        {
            AddImage(root.gameObject, Panel, false);
            var view = root.gameObject.AddComponent<UIEquipmentBarGameView>();
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(20, 20, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var slots = new UISpellCardGameView[4];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = CreateSpellCard(root, $"EquipmentSlot{index}");
            }

            SetObjects(view, "_slots", slots);
            return view;
        }

        private static UISpellCardGameView CreateSpellCard(Transform parent, string name)
        {
            var root = CreateRect(name, parent, Vector2.zero, Vector2.one);
            AddImage(root.gameObject, Slot, true);
            var itemSlot = root.gameObject.AddComponent<UIItemSlot>();
            var card = root.gameObject.AddComponent<UISpellCardGameView>();

            var emptyRoot = CreateRect("Empty", root, Vector2.zero, Vector2.one).gameObject;
            AddImage(emptyRoot, new Color(1f, 1f, 1f, 0.08f), false);
            var filledRoot = CreateRect("Filled", root, Vector2.zero, Vector2.one).gameObject;
            var icon = CreateRect("Icon", filledRoot.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
            var iconImage = AddImage(icon.gameObject, Color.white, false);
            var quantity = CreateTMP(
                "Quantity",
                filledRoot.transform,
                new Vector2(0.62f, 0f),
                new Vector2(1f, 0.30f),
                20f,
                TextAlignmentOptions.BottomRight,
                Color.white);
            var selected = CreateIndicator("Selected", root, new Color(1f, 0.72f, 0.12f, 0.35f));
            var tier = CreateLocalizedText(
                "Tier",
                root,
                new Vector2(0f, 0.72f),
                new Vector2(1f, 1f),
                20f,
                TextAlignmentOptions.Center,
                Color.white);
            var level = CreateLegacyText(
                "Level",
                root,
                new Vector2(0f, 0f),
                new Vector2(0.38f, 0.28f),
                18,
                Color.white);
            var locked = CreateIndicator("Locked", root, new Color(0.04f, 0.04f, 0.04f, 0.75f));

            SetObject(itemSlot, "_icon", iconImage);
            SetObject(itemSlot, "_quantityText", quantity);
            SetObject(itemSlot, "_selectedFrame", selected);
            SetObject(itemSlot, "_emptyRoot", emptyRoot);
            SetObject(itemSlot, "_filledRoot", filledRoot);
            SetObject(card, "_itemSlot", itemSlot);
            SetObject(card, "_tierText", tier);
            SetObject(card, "_levelText", level);
            SetObject(card, "_lockedIndicator", locked);
            return card;
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
            text.font = TMP_Settings.defaultFontAsset;
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateEffect(string name, Transform parent, Color color)
        {
            var root = CreateRect(name, parent, new Vector2(0f, 0.35f), new Vector2(0f, 0.65f));
            root.sizeDelta = new Vector2(64f, 64f);
            AddImage(root.gameObject, color, false);
            root.gameObject.SetActive(false);
            return root;
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
