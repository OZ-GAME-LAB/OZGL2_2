using System;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>상품 한 칸의 표시와 구매 버튼 입력만 담당한다.</summary>
    public sealed class ShopItemCardView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private UIIcon _emptyIcon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private UnityEngine.UI.Button _buyButton;
        [SerializeField] private TMP_Text _buyButtonText;

        //ShopView의 Refresh()에서 이벤트 연결됨
        private Action _purchase;

        private void OnEnable()
        {
            if (_buyButton != null) _buyButton.onClick.AddListener(HandlePurchase);
        }
        private void OnDisable()
        {
            if (_buyButton != null) _buyButton.onClick.RemoveListener(HandlePurchase);
            _purchase = null;
        }

        public void ShowItem(Sprite icon, string name, string description, string price,
            bool purchased, Action purchase)
        {
            _purchase = purchase;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            if (_emptyIcon != null) _emptyIcon.gameObject.SetActive(icon == null);
            _nameText.text = name;
            _descriptionText.text = (description ?? string.Empty).Replace("\\n", "\n");
            _priceText.text = price;
            _buyButton.gameObject.SetActive(true);
            _buyButton.interactable = !purchased;
            _buyButtonText.text = purchased ? "구매 완료" : "구매";
        }

        public void ShowEmpty()
        {
            _purchase = null;
            _icon.enabled = false;
            if (_emptyIcon != null) _emptyIcon.gameObject.SetActive(false);
            _nameText.text = "빈 슬롯";
            _descriptionText.text = string.Empty;
            _priceText.text = string.Empty;
            _buyButton.gameObject.SetActive(false);
        }

        public void Unbind()
        {
            _purchase = null;
            _buyButton.interactable = false;
        }

        private void HandlePurchase()
        {
            if (_buyButton.IsInteractable()) _purchase?.Invoke();
        }
    }
}
