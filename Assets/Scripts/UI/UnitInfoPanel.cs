using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class UnitInfoPanel : MonoBehaviour
    {
        public event Action<string> InfoPanelClosed;

        public string SelectionId { get; private set; }
        public bool HasSelection => SelectionId != null;

        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _contentPanel;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _factionText;
        [SerializeField] private TMP_Text _roleText;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _healthStateText;
        [SerializeField] private RectTransform _healthFill;
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _iconPlaceholder;
        [SerializeField] private Button _closeButton;
        [SerializeField] private ScrollRect _detailsScroll;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _combatText;
        [SerializeField] private TMP_Text _traitText;

        [Header("Display Copy")]
        [SerializeField] private string _healthFormat = "체력 {0:0.##} / {1:0.##}";
        [SerializeField] private string _emptyHealthLabel = "체력 소진";
        [SerializeField] private string _descriptionFallback = "설명이 아직 없습니다.";
        [SerializeField] private string _combatFormat = "전투 정보\n{0}";
        [SerializeField] private string _traitFormat = "특성 · 전직\n{0}";

        private float _currentHealth;
        private float _maxHealth;

        private void OnEnable()
        {
            if (_closeButton == null) return;
            _closeButton.onClick.RemoveListener(HandleCloseClicked);
            _closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        public void ShowUnitInfo(UnitInfoData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // 모든 포맷을 먼저 검사해 잘못된 표시 설정이 선택을 부분 적용하지 않게 한다.
            var health = string.Format(_healthFormat, data.CurrentHealth, data.MaxHealth);
            var combat = string.IsNullOrWhiteSpace(data.CombatSummary) ? "" : string.Format(_combatFormat, data.CombatSummary);
            var traits = string.IsNullOrWhiteSpace(data.TraitSummary) ? "" : string.Format(_traitFormat, data.TraitSummary);
            var changed = SelectionId != data.SelectionId;
            SelectionId = data.SelectionId;
            _nameText.text = data.DisplayName;
            _factionText.text = data.FactionLabel;
            _roleText.text = data.RoleLabel;
            _descriptionText.text = string.IsNullOrWhiteSpace(data.Description) ? _descriptionFallback : data.Description;
            _combatText.text = combat;
            _combatText.gameObject.SetActive(combat.Length > 0);
            _traitText.text = traits;
            _traitText.gameObject.SetActive(traits.Length > 0);
            _icon.sprite = data.Icon;
            _icon.gameObject.SetActive(data.Icon != null);
            _iconPlaceholder.SetActive(data.Icon == null);
            ApplyHealth(data.CurrentHealth, data.MaxHealth, health);
            _emptyState.SetActive(false);
            _contentPanel.SetActive(true);
            if (changed)
            {
                _detailsScroll.StopMovement();
                _detailsScroll.verticalNormalizedPosition = 1;
            }
        }

        /// <summary>외부 체력 알림 반영. 이전 선택/유효하지 않은 값은 변경 없이 false.</summary>
        public bool TryUpdateHealth(string selectionId, float currentHealth, float maxHealth)
        {
            if (!HasSelection || selectionId != SelectionId || !UnitInfoData.IsValidHealth(currentHealth, maxHealth)) return false;
            if (_currentHealth == currentHealth && _maxHealth == maxHealth) return true;
            var health = string.Format(_healthFormat, currentHealth, maxHealth);
            ApplyHealth(currentHealth, maxHealth, health);
            return true;
        }

        /// <summary>제거/디스폰 알림. 이전 유닛의 지연 알림으로 현재 선택을 닫지 않는다.</summary>
        public bool TryHideUnitInfo(string selectionId)
        {
            if (!HasSelection || selectionId != SelectionId) return false;
            HideUnitInfo();
            return true;
        }

        public void HideUnitInfo()
        {
            SelectionId = null;
            _currentHealth = _maxHealth = 0;
            _nameText.text = _factionText.text = _roleText.text = "";
            _healthText.text = _healthStateText.text = "";
            _descriptionText.text = _combatText.text = _traitText.text = "";
            _combatText.gameObject.SetActive(false);
            _traitText.gameObject.SetActive(false);
            _healthFill.anchorMax = new Vector2(0, 1);
            _icon.sprite = null;
            _icon.gameObject.SetActive(false);
            _iconPlaceholder.SetActive(true);
            _detailsScroll.StopMovement();
            _detailsScroll.verticalNormalizedPosition = 1;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_contentPanel.transform))
                EventSystem.current.SetSelectedGameObject(null);
            _contentPanel.SetActive(false);
            _emptyState.SetActive(true);
        }

        private void HandleCloseClicked()
        {
            if (!isActiveAndEnabled || !HasSelection || !_contentPanel.activeInHierarchy || !_closeButton.IsInteractable()) return;
            var id = SelectionId;
            HideUnitInfo();
            InfoPanelClosed?.Invoke(id);
        }

        private void ApplyHealth(float current, float maximum, string text)
        {
            _currentHealth = current;
            _maxHealth = maximum;
            _healthText.text = text;
            _healthFill.anchorMax = new Vector2(Mathf.Clamp01(current / maximum), 1);
            _healthStateText.text = current == 0 ? _emptyHealthLabel : "";
        }
    }
}
