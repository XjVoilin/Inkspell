using System;
using July.Arch;
using July.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 单张法术卡的纯显示与指针交互。
    /// </summary>
    public sealed class UISpellCardGameView : GameView,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerDownHandler,
        IDropHandler
    {
        [SerializeField] private UIItemSlot _itemSlot;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private GameObject _lockedIndicator;

        [Serializable]
        private struct IconBinding
        {
            public string ResourceKey;
            public Sprite Sprite;
        }
        [SerializeField] private IconBinding[] _iconBindings;
        [SerializeField] private SpellTierVisualSet[] _tierVisuals;
        private Sprite _displayedIcon;
        [SerializeField] private CanvasGroup _presentationGroup;
        private bool _dragged;
        [SerializeField] private Transform _iconTransform;

        internal Sprite DisplayedIcon => _displayedIcon;
        internal void SetDragDimmed(bool dimmed)
        {
            _presentationGroup.alpha = dimmed ? .35f : 1f;
        }

        internal void RenderDragCopy(UISpellCardGameView source)
        {
            ApplyLabels(source.Data);
            _displayedIcon = source.DisplayedIcon;
            _itemSlot.SetItem(_displayedIcon, 1);
        }

        internal SpellCardViewData Data { get; private set; }

        internal event Action<UISpellCardGameView> Clicked;
        internal event Action<UISpellCardGameView, PointerEventData> DragStarted;
        internal event Action<UISpellCardGameView, PointerEventData> DragMoved;
        internal event Action<UISpellCardGameView, PointerEventData> DragEnded;
        internal event Action<UISpellCardGameView, UISpellCardGameView> CardDropped;

        public void Render(SpellCardViewData data)
        {
            ApplyLabels(data);
            if (data == null) return;
            _displayedIcon = null;
            foreach (var binding in _iconBindings)
                if (binding.ResourceKey == data.IconResourceKey) { _displayedIcon = binding.Sprite; break; }
            if (_tierVisuals != null)
                foreach (var visual in _tierVisuals)
                    if (visual.ResourceKey == data.IconResourceKey)
                    {
                        _displayedIcon = visual.Get(data.Tier);
                        break;
                    }
            if (_displayedIcon == null)
            {
                _itemSlot.SetEmpty();
                Debug.LogError($"Missing authored spell icon: {data.IconResourceKey}", this);
                return;
            }
            _itemSlot.SetItem(_displayedIcon, 1);
        }

        private void ApplyLabels(SpellCardViewData data)
        {
            Data = data;
            if (_iconTransform != null) _iconTransform.localScale = Vector3.one * (data == null ? 1 : .80f + Mathf.Clamp(data.Tier, 1, 3) * .07f);
            _levelText.gameObject.SetActive(data != null);
            _lockedIndicator.SetActive(data != null && data.IsLocked);
            if (data == null)
            {
                _itemSlot.SetEmpty();
                _displayedIcon = null;
                return;
            }
            _levelText.text = data.Level.ToString();
        }

        internal void SetSelected(bool selected)
        {
            _itemSlot.SetSelected(selected);
        }

        public void OnPointerDown(PointerEventData eventData) => _dragged = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Data == null || !Data.CanDrag)
            {
                return;
            }

            _dragged = true;
            DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Data == null || !Data.CanDrag)
            {
                return;
            }

            DragMoved?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // A successful drop can consume this card before EndDrag is delivered.
            // Always release the shadow even when Render has already replaced its data.
            DragEnded?.Invoke(this, eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponentInParent<UISpellCardGameView>() : null;
            if (source == null || source.Data == null || !source.Data.CanDrag)
            {
                return;
            }

            CardDropped?.Invoke(source, this);
        }

        protected override void OnViewAwake()
        {
            _itemSlot.OnClicked += OnItemSlotClicked;
        }

        protected override void OnViewDestroy()
        {
            _itemSlot.OnClicked -= OnItemSlotClicked;
        }

        private void OnItemSlotClicked(UIItemSlot itemSlot)
        {
            if (_dragged) { _dragged = false; return; }
            if (Data != null)
            {
                Clicked?.Invoke(this);
            }
        }

    }
}
