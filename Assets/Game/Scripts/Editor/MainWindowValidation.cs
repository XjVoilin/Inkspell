using System;
using System.IO;
using System.Linq;
using Game;
using July.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Checks the actual configured asset and its serialized runtime binding graph.</summary>
    public static class MainWindowValidation
    {
        [Serializable] private class Windows { public Window[] rows; }
        [Serializable] private class Window { public int id; public string windowName; }

        [MenuItem("July/Inkspell/Validate MainWindow")]
        public static void Validate()
        {
            var rows = JsonUtility.FromJson<Windows>("{\"rows\":" +
                File.ReadAllText("Assets/Game/Res/Configs/tbuiwindow.json") + "}").rows;
            var address = rows.Single(row => row.id == UIWindowID.UIInkspellMainWindow).windowName;
            Require(address == "MainWindow", $"Main window loads '{address}', expected 'MainWindow'.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InkspellUIPrefabGenerator.MainPrefabPath);
            Require(prefab != null && prefab.GetComponent<UIView>() is UIInkspellMainWindow,
                "Configured MainWindow needs UIInkspellMainWindow on its root.");
            Require(prefab.transform.Find("Content/PaperWarmBg") != null, "MainWindow art is missing.");
            Require(prefab.GetComponentsInChildren<UISpellBoardGameView>(true).Length == 1, "Expected one spell board.");
            Require(prefab.GetComponentsInChildren<UIEquipmentBarGameView>(true).Length == 1, "Expected one equipment bar.");
            CheckArray(prefab.GetComponentInChildren<UISpellBoardGameView>(true), "_slots", 24);
            CheckArray(prefab.GetComponentInChildren<UIEquipmentBarGameView>(true), "_slots", 4);
            CheckArray(prefab.GetComponentInChildren<UIBattlefieldGameView>(true), "_enemyViews", 8);
            CheckArray(prefab.GetComponentInChildren<UIBattlefieldGameView>(true), "_cooldownProgresses", 4);
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Require(component != null, "Missing script on MainWindow.");
                if (!(component is UIInkspellMainWindow || component is UISpellCardGameView ||
                    component is UISpellBoardGameView || component is UIEquipmentBarGameView ||
                    component is UIBattlefieldGameView || component is UIEnemyBattleGameView ||
                    component is UIItemSlot || component is UIProgressBar)) continue;
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Require(iterator.objectReferenceValue != null,
                            $"Unbound {component.name}/{component.GetType().Name}.{iterator.propertyPath}");
            }
            Require(prefab.GetComponentsInChildren<UISpellCardGameView>(true)
                .All(card => card.GetComponent<Image>().raycastTarget), "Card hit targets are disabled.");
            Debug.Log("[MainWindowValidation] PASS: MainWindow address, art, runtime components and all bindings.");
        }

        private static void CheckArray(Component component, string field, int count)
        {
            var array = new SerializedObject(component).FindProperty(field);
            Require(array.arraySize == count,
                $"Expected {count} entries in {component.GetType().Name}.{field}.");
            if (field == "_slots")
                for (var i = 0; i < count; i++)
                {
                    var slot = (UISpellCardGameView)array.GetArrayElementAtIndex(i).objectReferenceValue;
                    Require(slot.gameObject.activeSelf && ((RectTransform)slot.transform).sizeDelta.x > 0,
                        $"{slot.name} must be visible and retain its authored size.");
                    Require(slot.GetComponent<Image>().type == Image.Type.Simple,
                        $"{slot.name} must scale the full frame instead of tiling a cropped image.");
                }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
