using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>UI 응답을 수동으로 재현하는 테스트 더블. 건물 생성/재화 변경/전투를 하지 않는다.</summary>
    public sealed class MvpBuildingActionSample : MonoBehaviour
    {
        [SerializeField] private BuildingActionPanel _panel;
        [SerializeField] private BuildingInfoPanel _info;
        [SerializeField] private GameUIController _hud;
        [SerializeField] private Button _slotAButton;
        [SerializeField] private Button _slotBButton;
        [SerializeField] private Button _blockedButton;
        [SerializeField] private Button _phaseButton;
        [SerializeField] private Button _successButton;
        [SerializeField] private Button _failureButton;
        [SerializeField] private TMP_Text _status;

        private BuildingActionRequest _request;
        private string _selected = "sample-slot-a";
        private int _slotALevel;
        private int _slotBLevel = 1;
        private bool _inBattle;

        private void OnEnable()
        {
            _panel.ActionRequested += HandleActionRequested;
            _info.InfoPanelClosed += HandleInfoClosed;
            _slotAButton.onClick.AddListener(HandleSlotAClicked);
            _slotBButton.onClick.AddListener(HandleSlotBClicked);
            _blockedButton.onClick.AddListener(HandleBlockedClicked);
            _phaseButton.onClick.AddListener(HandlePhaseClicked);
            _successButton.onClick.AddListener(HandleSuccessClicked);
            _failureButton.onClick.AddListener(HandleFailureClicked);
        }

        private void OnDisable()
        {
            _panel.ActionRequested -= HandleActionRequested;
            _info.InfoPanelClosed -= HandleInfoClosed;
            _slotAButton.onClick.RemoveListener(HandleSlotAClicked);
            _slotBButton.onClick.RemoveListener(HandleSlotBClicked);
            _blockedButton.onClick.RemoveListener(HandleBlockedClicked);
            _phaseButton.onClick.RemoveListener(HandlePhaseClicked);
            _successButton.onClick.RemoveListener(HandleSuccessClicked);
            _failureButton.onClick.RemoveListener(HandleFailureClicked);
        }

        private void Start()
        {
            _hud.Initialize();
            _hud.SetGold(100);
            _hud.SetWaveProgress(1, 3);
            _hud.SetPhaseLabel("UI 테스트 · 준비");
            _panel.SetActionsAllowed(true);
            ShowSelection();
            _status.text = "임시 견적: 30 / 50 / 21 골드 · 실제 건설과 차감 없음";
        }

        private void HandleSlotAClicked() { _selected = "sample-slot-a"; ShowSelection(); }
        private void HandleSlotBClicked() { _selected = "sample-slot-b"; ShowSelection(); }
        private void HandleBlockedClicked() { _selected = "sample-slot-c"; ShowSelection(); }

        private void HandlePhaseClicked()
        {
            _inBattle = !_inBattle;
            _panel.SetActionsAllowed(!_inBattle);
            _hud.SetPhaseLabel(_inBattle ? "UI 테스트 · 전투" : "UI 테스트 · 준비");
        }

        private void HandleInfoClosed(string targetId)
        {
            _selected = null;
            _panel.HideActions();
        }

        private void HandleActionRequested(BuildingActionRequest request)
        {
            _request = request;
            _status.text = $"{request.TargetId} / {request.Action} 요청 접수 · 아래 성공/실패 응답을 선택하세요.";
        }

        private void HandleSuccessClicked()
        {
            if (_request == null) return;
            var request = _request;
            _request = null;
            if (!_panel.TryResolveRequest(request.RequestId, true)) return;
            // 테스트 화면 상태만 갱신한다. 실제 게임 객체/골드/세이브에는 접근하지 않는다.
            if (request.TargetId == "sample-slot-a") _slotALevel = NextLevel(_slotALevel, request.Action);
            if (request.TargetId == "sample-slot-b") _slotBLevel = NextLevel(_slotBLevel, request.Action);
            if (_selected == request.TargetId) ShowSelection();
            _status.text = "성공 응답 + 최신 표시 정보 수신 · HUD 골드는 100 그대로 유지됩니다.";
        }

        private void HandleFailureClicked()
        {
            if (_request == null) return;
            var request = _request;
            _request = null;
            _panel.TryResolveRequest(request.RequestId, false, "조건이 변경되어 실행하지 못했습니다. 다시 시도해 주세요.");
            _status.text = "실패 응답 수신 · 선택/레벨/골드 변경 없음";
        }

        private void ShowSelection()
        {
            if (_selected == null) return;
            var level = _selected == "sample-slot-a" ? _slotALevel : _slotBLevel;
            if (_selected == "sample-slot-c")
            {
                _info.HideBuildingInfo();
                _panel.ShowActions(new BuildingActionViewData(_selected, "건설 불가 · 조건 표시 테스트",
                    new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, false,
                        "골드가 부족합니다. (담당 시스템이 전달한 테스트 사유)", "sample-warrior")));
            }
            else if (level == 0)
            {
                _info.HideBuildingInfo();
                _panel.ShowActions(new BuildingActionViewData(_selected, "빈 건설 공간 · " + _selected,
                    new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, true, optionId: "sample-warrior")));
            }
            else
            {
                _info.ShowBuildingInfo(new BuildingInfoData(_selected, "전사 훈련소", "유닛 생산 건물", level,
                    "UI 응답 테스트용 정보입니다. 실제 건물과 유닛은 생성하지 않습니다.",
                    level == 1 ? "전사" : "밸런스 전사(1차)"));
                _panel.ShowActions(new BuildingActionViewData(_selected, "전사 훈련소 · 레벨 " + level,
                    upgrade: new BuildingActionOffer(BuildingUiAction.Upgrade, "생산 건물 강화", 50,
                        level == 1, "최대 업그레이드 단계입니다."),
                    dismantle: new BuildingActionOffer(BuildingUiAction.Dismantle, "선택한 건물", 21, true)));
            }
        }

        private static int NextLevel(int level, BuildingUiAction action)
        {
            if (action == BuildingUiAction.Dismantle) return 0;
            return action == BuildingUiAction.Build ? 1 : level + 1;
        }
    }
}
