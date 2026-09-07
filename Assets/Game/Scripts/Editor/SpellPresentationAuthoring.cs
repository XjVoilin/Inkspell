using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    // Editor-only migration shared by existing prefabs and the MainWindow generator.
    public static class SpellPresentationAuthoring
    {
        private const string PrefabPath = "Assets/Game/Res/Prefabs/UI/MainWindow/MainWindow.prefab";

        [MenuItem("July/Inkspell/Update Spell Presentation Prefab")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("[SpellPresentationAuthoring] Prefab presentation components saved.");
        }

        [Serializable] private class IconRow { public string iconResourceKey; }
        [Serializable] private class IconRows { public IconRow[] rows; }

        public static void Configure(GameObject root)
        {
            var definitions = JsonUtility.FromJson<IconRows>("{\"rows\":" + File.ReadAllText("Assets/Game/Res/Configs/tbspelldefinition.json") + "}").rows;
            foreach (var card in root.GetComponentsInChildren<UISpellCardGameView>(true))
            {
                var group = card.GetComponent<CanvasGroup>();
                if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
                var legacyEmblem = card.transform.Find("TierEmblem");
                if (legacyEmblem != null) legacyEmblem.name = "TierFrame";
                var emblem = Child(card.transform, "TierFrame");
                emblem.SetAsFirstSibling();
                var graphic = Component<SpellTierGraphic>(emblem);
                graphic.raycastTarget = false;
                Bind(card, "_tierGraphic", graphic);
                Bind(card, "_presentationGroup", group);
                Bind(card, "_iconTransform", card.transform.Find("Filled/Icon"));
                var cardObject = new SerializedObject(card);
                var bindings = cardObject.FindProperty("_iconBindings");
                bindings.arraySize = definitions.Length;
                for (var i = 0; i < definitions.Length; i++)
                {
                    var key = definitions[i].iconResourceKey;
                    var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Arts/Textures/MainWindow/" + key + ".png");
                    if (icon == null) throw new InvalidOperationException("Missing spell sprite: " + key);
                    var entry = bindings.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("ResourceKey").stringValue = key;
                    entry.FindPropertyRelative("Sprite").objectReferenceValue = icon;
                }
                cardObject.ApplyModifiedPropertiesWithoutUndo();
            }
            var board = root.GetComponentInChildren<UISpellBoardGameView>(true);
            var boardObject = new SerializedObject(board);
            var coordinate = (RectTransform)boardObject.FindProperty("_dragCoordinateRoot").objectReferenceValue;
            var burstRect = Child(coordinate, "MergeBurst");
            burstRect.anchorMin = burstRect.anchorMax = new Vector2(.5f, .5f);
            burstRect.sizeDelta = new Vector2(240, 240);
            var burst = Component<SpellMergeBurstGraphic>(burstRect);
            burst.raycastTarget = false;
            burst.gameObject.SetActive(false);
            Bind(board, "_burst", burst);

            var battle = root.GetComponentInChildren<UIBattlefieldGameView>(true);
            var serialized = new SerializedObject(battle);
            var texts = serialized.FindProperty("_cooldownTexts");
            var covers = serialized.FindProperty("_cooldownCovers");
            covers.arraySize = texts.arraySize;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Arts/Textures/MainWindow/tile_spellSlotBase.png");
            for (var i = 0; i < texts.arraySize; i++)
            {
                var text = (TMP_Text)texts.GetArrayElementAtIndex(i).objectReferenceValue;
                var slot = text.transform.parent;
                var legacy = slot.Find("Cooldown" + i);
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                var rect = Child(slot, "CooldownCover");
                rect.SetAsLastSibling();
                var cover = Component<Image>(rect);
                cover.sprite = sprite;
                cover.type = Image.Type.Filled;
                cover.fillMethod = Image.FillMethod.Radial360;
                cover.fillOrigin = (int)Image.Origin360.Top;
                cover.fillClockwise = true;
                cover.fillAmount = 0;
                cover.preserveAspect = true;
                cover.color = new Color(.06f, .07f, .10f, .72f);
                cover.raycastTarget = false;
                covers.GetArrayElementAtIndex(i).objectReferenceValue = cover;
                text.transform.SetAsLastSibling();
                text.rectTransform.anchorMin = new Vector2(.15f, .27f);
                text.rectTransform.anchorMax = new Vector2(.85f, .73f);
                text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                text.fontSize = 38; text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static T Component<T>(RectTransform rect) where T : Component
        {
            if (typeof(Graphic).IsAssignableFrom(typeof(T)) && rect.GetComponent<CanvasRenderer>() == null)
                rect.gameObject.AddComponent<CanvasRenderer>();
            var component = rect.GetComponent<T>();
            return component != null ? component : rect.gameObject.AddComponent<T>();
        }
        private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
