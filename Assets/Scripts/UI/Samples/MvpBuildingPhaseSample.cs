using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>UI 독립 테스트용 견적/응답. 골드 차감이나 실제 건물 생성은 하지 않는다.</summary>
    public sealed class MvpBuildingPhaseSample : MonoBehaviour
    {
        [SerializeField] private BuildingActionPanel _panel;
        [SerializeField] private Button _slotButton;
        [SerializeField] private Button _buildingButton;
        [SerializeField] private Button _unavailableButton;
        [SerializeField] private TMP_Text _status;

        private void OnEnable()
        {
            _slotButton.onClick.AddListener(HandleSlot);
            _buildingButton.onClick.AddListener(HandleBuilding);
            _unavailableButton.onClick.AddListener(HandleUnavailable);
            _panel.ActionRequested += HandleRequest;
        }

        private void OnDisable()
        {
            _slotButton.onClick.RemoveListener(HandleSlot);
            _buildingButton.onClick.RemoveListener(HandleBuilding);
            _unavailableButton.onClick.RemoveListener(HandleUnavailable);
            _panel.ActionRequested -= HandleRequest;
        }

        public static BuildingActionViewData CreateSlot() => new BuildingActionViewData("sample-slot", "모의 건설 공간",
            new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, true, optionId: "warrior"));

        public static BuildingActionViewData CreateBuilding() => new BuildingActionViewData("sample-building", "모의 생산 건물",
            upgrade: new BuildingActionOffer(BuildingUiAction.Upgrade, "생산 건물 강화", 50, true),
            dismantle: new BuildingActionOffer(BuildingUiAction.Dismantle, "선택한 건물", 21, true));

        private void HandleSlot() => _panel.ShowActions(CreateSlot());
        private void HandleBuilding() => _panel.ShowActions(CreateBuilding());
        private void HandleUnavailable() => _panel.ShowActions(new BuildingActionViewData("sample-locked", "모의 미개방 공간",
            new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, false, "아직 개방되지 않은 공간입니다.", "warrior")));

        private void HandleRequest(BuildingActionRequest request)
        {
            _status.text = $"{request.Action} 요청 확인 · 모의 응답이며 실제 건설/차감 없음";
            _panel.TryResolveRequest(request.RequestId, false, "모의 요청 확인 완료. 실제 건물 시스템은 연결 대기입니다.");
        }
    }
}
