using System;
using July.Arch;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>
    /// 合成区固定格渲染、选中与拖拽命中。
    /// </summary>
    public sealed class UISpellBoardGameView : GameView
    {
        [SerializeField] private UISpellCardGameView[] _slots;
        [SerializeField] private RectTransform _dragCoordinateRoot;
        [SerializeField] private RectTransform _dragShadowRoot;
        [SerializeField] private UISpellCardGameView _dragShadowCard;
        [SerializeField] private CanvasGroup _dragShadowCanvasGroup;

        private long? _selectedSpellId;

        public event Action<long> SpellClicked;
        public event Action<long, long> SynthesisRequested;

        public void Render(SpellBoardViewData data)
        {
            var selectedFound = false;
            for (var index = 0; index < data.Slots.Length; index++)
            {
                var card = data.Slots[index];
                _slots[index].Render(card);

                var selected = card != null && card.InstanceId == _selectedSpellId;
                _slots[index].SetSelected(selected);
                selectedFound |= selected;
            }

            if (!selectedFound)
            {
                _selectedSpellId = null;
            }
        }

        protected override void OnViewAwake()
        {
            foreach (var slot in _slots)
            {
                slot.Clicked += OnCardClicked;
                slot.DragStarted += OnCardDragStarted;
                slot.DragMoved += OnCardDragMoved;
                slot.DragEnded += OnCardDragEnded;
                slot.CardDropped += OnCardDropped;
            }

            _dragShadowCanvasGroup.blocksRaycasts = false;
            _dragShadowRoot.gameObject.SetActive(false);
        }

        protected override void OnViewDestroy()
        {
            foreach (var slot in _slots)
            {
                slot.Clicked -= OnCardClicked;
                slot.DragStarted -= OnCardDragStarted;
                slot.DragMoved -= OnCardDragMoved;
                slot.DragEnded -= OnCardDragEnded;
                slot.CardDropped -= OnCardDropped;
            }
        }

        private void OnCardClicked(UISpellCardGameView card)
        {
            _selectedSpellId = card.Data.InstanceId;
            SetSelection();
            SpellClicked?.Invoke(card.Data.InstanceId);
        }

        private void OnCardDragStarted(
            UISpellCardGameView card,
            PointerEventData eventData)
        {
            _selectedSpellId = card.Data.InstanceId;
            SetSelection();
            _dragShadowCard.Render(card.Data);
            _dragShadowRoot.gameObject.SetActive(true);
            _dragShadowRoot.SetAsLastSibling();
            MoveDragShadow(eventData);
        }

        private void OnCardDragMoved(
            UISpellCardGameView card,
            PointerEventData eventData)
        {
            MoveDragShadow(eventData);
        }

        private void OnCardDragEnded(
            UISpellCardGameView card,
            PointerEventData eventData)
        {
            _dragShadowRoot.gameObject.SetActive(false);
            _dragShadowCard.Render(null);
        }

        private void OnCardDropped(
            UISpellCardGameView source,
            UISpellCardGameView target)
        {
            if (target.Data == null || source.Data.InstanceId == target.Data.InstanceId)
            {
                return;
            }

            SynthesisRequested?.Invoke(source.Data.InstanceId, target.Data.InstanceId);
        }

        private void SetSelection()
        {
            foreach (var slot in _slots)
            {
                slot.SetSelected(
                    slot.Data != null && slot.Data.InstanceId == _selectedSpellId);
            }
        }

        private void MoveDragShadow(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragCoordinateRoot,
                eventData.position,
                eventData.pressEventCamera,
                out var localPoint);
            _dragShadowRoot.anchoredPosition = localPoint;
        }
    }
}
