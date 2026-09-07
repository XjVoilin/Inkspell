using System;
using System.Linq;
using System.IO;
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
        private const string MainArtDirectory = "Assets/Game/Arts/Textures/MainWindow/";
        [Serializable] private sealed class ArtLayout { public ArtLayer[] layers; }
        [Serializable] private sealed class ArtLayer
        {
            public string name;
            public float x, y, width, height;
        }

        [MenuItem("July/Inkspell/Bind MainWindow Art")]
        public static void GenerateMainWindow()
        {
            var prefab = PrefabUtility.LoadPrefabContents(MainPrefabPath);
            try
            {
                // A bound prefab is artist-editable. Re-running must preserve those edits.
                if (prefab.GetComponent<UIInkspellMainWindow>() != null)
                {
                    SpellPresentationAuthoring.Configure(prefab);
                    ValidateMainWindow(prefab);
                    SavePrefab(prefab, MainPrefabPath);
                    return;
                }

                var art = prefab.GetComponentsInChildren<Image>(true)
                    .ToDictionary(image => image.name);
                var window = prefab.AddComponent<UIInkspellMainWindow>();
                var content = CreateRect("Content", prefab.transform,
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                content.sizeDelta = new Vector2(1080, 1920);
                foreach (var image in art.Values)
                    image.transform.SetParent(content, false);

                var stage = Label("Stage", content, 100, 64, 410, 42, 30);
                var ink = Label("MagicInk", content, 570, 64, 410, 42, 30);
                var pending = Label("Pending", content, 780, 216, 260, 38, 24);
                var generation = Label("Generation", content, 100, 170, 650, 38, 24);
                var fill = art["ProgressFillImg"].rectTransform;
                // The exported example shows a partial fill; the mask needs the full track width.
                fill.sizeDelta = new Vector2(488, fill.sizeDelta.y);
                fill.anchoredPosition = new Vector2(106 + 244 - 540, fill.anchoredPosition.y);
                var maskRect = PixelRect("GenerationProgress", content, 106, 124, 488, 35);
                var mask = maskRect.gameObject.AddComponent<RectMask2D>();
                fill.SetParent(maskRect, false);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                var progress = maskRect.gameObject.AddComponent<UIProgressBar>();
                SetObject(progress, "_mask", mask);

                var equipmentRoot = CreateRect("Equipment", content, Vector2.zero, Vector2.one);
                var equipment = equipmentRoot.gameObject.AddComponent<UIEquipmentBarGameView>();
                var equipmentSlots = new UISpellCardGameView[4];
                var boardRoot = CreateRect("SpellBoard", content, Vector2.zero, Vector2.one);
                var board = boardRoot.gameObject.AddComponent<UISpellBoardGameView>();
                var boardSlots = new UISpellCardGameView[24];
                // Repeated slot images in the export are inactive placeholders. Restore their
                // individual authored rectangles from the source layout before binding them.
                var slotLayouts = JsonUtility.FromJson<ArtLayout>(File.ReadAllText(
                    MainArtDirectory + "MainWindow_ui_data.json")).layers
                    .Where(layer => layer.name == "MainWindow/tile_spellSlotBase").ToArray();
                if (slotLayouts.Length != 28)
                    throw new InvalidOperationException("MainWindow art must define 4 equipment and 24 board slots.");
                for (var i = 0; i < 28; i++)
                {
                    var slot = art[i == 0 ? "SpellSlotBaseTile" : $"SpellSlotBaseTile{i}"];
                    slot.transform.SetParent(i < 4 ? equipmentRoot : boardRoot, false);
                    var layout = slotLayouts[i];
                    slot.rectTransform.anchorMin = slot.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    slot.rectTransform.sizeDelta = new Vector2(layout.width, layout.height);
                    slot.rectTransform.anchoredPosition = new Vector2(
                        layout.x + layout.width / 2 - 540, 960 - layout.y - layout.height / 2);
                    slot.sprite = ArtSprite("tile_spellSlotBase");
                    slot.type = Image.Type.Simple;
                    slot.preserveAspect = true;
                    slot.gameObject.SetActive(true);
                    var card = BindSpellCard(slot.rectTransform);
                    if (i < 4) equipmentSlots[i] = card;
                    else boardSlots[i - 4] = card;
                }
                SetObjects(equipment, "_slots", equipmentSlots);
                SetObjects(board, "_slots", boardSlots);
                var shadowRect = PixelRect("DragShadow", boardRoot, 0, 0, 144, 144);
                AddImage(shadowRect.gameObject, Color.white, false).sprite = ArtSprite("tile_spellSlotBase");
                var shadow = BindSpellCard(shadowRect);
                var shadowGroup = shadowRect.gameObject.AddComponent<CanvasGroup>();
                shadowGroup.blocksRaycasts = false;
                shadowGroup.alpha = .8f;
                shadowRect.gameObject.SetActive(false);
                SetObject(board, "_dragCoordinateRoot", boardRoot);
                SetObject(board, "_dragShadowRoot", shadowRect);
                SetObject(board, "_dragShadowCard", shadow);
                SetObject(board, "_dragShadowCanvasGroup", shadowGroup);

                var battlefield = BindBattlefield(content, equipmentSlots, out var health, out var shield);
                foreach (var image in art.Values)
                {
                    if (image.name.StartsWith("SpellSlotEmptyMarkImg") ||
                        image.name.StartsWith("SpellFireballIcon") ||
                        image.name.StartsWith("SpellChainLightningIcon") ||
                        image.name.StartsWith("SpellIceRingIcon") ||
                        image.name.StartsWith("SpellRuneShieldIcon") ||
                        image.name == "SpellSelectedOverlayImg" || image.name == "SpellLockedIcon" ||
                        image.name.StartsWith("Enemy"))
                        UnityEngine.Object.DestroyImmediate(image.gameObject);
                }

                SetObject(window, "_stageText", stage);
                SetObject(window, "_magicInkText", ink);
                SetObject(window, "_pendingText", pending);
                SetObject(window, "_generationText", generation);
                SetObject(window, "_generationProgress", progress);
                SetObject(window, "_bookHealthText", health);
                SetObject(window, "_bookShieldText", shield);
                SetObject(window, "_spellBoard", board);
                SetObject(window, "_equipmentBar", equipment);
                SetObject(window, "_battlefield", battlefield);
                // Dragged cards must draw above all other interface regions.
                boardRoot.SetAsLastSibling();
                SpellPresentationAuthoring.Configure(prefab);
                ApplyUIFont(prefab);
                ValidateMainWindow(prefab);
                SavePrefab(prefab, MainPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static UISpellCardGameView BindSpellCard(RectTransform root)
        {
            root.GetComponent<Image>().raycastTarget = true;
            var item = root.gameObject.AddComponent<UIItemSlot>();
            var card = root.gameObject.AddComponent<UISpellCardGameView>();
            var empty = CreateRect("Empty", root, new Vector2(.15f, .15f), new Vector2(.85f, .85f));
            AddImage(empty.gameObject, new Color(1, 1, 1, .5f), false).sprite = ArtSprite("img_spellSlotEmptyMark");
            var filled = CreateRect("Filled", root, Vector2.zero, Vector2.one);
            var iconRect = CreateRect("Icon", filled, new Vector2(.14f, .14f), new Vector2(.86f, .86f));
            var icon = AddImage(iconRect.gameObject, Color.white, false);
            icon.preserveAspect = true;
            var quantity = CreateTMP("Quantity", filled, new Vector2(.6f, 0), new Vector2(1, .25f),
                20, TextAlignmentOptions.BottomRight, Ink);
            var selected = CreateIndicator("Selected", root, Color.white);
            selected.GetComponent<Image>().sprite = ArtSprite("img_spellSelectedOverlay");
            var tier = CreateLocalizedText("Tier", root, new Vector2(.06f, .78f), new Vector2(.94f, .98f),
                18, TextAlignmentOptions.Center, Ink);
            var level = CreateText("Level", root, new Vector2(.05f, .02f), new Vector2(.28f, .23f), 18, Ink);
            var locked = CreateRect("Locked", root, new Vector2(.73f, .02f), new Vector2(.98f, .35f));
            AddImage(locked.gameObject, Color.white, false).sprite = ArtSprite("icon_spellLocked");
            locked.gameObject.SetActive(false);
            filled.gameObject.SetActive(false);
            tier.gameObject.SetActive(false);
            level.gameObject.SetActive(false);
            SetObject(item, "_icon", icon);
            SetObject(item, "_quantityText", quantity);
            SetObject(item, "_selectedFrame", selected);
            SetObject(item, "_emptyRoot", empty.gameObject);
            SetObject(item, "_filledRoot", filled.gameObject);
            SetObject(card, "_itemSlot", item);
            SetObject(card, "_tierText", tier);
            SetObject(card, "_levelText", level);
            SetObject(card, "_lockedIndicator", locked.gameObject);
            return card;
        }

        private static UIBattlefieldGameView BindBattlefield(RectTransform content,
            UISpellCardGameView[] equipment, out UILocalizedText healthText, out UILocalizedText shieldText)
        {
            var root = CreateRect("Battlefield", content, Vector2.zero, Vector2.one);
            var view = root.gameObject.AddComponent<UIBattlefieldGameView>();
            var health = ThinProgress("BookHealth", root, 100, 270, 240, 12, new Color(.7f, .15f, .12f));
            healthText = Label("BookHealthText", root, 80, 288, 300, 32, 22);
            var shield = ThinProgress("BookShield", root, 100, 326, 240, 8, new Color(.65f, .49f, .12f));
            shieldText = Label("BookShieldText", root, 80, 620, 300, 32, 22);
            var book = content.Find("LivingSpellbookImg");
            var hit = CreateIndicator("BookHitFeedback", book, new Color(1, .2f, .1f, .35f));
            hit.GetComponent<Image>().sprite = ArtSprite("img_livingSpellbook");
            var bookShield = CreateIndicator("ShieldFeedback", book, new Color(1, .8f, .2f, .45f));
            bookShield.GetComponent<Image>().sprite = ArtSprite("icon_spellRuneShield");
            var path = PixelRect("EnemyPath", root, 330, 380, 630, 260);
            var enemies = new UIEnemyBattleGameView[8];
            for (var i = 0; i < enemies.Length; i++)
                enemies[i] = BindEnemy(path, i);
            var cooldownTexts = new TMP_Text[4];
            for (var i = 0; i < 4; i++)
            {
                cooldownTexts[i] = CreateText($"CooldownText{i}", equipment[i].transform,
                    new Vector2(.7f, .08f), new Vector2(.94f, .3f), 18, Ink);
            }
            var fireball = ArtEffect("FireballFeedback", path, "icon_spellFireball");
            var chain = ArtEffect("ChainLightningFeedback", path, "icon_spellChainLightning");
            var frost = ArtEffect("FrostRingFeedback", path, "icon_spellIceRing");
            var spellShield = ArtEffect("SpellShieldFeedback", path, "icon_spellRuneShield");
            var result = Label("ResultFeedback", root, 370, 290, 550, 70, 44);
            var retry = Label("RetryFeedback", root, 370, 360, 550, 48, 28);
            result.gameObject.SetActive(false);
            retry.gameObject.SetActive(false);
            SetObject(view, "_bookHealthProgress", health);
            SetObject(view, "_bookHealthText", healthText);
            SetObject(view, "_bookShieldProgress", shield);
            SetObject(view, "_bookShieldText", shieldText);
            SetObject(view, "_bookHitFeedback", hit);
            SetObject(view, "_shieldFeedback", bookShield);
            SetObject(view, "_enemyPathRoot", path);
            SetObjects(view, "_enemyViews", enemies);
            SetObjects(view, "_cooldownTexts", cooldownTexts);
            SetObject(view, "_fireballFeedback", fireball);
            SetObject(view, "_chainLightningFeedback", chain);
            SetObject(view, "_frostRingFeedback", frost);
            SetObject(view, "_spellShieldFeedback", spellShield);
            SetObject(view, "_resultFeedback", result);
            SetObject(view, "_retryFeedback", retry);
            return view;
        }

        private static UIEnemyBattleGameView BindEnemy(RectTransform path, int index)
        {
            var root = CreateRect($"Enemy{index}", path, new Vector2(.5f, 0), new Vector2(.5f, 0));
            root.pivot = new Vector2(.5f, 0);
            root.sizeDelta = new Vector2(160, 180);
            var artRoot = CreateRect("Art", root, Vector2.zero, Vector2.one);
            var art = AddImage(artRoot.gameObject, Color.white, false);
            art.preserveAspect = true;
            var view = root.gameObject.AddComponent<UIEnemyBattleGameView>();
            var health = CreateProgressBar("Health", root, new Vector2(.1f, 1.02f), new Vector2(.9f, 1.07f),
                Color.clear, new Color(.7f, .15f, .12f));
            var text = CreateText("HealthText", root, new Vector2(0, 1.07f), new Vector2(1, 1.22f), 18, Ink);
            var slow = CreateIndicator("Slow", root, new Color(.2f, .7f, 1, .4f));
            slow.GetComponent<Image>().sprite = ArtSprite("icon_spellIceRing");
            var hit = CreateIndicator("Hit", root, new Color(1, .3f, .15f, .4f));
            hit.GetComponent<Image>().sprite = ArtSprite("icon_spellFireball");
            var death = CreateIndicator("Death", root, new Color(.1f, .08f, .08f, .4f));
            death.GetComponent<Image>().sprite = ArtSprite("img_enemyInkling");
            SetObject(view, "_rectTransform", root);
            SetObject(view, "_art", art);
            SetObject(view, "_normalSprite", ArtSprite("img_enemyInkling"));
            SetObject(view, "_swiftSprite", ArtSprite("img_enemySwift"));
            SetObject(view, "_eliteSprite", ArtSprite("img_enemyElite"));
            SetObject(view, "_bossSprite", ArtSprite("img_enemyBoss"));
            SetObject(view, "_healthProgress", health);
            SetObject(view, "_healthText", text);
            SetObject(view, "_slowIndicator", slow);
            SetObject(view, "_hitFeedback", hit);
            SetObject(view, "_deathFeedback", death);
            root.gameObject.SetActive(false);
            return view;
        }

        private static RectTransform ArtEffect(string name, RectTransform parent, string sprite)
        {
            var rect = CreateRect(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            rect.sizeDelta = new Vector2(100, 100);
            AddImage(rect.gameObject, new Color(1, 1, 1, .8f), false).sprite = ArtSprite(sprite);
            rect.gameObject.SetActive(false);
            return rect;
        }

        private static RectTransform PixelRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = CreateRect(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x + width / 2 - 540, 960 - y - height / 2);
            return rect;
        }

        private static UILocalizedText Label(string name, Transform parent, float x, float y, float width, float height, float size)
        {
            var rect = PixelRect(name, parent, x, y, width, height);
            return CreateLocalizedText("Text", rect, Vector2.zero, Vector2.one, size, TextAlignmentOptions.Center, Ink);
        }

        private static UIProgressBar ThinProgress(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = PixelRect(name, parent, x, y, width, height);
            return CreateProgressBar("Progress", rect, Vector2.zero, Vector2.one, Color.clear, color);
        }

        private static Sprite ArtSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(MainArtDirectory + name + ".png") ??
                throw new InvalidOperationException($"MainWindow art sprite is missing: {name}");
        }

        private static void ValidateMainWindow(GameObject main)
        {
            var window = main.GetComponent<UIInkspellMainWindow>() ??
                throw new InvalidOperationException("MainWindow is missing its window logic.");
            ValidateReferences(window);
            foreach (var component in main.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is UISpellCardGameView || component is UIEquipmentBarGameView ||
                    component is UISpellBoardGameView || component is UIBattlefieldGameView ||
                    component is UIEnemyBattleGameView || component is UIItemSlot || component is UIProgressBar)
                    ValidateReferences(component);
        }
    }
}
