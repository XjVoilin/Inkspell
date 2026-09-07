using System;
using System.IO;
using System.Linq;
using cfg;
using July.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class MainWindowPrefabTests
    {
        private const string PrefabPath = "Assets/Game/Res/Prefabs/UI/MainWindow/MainWindow.prefab";
        [Serializable] private class WindowRows { public WindowRow[] rows; }
        [Serializable] private class WindowRow { public int id; public string windowName; }

        [Test]
        public void LaunchWindowAddress_ResolvesToBoundArtPrefab()
        {
            var rows = JsonUtility.FromJson<WindowRows>("{\"rows\":" +
                File.ReadAllText("Assets/Game/Res/Configs/tbuiwindow.json") + "}").rows;
            var address = rows.Single(row => row.id == UIWindowID.UIInkspellMainWindow).windowName;
            var paths = AssetDatabase.FindAssets($"{address} t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => Path.GetFileNameWithoutExtension(path) == address).ToArray();
            Assert.That(paths, Is.EqualTo(new[] { PrefabPath }));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths.Single());
            Assert.That(prefab.GetComponent<UIView>(), Is.TypeOf<UIInkspellMainWindow>());
            Assert.That(prefab.transform.Find("Content/PaperWarmBg"), Is.Not.Null);
        }

        [Test]
        public void AuthoredSlots_AreVisibleUniqueAndUseCompleteFrames()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AssertSlots(prefab.GetComponentInChildren<UIEquipmentBarGameView>(true), 4, 192);
            AssertSlots(prefab.GetComponentInChildren<UISpellBoardGameView>(true), 24, 144);
        }

        [TestCase(EnemyType.NormalInkling, "img_enemyInkling")]
        [TestCase(EnemyType.SwiftInkling, "img_enemySwift")]
        [TestCase(EnemyType.ThickInkElite, "img_enemyElite")]
        [TestCase(EnemyType.ChapterBoss, "img_enemyBoss")]
        public void EnemyRender_UsesArtForItsBusinessType(EnemyType type, string spriteName)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                .GetComponentInChildren<UIEnemyBattleGameView>(true);
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                instance.Render(new EnemyBattleViewData { RuntimeId = 1, Type = type, Health = 50, MaxHealth = 100 });
                var art = (Image)new SerializedObject(instance).FindProperty("_art").objectReferenceValue;
                Assert.That(instance.gameObject.activeSelf, Is.True);
                Assert.That(art.sprite.name, Is.EqualTo(spriteName));
                Assert.That(instance.GetComponentInChildren<UIProgressBar>().NormalizedValue, Is.EqualTo(.5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void StatusFont_ContainsChineseInterfaceGlyphs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
                Assert.That(text.font.HasCharacters("第关魔法墨水待领取生成秒生命护盾一阶"), Is.True, text.name);
        }

        private static void AssertSlots(Component view, int count, float width)
        {
            var slots = new SerializedObject(view).FindProperty("_slots");
            Assert.That(slots.arraySize, Is.EqualTo(count));
            var positions = new System.Collections.Generic.HashSet<Vector2>();
            for (var i = 0; i < count; i++)
            {
                var slot = (UISpellCardGameView)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                var rect = (RectTransform)slot.transform;
                Assert.That(slot.gameObject.activeSelf, Is.True, slot.name);
                Assert.That(rect.sizeDelta.x, Is.EqualTo(width), slot.name);
                Assert.That(positions.Add(rect.anchoredPosition), Is.True, slot.name);
                Assert.That(slot.GetComponent<Image>().type, Is.EqualTo(Image.Type.Simple), slot.name);
                Assert.That(slot.GetComponent<Image>().raycastTarget, Is.True, slot.name);
            }
        }
    }
}
