using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>표시용 선택/체력 알림만 재현한다. 실제 유닛 생성, 공격, 회복, 사망 처리는 하지 않는다.</summary>
    public sealed class MvpUnitInfoSample : MonoBehaviour
    {
        [SerializeField] private UnitInfoPanel _panel;
        [SerializeField] private GameUIController _hud;
        [SerializeField] private Button _warriorButton;
        [SerializeField] private Button _archerButton;
        [SerializeField] private Button _enemyButton;
        [SerializeField] private Button _longTextButton;
        [SerializeField] private Button _damageButton;
        [SerializeField] private Button _healButton;
        [SerializeField] private Button _removeButton;
        [SerializeField] private Button _staleButton;
        [SerializeField] private TMP_Text _status;

        private string _selectedId;
        private float _health;
        private float _maximum;

        private void OnEnable()
        {
            _panel.InfoPanelClosed += HandlePanelClosed;
            _warriorButton.onClick.AddListener(HandleWarriorClicked);
            _archerButton.onClick.AddListener(HandleArcherClicked);
            _enemyButton.onClick.AddListener(HandleEnemyClicked);
            _longTextButton.onClick.AddListener(HandleLongTextClicked);
            _damageButton.onClick.AddListener(HandleDamageClicked);
            _healButton.onClick.AddListener(HandleHealClicked);
            _removeButton.onClick.AddListener(HandleRemoveClicked);
            _staleButton.onClick.AddListener(HandleStaleClicked);
        }

        private void OnDisable()
        {
            _panel.InfoPanelClosed -= HandlePanelClosed;
            _warriorButton.onClick.RemoveListener(HandleWarriorClicked);
            _archerButton.onClick.RemoveListener(HandleArcherClicked);
            _enemyButton.onClick.RemoveListener(HandleEnemyClicked);
            _longTextButton.onClick.RemoveListener(HandleLongTextClicked);
            _damageButton.onClick.RemoveListener(HandleDamageClicked);
            _healButton.onClick.RemoveListener(HandleHealClicked);
            _removeButton.onClick.RemoveListener(HandleRemoveClicked);
            _staleButton.onClick.RemoveListener(HandleStaleClicked);
        }

        private void Start()
        {
            _hud.Initialize();
            _hud.SetGold(100);
            _hud.SetWaveProgress(1, 3);
            _hud.SetPhaseLabel("유닛 UI 테스트");
            _panel.HideUnitInfo();
            _status.text = "선택 대기 · 체력/능력치는 UI 검사용 임시 수치입니다.";
        }

        private void HandleWarriorClicked() => Select(new UnitInfoData("sample-warrior-001", "전사", "아군 · 마계", "전열 · 균형형 근거리", 100, 100,
            "공격과 방어가 균형 잡힌 전열 병종입니다.", "공격력 12 · 방어력 5\n사거리 1 · 공격 간격 1초 (임시)", "건물 업그레이드에 따라 전직한 유닛이 생산됩니다."));
        private void HandleArcherClicked() => Select(new UnitInfoData("sample-archer-001", "궁사", "아군 · 마계", "후열 · 원거리 공격", 60, 60,
            "후열에서 공격합니다. 적이 접근하면 취약하므로 전열 병종과 조합하세요.", "공격력 15 · 방어력 2\n사거리 5 · 공격 간격 1.2초 (임시)"));
        private void HandleEnemyClicked() => Select(new UnitInfoData("sample-enemy-001", "경비병", "적군 · 비정규군", "탱커 · 전열 유지", 150, 150,
            "높은 체력과 방어력으로 전선을 유지하는 적 병종입니다.", "공격력 8 · 방어력 8\n사거리 1 (임시)"));
        private void HandleLongTextClicked() => Select(new UnitInfoData("sample-long-001", "긴 설명 확인용 유닛", "아군 · 마계", "지원 · 스크롤 테스트", 80, 100,
            string.Concat(Enumerable.Repeat("긴 설명을 스크롤해서 확인합니다. 체력만 갱신할 때 읽던 위치를 유지해야 합니다.\n", 20))));

        private void HandleDamageClicked() => UpdateHealth(-25);
        private void HandleHealClicked() => UpdateHealth(25);
        private void HandleRemoveClicked()
        {
            if (!_panel.TryHideUnitInfo(_selectedId)) return;
            _selectedId = null;
            _status.text = "제거 알림 반영 · 정보창 초기화 (실제 유닛 삭제 없음)";
        }
        private void HandleStaleClicked()
        {
            var updated = _panel.TryUpdateHealth("previous-spawn-instance", 0, 100);
            var hidden = _panel.TryHideUnitInfo("previous-spawn-instance");
            _status.text = !updated && !hidden ? "이전 유닛 알림 무시 · 현재 정보 유지" : "예상하지 않은 이전 알림 적용";
        }
        private void HandlePanelClosed(string id)
        {
            _selectedId = null;
            _status.text = "닫기 알림 수신 · 외부 선택 연결 지점";
        }

        private void Select(UnitInfoData data)
        {
            _selectedId = data.SelectionId;
            _health = data.CurrentHealth;
            _maximum = data.MaxHealth;
            _panel.ShowUnitInfo(data);
            _status.text = data.DisplayName + " 선택 · 임시 표시 데이터";
        }

        private void UpdateHealth(float delta)
        {
            if (_selectedId == null) return;
            var next = Mathf.Clamp(_health + delta, 0, _maximum);
            if (!_panel.TryUpdateHealth(_selectedId, next, _maximum)) return;
            _health = next;
            _status.text = "체력 표시 갱신 · 실제 전투/회복/사망 판정 없음";
        }
    }
}
