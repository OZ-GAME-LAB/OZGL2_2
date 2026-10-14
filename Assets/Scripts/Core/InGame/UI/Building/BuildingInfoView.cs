using System;
using TMPro;
using Units.UnitDatas;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>현재 건물, 업그레이드 비교, 상세 팝업에서 공통으로 사용하는 정보 표시.</summary>
    public sealed class BuildingInfoView : MonoBehaviour
    {
        public event Action<UnitData> UnitSelected;
        public UIScreen Popup => _playerPopup;
        public string SelectionId { get; private set; }
        public bool HasSelection => SelectionId != null;

        [Header("Selection")]
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _contentPanel;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _categoryText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private GameObject _iconPlaceholder;

        [Header("Scrollable Information")]
        [SerializeField] private UnityEngine.UI.ScrollRect _detailsScroll;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _productionText;
        [SerializeField] private TMP_Text _effectText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private UnityEngine.UI.Button _unitButton;
        [SerializeField] private TMP_Text _unitText;
        [SerializeField] private bool _resetScrollOnSelection = true;

        [SerializeField] private string _missingDescription = "설명이 아직 없습니다.";
        [SerializeField] private UIScreen _playerPopup;
        private UnitData _unit;
        private string _buildingId;

        private void Awake()
        {
            _unitButton.onClick.AddListener(() => UnitSelected?.Invoke(_unit));
        }

        public void ShowBuildingInfo(BuildingInfoData data)
        {
            bool selectionChanged = SelectionId != data.SelectionId || _buildingId != data.BuildingId;
            SelectionId = data.SelectionId;
            _buildingId = data.BuildingId;
            _nameText.text = data.DisplayName;
            _categoryText.text = data.CategoryLabel;
            // 일반 건물 데이터에는 레벨이 없으므로 임의의 레벨을 표시하지 않는다.
            _levelText.gameObject.SetActive(false);
            _descriptionText.text = string.IsNullOrWhiteSpace(data.Description)
                ? _missingDescription : data.Description;
            _productionText.text = data.ProductionSummary;
            _productionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.ProductionSummary));
            _effectText.text = data.EffectSummary;
            _effectText.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.EffectSummary));
            _costText.text = data.CostSummary;
            _costText.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.CostSummary));
            _unit = data.Unit;
            _unitButton.gameObject.SetActive(_unit != null);
            _unitText.text = _unit != null ? _unit.UnitName + "  정보 보기" : string.Empty;
            _icon.sprite = data.Icon;
            _icon.gameObject.SetActive(data.Icon != null);
            _iconPlaceholder.SetActive(data.Icon == null);
            _emptyState.SetActive(false);
            _contentPanel.SetActive(true);

            if (selectionChanged && _resetScrollOnSelection)
            {
                _detailsScroll.StopMovement();
                _detailsScroll.verticalNormalizedPosition = 1f;
            }
        }

        public void HideBuildingInfo()
        {
            SelectionId = null;
            _buildingId = null;
            _unit = null;
            _detailsScroll.StopMovement();
            _detailsScroll.verticalNormalizedPosition = 1f;

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_contentPanel.transform))
                EventSystem.current.SetSelectedGameObject(null);

            _contentPanel.SetActive(false);
            _emptyState.SetActive(true);
        }
    }
}
