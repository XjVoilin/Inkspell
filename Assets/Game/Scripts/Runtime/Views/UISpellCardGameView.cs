using System;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Localization;
using July.Resource;
using July.UI;
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
        IDropHandler
    {
        [SerializeField] private UIItemSlot _itemSlot;
        [SerializeField] private UILocalizedText _tierText;
        [SerializeField] private Text _levelText;
        [SerializeField] private GameObject _lockedIndicator;

        private ResourceHandle<Sprite> _iconHandle;
        private int _renderVersion;

        internal SpellCardViewData Data { get; private set; }

        internal event Action<UISpellCardGameView> Clicked;
        internal event Action<UISpellCardGameView, PointerEventData> DragStarted;
        internal event Action<UISpellCardGameView, PointerEventData> DragMoved;
        internal event Action<UISpellCardGameView, PointerEventData> DragEnded;
        internal event Action<UISpellCardGameView, UISpellCardGameView> CardDropped;

        public void Render(SpellCardViewData data)
        {
            Data = data;
            _renderVersion++;
            ReleaseIcon();

            if (data == null)
            {
                _itemSlot.SetEmpty();
                _tierText.gameObject.SetActive(false);
                _levelText.gameObject.SetActive(false);
                _lockedIndicator.SetActive(false);
                return;
            }

            _itemSlot.SetEmpty();
            _tierText.gameObject.SetActive(true);
            _tierText.SetKey(data.TierDisplayKey);
            _levelText.gameObject.SetActive(true);
            _levelText.text = data.Level.ToString();
            _lockedIndicator.SetActive(data.IsLocked);
            LoadIconAsync(data.IconResourceKey, _renderVersion).Forget();
        }

        internal void SetSelected(bool selected)
        {
            _itemSlot.SetSelected(selected);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Data == null || !Data.CanDrag)
            {
                return;
            }

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
            if (Data == null || !Data.CanDrag)
            {
                return;
            }

            DragEnded?.Invoke(this, eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag.GetComponentInParent<UISpellCardGameView>();
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
            _renderVersion++;
            ReleaseIcon();
        }

        private void OnItemSlotClicked(UIItemSlot itemSlot)
        {
            if (Data != null)
            {
                Clicked?.Invoke(this);
            }
        }

        private async UniTask LoadIconAsync(string resourceKey, int renderVersion)
        {
            var handle = await GetSystem<IResourceSystem>().LoadAssetAsync<Sprite>(resourceKey);
            if (this == null || renderVersion != _renderVersion)
            {
                handle.Dispose();
                return;
            }

            _iconHandle = handle;
            _itemSlot.SetItem(handle.Asset, 1);
        }

        private void ReleaseIcon()
        {
            _iconHandle?.Dispose();
            _iconHandle = null;
        }
    }
}
