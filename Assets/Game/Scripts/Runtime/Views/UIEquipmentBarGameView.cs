using System;
using July.Arch;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 配置数量装备槽的显示与投放命中。
    /// </summary>
    public sealed class UIEquipmentBarGameView : GameView
    {
        [SerializeField] private UISpellCardGameView[] _slots;

        public event Action<long, int> EquipRequested;

        public void Render(EquipmentBarViewData data)
        {
            for (var index = 0; index < data.Slots.Length; index++)
            {
                _slots[index].Render(data.Slots[index]);
                _slots[index].SetSelected(false);
            }
        }

        protected override void OnViewAwake()
        {
            foreach (var slot in _slots)
            {
                slot.CardDropped += OnCardDropped;
            }
        }

        protected override void OnViewDestroy()
        {
            foreach (var slot in _slots)
            {
                slot.CardDropped -= OnCardDropped;
            }
        }

        private void OnCardDropped(
            UISpellCardGameView source,
            UISpellCardGameView target)
        {
            EquipRequested?.Invoke(source.Data.InstanceId, Array.IndexOf(_slots, target));
        }
    }
}
