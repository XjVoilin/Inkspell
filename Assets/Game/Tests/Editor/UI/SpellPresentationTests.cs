using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class SpellPresentationTests
    {
        private const string Prefab = "Assets/Game/Res/Prefabs/UI/MainWindow/MainWindow.prefab";
        private static UISpellCardGameView NewCard() => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponentInChildren<UISpellCardGameView>(true));
        [Test]
        public void DragDimming_ReusesAuthoredGroupWithoutAddingComponents()
        {
            var card = NewCard();
            try
            {
                var group = card.GetComponent<CanvasGroup>();
                var count = card.GetComponentsInChildren<Component>(true).Length;
                Assert.That(group, Is.Not.Null);
                for (var i = 0; i < 20; i++)
                {
                    card.SetDragDimmed(true);
                    Assert.That(group.alpha, Is.EqualTo(.35f));
                    card.SetDragDimmed(false);
                    Assert.That(group.alpha, Is.EqualTo(1f));
                }
                Assert.That(card.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(card.gameObject); }
        }

        [Test]
        public void SwitchingSpellTypesAndTiers_ImmediatelyShowsMatchingAuthoredIcon()
        {
            var card = NewCard(); var shadow = NewCard();
            var keys = new[]{"icon_spellFireball", "icon_spellChainLightning", "icon_spellIceRing", "icon_spellRuneShield"};
            try
            {
                var count = card.GetComponentsInChildren<Component>(true).Length;
                for (var i = 0; i < 100; i++)
                {
                    var key = keys[i % 4];
                    card.Render(new SpellCardViewData {InstanceId=i+1, Tier=1+i%3, Level=1, IconResourceKey=key});
                    Assert.That(card.DisplayedIcon.name, Is.EqualTo(key));
                    shadow.RenderDragCopy(card);
                    Assert.That(shadow.DisplayedIcon, Is.SameAs(card.DisplayedIcon));
                    shadow.Render(null);
                    card.Render(null);
                }
                Assert.That(card.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(shadow.gameObject); Object.DestroyImmediate(card.gameObject); }
        }

        [Test]
        public void SettlementRender_DoesNotWaitForMergeAnimation()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            try
            {
                var board = root.GetComponentInChildren<UISpellBoardGameView>(true);
                typeof(UISpellBoardGameView).GetField("_merging",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(board,true);
                var slots = new SpellCardViewData[24];
                slots[0] = new SpellCardViewData {InstanceId=99,Tier=3,Level=1,IconResourceKey="icon_spellIceRing"};
                board.Render(new SpellBoardViewData {Slots=slots});
                var view = (UISpellCardGameView)new SerializedObject(board).FindProperty("_slots").GetArrayElementAtIndex(0).objectReferenceValue;
                Assert.That(view.Data.InstanceId,Is.EqualTo(99));
                Assert.That(view.DisplayedIcon.name,Is.EqualTo("icon_spellIceRing"));
            }
            finally {Object.DestroyImmediate(root);}
        }

        [TestCase(1,1,false,1,true)]
        [TestCase(1,2,false,1,false)]
        [TestCase(3,3,false,1,false)]
        [TestCase(2,2,true,1,false)]
        [TestCase(2,2,false,2,false)]
        public void MergeHover_RejectsIneligibleInputs(int tierA,int tierB,bool locked,int level,bool expected)
        {
            var a=new SpellCardViewData {InstanceId=1,Tier=tierA,Level=level,CanDrag=true};
            var b=new SpellCardViewData {InstanceId=2,Tier=tierB,Level=1,CanDrag=true,IsLocked=locked};
            Assert.That(UISpellBoardGameView.CanPreviewMerge(a,b), Is.EqualTo(expected));
        }

        [Test]
        public void DropWithoutPointerSource_IsIgnored()
        {
            var card=NewCard();
            try { Assert.DoesNotThrow(()=>card.OnDrop(new PointerEventData(null))); }
            finally { Object.DestroyImmediate(card.gameObject); }
        }

        [Test]
        public void Prefab_ContainsBoundPresentationAndTileShapedCooldowns()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            foreach (var card in root.GetComponentsInChildren<UISpellCardGameView>(true))
            {
                var serialized = new SerializedObject(card);
                foreach (var field in new[]{"_tierGraphic", "_presentationGroup", "_iconTransform"})
                    Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, card.name + field);
            }
            foreach (var graphic in root.GetComponentsInChildren<SpellTierGraphic>(true))
                Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null);
            var board = new SerializedObject(root.GetComponentInChildren<UISpellBoardGameView>(true));
            Assert.That(board.FindProperty("_burst").objectReferenceValue, Is.Not.Null);
            var battle = new SerializedObject(root.GetComponentInChildren<UIBattlefieldGameView>(true));
            var covers = battle.FindProperty("_cooldownCovers");
            Assert.That(covers.arraySize, Is.EqualTo(4));
            for (var i = 0; i < covers.arraySize; i++)
            {
                var image = (Image)covers.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(image.sprite.name, Is.EqualTo("tile_spellSlotBase"));
                Assert.That(image.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(image.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
                Assert.That(image.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(image.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(image.raycastTarget, Is.False);
            }
        }
    }
}
