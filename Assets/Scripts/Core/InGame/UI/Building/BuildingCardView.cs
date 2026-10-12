using System;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>본문과 정보 버튼의 동작을 분리한다. 선택 가능 여부는 사용하는 화면에서 정한다.</summary>
    public sealed class BuildingCardView : MonoBehaviour
    {
        [SerializeField] private UIItemSlot _slot;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private UnityEngine.UI.Button _infoButton;
        [SerializeField] private TMP_Text _summary;

        private string _itemId;
        private Action<string> _onSelected;
        private Action<string> _onInfoRequested;

        private void Awake()
        {
            _infoButton.onClick.AddListener(() => _onInfoRequested?.Invoke(_itemId));
        }

        public void Bind(BuildingCatalogItem item, bool selected, bool interactable,
            Action<string> onSelected, Action<string> onInfo)
        {
            _itemId = item.Id;
            _onSelected = onSelected;
            _onInfoRequested = onInfo;
            _icon.enabled = item.Icon != null;

            string price = item.GoldCost.HasValue
                ? BuildingCurrencyText.Format(item.GoldCost.Value, item.GemCost.Value)
                : "가격 미정";
            _summary.text = item.Offer != null && !item.Offer.CanExecute
                ? item.Offer.DisabledReason : item.Summary;
            _slot.Bind(item.Icon, item.Name, price, selected, interactable,
                () => _onSelected?.Invoke(_itemId));
        }

        public void Unbind()
        {
            _itemId = null;
            _onSelected = null;
            _onInfoRequested = null;
            _slot.Unbind();
        }
    }
}
