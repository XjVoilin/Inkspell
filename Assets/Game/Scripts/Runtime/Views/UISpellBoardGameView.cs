using System;
using System.Collections;
using July.Arch;
using July.Audio;
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
        private IAudioSystem _audio;
        private UISpellCardGameView _dragSource;
        private UISpellCardGameView _hover;
        private bool _merging;
        private bool _resolved;
        private bool _success;
        private Vector2 _dragMotion;
        private Coroutine _mergeRoutine;
        [SerializeField] private SpellMergeBurstGraphic _burst;
        private long _dragSourceId;
        private long _resultId;

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
            _audio = this.GetSystem<IAudioSystem>();
            foreach (var slot in _slots)
            {
                slot.Clicked += OnCardClicked;
                slot.DragStarted += OnCardDragStarted;
                slot.DragMoved += OnCardDragMoved;
                slot.DragEnded += OnCardDragEnded;
                slot.CardDropped += OnCardDropped;
            }

            _burst.gameObject.SetActive(false);
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
            if (_merging) return;
            Play("CommonBtnClick", 0.42f, 105);
            _selectedSpellId = card.Data.InstanceId;
            SetSelection();
            SpellClicked?.Invoke(card.Data.InstanceId);
        }

        private void OnCardDragStarted(
            UISpellCardGameView card,
            PointerEventData eventData)
        {
            if (_merging || _dragSource != null || card.DisplayedIcon == null) return;
            Play("SfxUiCardPickup", 0.44f, 105);
            _selectedSpellId = card.Data.InstanceId;
            SetSelection();
            _dragSource = card;
            _dragSourceId = card.Data.InstanceId;
            card.SetDragDimmed(true);
            _dragShadowCard.RenderDragCopy(card);
            _dragShadowRoot.localScale = new Vector3(1.10f, 1.10f, 1);
            _dragMotion = Vector2.zero;
            _dragShadowRoot.gameObject.SetActive(true);
            _dragShadowRoot.SetAsLastSibling();
            MoveDragShadow(eventData);
        }

        private void OnCardDragMoved(
            UISpellCardGameView card,
            PointerEventData eventData)
        {
            if (_merging || _dragSource != card) return;
            MoveDragShadow(eventData);
            _hover = null;
            foreach (var slot in _slots)
            {
                if (slot != card && slot.Data != null &&
                    RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, eventData.position, eventData.pressEventCamera))
                    _hover = slot;
            }
            foreach (var slot in _slots)
                slot.SetSelected(slot == _hover && CanPreviewMerge(card.Data, slot.Data));
        }

        private void OnCardDragEnded(
            UISpellCardGameView card,
            PointerEventData eventData)
        {
            Play("SfxUiCardDrop", 0.40f, 110);
            if (_merging) return;
            ClearDrag();
        }

        private void OnCardDropped(
            UISpellCardGameView source,
            UISpellCardGameView target)
        {
            if (_merging || source.Data == null || target.Data == null || source.Data.InstanceId == target.Data.InstanceId)
            {
                Play("SfxUiInvalid", 0.48f, 85);
                return;
            }

            if (_dragSource != source || source.Data.InstanceId != _dragSourceId) { ClearDrag(); return; }
            _merging = true;
            _resolved = false;
            var dropPosition = target.transform.position;
            var firstId = source.Data.InstanceId;
            var secondId = target.Data.InstanceId;
            Play("SfxSynthesisStart", 0.55f, 75, "Synthesis");
            try { SynthesisRequested?.Invoke(firstId, secondId); }
            finally { _merging = false; ClearDrag(); }
            if (!_resolved) return;
            if (_mergeRoutine != null) StopCoroutine(_mergeRoutine);
            _mergeRoutine = StartCoroutine(AnimateMerge(dropPosition));
        }

        internal void ResolveSynthesis(bool success, long resultInstanceId)
        {
            if (!_merging) return;
            _resolved = true;
            _success = success;
            _resultId = resultInstanceId;
        }

        internal static bool CanPreviewMerge(SpellCardViewData first, SpellCardViewData second) =>
            first != null && second != null && first.InstanceId != second.InstanceId &&
            first.CanDrag && second.CanDrag && !first.IsLocked && !second.IsLocked &&
            first.Tier == second.Tier && first.Tier < 3 && first.Level == 1 && second.Level == 1;

        private void Update()
        {
            if (_dragSource == null || _merging) return;
            var dt = Time.unscaledDeltaTime;
            _dragMotion = Vector2.Lerp(_dragMotion, Vector2.zero, 1 - Mathf.Exp(-12 * dt));
            var amount = _dragMotion.magnitude / 55f;
            var angle = Mathf.Clamp(-_dragMotion.x * .3f + _dragMotion.y * .12f, -14, 14);
            _dragShadowRoot.localRotation = Quaternion.Slerp(_dragShadowRoot.localRotation,
                Quaternion.Euler(0, 0, angle), 1 - Mathf.Exp(-20 * dt));
            _dragShadowRoot.localScale = Vector3.Lerp(_dragShadowRoot.localScale,
                new Vector3(1.10f + amount * .12f, 1.10f - amount * .07f, 1), 1 - Mathf.Exp(-20 * dt));
        }

        private IEnumerator AnimateMerge(Vector3 dropPosition)
        {
            var position = dropPosition;
            if (_success)
                foreach (var slot in _slots)
                    if (slot.Data != null && slot.Data.InstanceId == _resultId)
                    {
                        position = slot.transform.position;
                        _selectedSpellId = _resultId;
                        SetSelection();
                        break;
                    }
            _burst.transform.position = position;
            _burst.transform.SetAsLastSibling();
            _burst.gameObject.SetActive(true);
            // The sorted result is already visible. Feedback never locks the next drag.
            for (var elapsed = 0f; elapsed < .25f; elapsed += Time.unscaledDeltaTime)
            {
                _burst.Render(elapsed / .25f, _success);
                yield return null;
            }
            _burst.gameObject.SetActive(false);
            _mergeRoutine = null;
        }

        private void ClearDrag()
        {
            if (_dragSource != null) _dragSource.SetDragDimmed(false);
            _dragSource = null; _hover = null;
            _dragShadowRoot.gameObject.SetActive(false);
            _dragShadowRoot.localScale = Vector3.one;
            _dragShadowRoot.localRotation = Quaternion.identity;
            _dragShadowCard.Render(null);
            SetSelection();
        }

        protected override void OnViewDisable()
        {
            if (_mergeRoutine != null) StopCoroutine(_mergeRoutine);
            _mergeRoutine = null; _merging = false;
            if (_burst != null) _burst.gameObject.SetActive(false);
            foreach (var slot in _slots)
            {
                slot.transform.localScale = Vector3.one;
                slot.transform.localRotation = Quaternion.identity;
                slot.SetDragDimmed(false);
            }
            ClearDrag();
        }

        private void Play(
            string address,
            float volume,
            int priority,
            string group = "UI")
        {
            _audio.PlaySfx(address, new SfxPlayOptions
            {
                Group = group,
                Volume = volume,
                Priority = priority,
            });
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
            _dragMotion = Vector2.ClampMagnitude(localPoint - _dragShadowRoot.anchoredPosition, 55f);
            _dragShadowRoot.anchoredPosition = localPoint;
        }
    }
}
