using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>UI 확인 전용 임시 데이터. 실제 게임 씬에 사용하지 않는다.</summary>
    public sealed class MvpBuildingInfoSample : MonoBehaviour
    {
        [SerializeField] private BuildingInfoPanel _panel;
        [SerializeField] private GameUIController _hud;
        [SerializeField] private Button _resourceButton;
        [SerializeField] private Button _warriorButton;
        [SerializeField] private Button _archerButton;
        [SerializeField] private Button _supportButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private TMP_Text _statusText;

        private bool _showUpgradedWarrior;

        private void OnEnable()
        {
            _resourceButton.onClick.AddListener(HandleResourceClicked);
            _warriorButton.onClick.AddListener(HandleWarriorClicked);
            _archerButton.onClick.AddListener(HandleArcherClicked);
            _supportButton.onClick.AddListener(HandleSupportClicked);
            _refreshButton.onClick.AddListener(HandleRefreshClicked);
            _clearButton.onClick.AddListener(HandleClearClicked);
            _panel.InfoPanelClosed += HandleInfoPanelClosed;
        }

        private void Start()
        {
            _hud.Initialize();
            _hud.SetGold(100);
            _hud.SetWaveProgress(1, 3);
            _hud.SetPhaseLabel("UI 테스트");
            _panel.HideBuildingInfo();
            _statusText.text = "선택 대기 · 실제 건물 선택과 건설 기능은 미연동";
        }

        private void OnDisable()
        {
            _resourceButton.onClick.RemoveListener(HandleResourceClicked);
            _warriorButton.onClick.RemoveListener(HandleWarriorClicked);
            _archerButton.onClick.RemoveListener(HandleArcherClicked);
            _supportButton.onClick.RemoveListener(HandleSupportClicked);
            _refreshButton.onClick.RemoveListener(HandleRefreshClicked);
            _clearButton.onClick.RemoveListener(HandleClearClicked);
            _panel.InfoPanelClosed -= HandleInfoPanelClosed;
        }

        private void HandleResourceClicked()
        {
            Show(new BuildingInfoData("sample-slot-01", "골드 생산소", "자원 건물", 1,
                "현재 플레이에서 사용할 골드를 생산하는 건물입니다.",
                effectSummary: "골드 생산 · 수치와 지급 시점은 담당 시스템에서 전달"));
        }

        private void HandleWarriorClicked()
        {
            _showUpgradedWarrior = false;
            ShowWarrior();
        }

        private void HandleArcherClicked()
        {
            Show(new BuildingInfoData("sample-slot-03", "궁사 훈련소", "유닛 생산 건물", 1,
                "후열에서 공격하는 궁사를 생산합니다. 전열 병종과 조합해 보호하세요.",
                "궁사 · 후열 원거리 병종"));
        }

        private void HandleSupportClicked()
        {
            Show(new BuildingInfoData("sample-slot-04", "지원 건물", "지원 건물", 1,
                "아군의 전투를 보조합니다. 실제 지원 효과는 건물 데이터 확정 후 연결합니다.",
                effectSummary: "강화 · 회복 · 방어 중 확정된 효과 표시"));
        }

        private void HandleRefreshClicked()
        {
            _showUpgradedWarrior = !_showUpgradedWarrior;
            ShowWarrior();
            _statusText.text = "동일 슬롯의 새 표시 데이터 수신 · 실제 업그레이드/차감 없음";
        }

        private void HandleClearClicked()
        {
            _panel.HideBuildingInfo();
            _statusText.text = "외부 선택 해제 알림 수신 · 이전 정보 초기화";
        }

        private void HandleInfoPanelClosed(string selectionId)
        {
            _statusText.text = "정보창 닫힘 · 실제 월드 선택 해제는 담당 시스템에서 처리";
        }

        private void ShowWarrior()
        {
            Show(new BuildingInfoData("sample-slot-02", "전사 훈련소", "유닛 생산 건물",
                _showUpgradedWarrior ? 2 : 1,
                "공격과 방어가 균형 잡힌 전열 병종을 생산합니다. 건물 업그레이드 후에는 해당 전직의 유닛 정보가 표시됩니다.",
                _showUpgradedWarrior ? "밸런스 전사(1차) · 전열 근거리 병종" : "전사 · 전열 근거리 병종"));
        }

        private void Show(BuildingInfoData data)
        {
            _panel.ShowBuildingInfo(data);
            _statusText.text = data.DisplayName + " 선택 · 테스트용 명칭/데이터";
        }
    }
}
