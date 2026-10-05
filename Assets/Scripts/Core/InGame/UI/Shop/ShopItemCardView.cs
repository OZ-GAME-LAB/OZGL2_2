using System;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>상품 한 칸의 표시와 구매 버튼 입력만 담당한다.</summary>
    public sealed class ShopItemCardView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private UnityEngine.UI.Button _iconButton;
        [SerializeField] private UIIcon _emptyIcon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private UnityEngine.UI.Button _buyButton;
        [SerializeField] private TMP_Text _buyButtonText;

        public ArtifactData Artifact { get; private set; }

        // Refresh에서는 콜백만 교체하고 버튼 리스너는 다시 등록하지 않는다.
        private Action _purchase;
        private Action<ArtifactData, Vector2> _detail;

        private void OnEnable()
        {
            if (_buyButton != null) _buyButton.onClick.AddListener(HandlePurchase);
            if (_iconButton != null) _iconButton.onClick.AddListener(HandleDetail);
        }
        private void OnDisable()
        {
            if (_buyButton != null) _buyButton.onClick.RemoveListener(HandlePurchase);
            if (_iconButton != null) _iconButton.onClick.RemoveListener(HandleDetail);
            Artifact = null;
            _purchase = null;
            _detail = null;
        }

        public void ShowItem(Sprite icon, string name, string description, string price,
            bool purchased, Action purchase)
        {
            Artifact = null;
            _purchase = purchase;
            _detail = null;
            if (_iconButton != null) _iconButton.interactable = false;
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
            Artifact = null;
            _purchase = null;
            _detail = null;
            if (_iconButton != null) _iconButton.interactable = false;
            _icon.enabled = false;
            if (_emptyIcon != null) _emptyIcon.gameObject.SetActive(false);
            _nameText.text = "빈 슬롯";
            _descriptionText.text = string.Empty;
            _priceText.text = string.Empty;
            _buyButton.gameObject.SetActive(false);
        }

        public void Bind(ArtifactData artifact, string price, bool purchased, Action purchase,
            Action<ArtifactData, Vector2> detail = null)
        {
            if (artifact == null)
            {
                ShowEmpty();
                return;
            }

            ShowItem(artifact.Icon, artifact.DisplayName, artifact.Description, price, purchased, purchase);
            Artifact = artifact;
            _detail = detail;
            if (_iconButton != null) _iconButton.interactable = detail != null;
        }

        public void Unbind()
        {
            Artifact = null;
            _purchase = null;
            _detail = null;
            if (_iconButton != null) _iconButton.interactable = false;
            _buyButton.interactable = false;
        }

        private void HandlePurchase()
        {
            if (_buyButton.IsInteractable()) _purchase?.Invoke();
        }

        /// <summary>아이템의 이미지를 클릭했을 시 아이템 상세보기창을 여는 메서드</summary>
        private void HandleDetail()
        {
            if (Artifact == null || _detail == null || _iconButton == null || !_iconButton.IsInteractable()) return;
            Canvas canvas = _iconButton.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            // Button 입력은 키보드로도 실행될 수 있으므로 아이콘 중심을 상세 창 기준점으로 사용한다.
            var iconRect = (RectTransform)_iconButton.transform;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(camera,
                iconRect.TransformPoint(iconRect.rect.center));
            _detail(Artifact, screenPosition);
        }
    }
}
