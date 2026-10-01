using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>선택된 건물의 표시만 담당한다. 선택 판정/재화/건설 규칙은 외부 소유다.</summary>
    public sealed class BuildingInfoView : MonoBehaviour
    {
        public event Action<string> InfoPanelClosed;

        public string SelectionId { get; private set; }
        public bool HasSelection => SelectionId != null;

        [Header("Selection")]
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _contentPanel;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _categoryText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _iconPlaceholder;

        [Header("Scrollable Information")]
        [SerializeField] private ScrollRect _detailsScroll;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _productionText;
        [SerializeField] private TMP_Text _effectText;

        [Header("Display Copy")]
        [SerializeField] private string _levelFormat = "레벨 {0}";
        [SerializeField] private string _missingDescription = "설명이 아직 없습니다.";
        [SerializeField] private string _productionFormat = "생산 유닛\n{0}";
        [SerializeField] private string _effectFormat = "건물 효과\n{0}";

        [SerializeField] private UIScreen _playerPopup;

        private void OnEnable()
        {
            if (_playerPopup != null) _playerPopup.Closed += HandlePopupClosed;
        }

        private void OnDisable()
        {
            if (_playerPopup != null) _playerPopup.Closed -= HandlePopupClosed;
        }

        public void ShowBuildingInfo(BuildingInfoData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // 표시 문자열을 먼저 준비해 형식 오류가 나도 선택이 일부만 바뀌지 않게 한다.
            var level = string.Format(_levelFormat, data.Level);
            var production = string.IsNullOrWhiteSpace(data.ProductionSummary)
                ? string.Empty : string.Format(_productionFormat, data.ProductionSummary);
            var effect = string.IsNullOrWhiteSpace(data.EffectSummary)
                ? string.Empty : string.Format(_effectFormat, data.EffectSummary);
            var selectionChanged = SelectionId != data.SelectionId;

            SelectionId = data.SelectionId;
            _nameText.text = data.DisplayName;
            _categoryText.text = data.CategoryLabel;
            _levelText.text = level;
            _descriptionText.text = string.IsNullOrWhiteSpace(data.Description)
                ? _missingDescription : data.Description;
            _productionText.text = production;
            _productionText.gameObject.SetActive(production.Length > 0);
            _effectText.text = effect;
            _effectText.gameObject.SetActive(effect.Length > 0);
            _icon.sprite = data.Icon;
            _icon.gameObject.SetActive(data.Icon != null);
            _iconPlaceholder.SetActive(data.Icon == null);
            _emptyState.SetActive(false);
            _contentPanel.SetActive(true);
            if (_playerPopup != null) _playerPopup.Show();

            if (selectionChanged)
            {
                _detailsScroll.StopMovement();
                _detailsScroll.verticalNormalizedPosition = 1f;
            }
        }

        public void HideBuildingInfo()
        {
            ClearDisplay();
            if (_playerPopup != null) _playerPopup.Hide();
        }

        private void ClearDisplay()
        {
            SelectionId = null;
            _detailsScroll.StopMovement();
            _detailsScroll.verticalNormalizedPosition = 1f;
            _nameText.text = string.Empty;
            _categoryText.text = string.Empty;
            _levelText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _productionText.text = string.Empty;
            _effectText.text = string.Empty;
            _productionText.gameObject.SetActive(false);
            _effectText.gameObject.SetActive(false);
            _icon.sprite = null;
            _icon.gameObject.SetActive(false);
            _iconPlaceholder.SetActive(true);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_contentPanel.transform))
                EventSystem.current.SetSelectedGameObject(null);
            _contentPanel.SetActive(false);
            _emptyState.SetActive(_playerPopup == null);
        }

        private void HandlePopupClosed(UIScreen popup, UICloseReason reason)
        {
            if (!HasSelection) return;
            var id = SelectionId;
            ClearDisplay();
            // 상세 창을 떠나면 선택도 정리한다. 후보 목록에서 넘어오는 전환은 목록에서 처리한다.
            InfoPanelClosed?.Invoke(id);
        }
    }
}
