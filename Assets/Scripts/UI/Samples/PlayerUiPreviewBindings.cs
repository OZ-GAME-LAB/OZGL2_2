using Game.Core;
using TMPro;
using Units;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>플레이어 UI 프리뷰 전용 구성. 실제 건물 생성/소비를 가장하지 않는다.</summary>
    public sealed class PlayerUiPreviewBindings : MonoBehaviour
    {
        public bool IsReady { get; private set; }

        [SerializeField] private BuildingCatalogPanel _catalog;
        [SerializeField] private BuildingInfoPanel _buildingInfo;
        [SerializeField] private BuildingActionPanel _actions;
        [SerializeField] private UnitInfoPanel _unitInfo;
        [SerializeField] private RuntimeUnitInfoBinding _unitBinding;
        [SerializeField] private RuntimeUnitInfoSource _unitSource;
        [SerializeField] private Unit_Core _unit;
        [SerializeField] private RuntimeUnitCountHud _counts;
        [SerializeField] private Button _unitButton;
        [SerializeField] private Button _campButton;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private bool _useRuntimeBuildings;

        private void OnEnable()
        {
            if (!_useRuntimeBuildings) _catalog.ItemSelected += HandleCatalogSelected;
            _unitButton.onClick.AddListener(HandleUnit);
            _campButton.onClick.AddListener(HandleCamp);
            if (!_useRuntimeBuildings) _actions.ActionRequested += HandleUnavailableAction;
            _flow.PhaseChanged += HandlePhase;
        }

        private void Start()
        {
            if (!_useRuntimeBuildings) _catalog.SetItems(new[]
            {
                new BuildingCatalogItem("warrior", "전사 훈련소", "전열을 지키는 근거리 병력", MvpBuildingPhaseSample.CreateSlot().Build.GoldAmount),
                new BuildingCatalogItem("archer", "궁사 훈련소", "후열에서 적을 공격", null),
                new BuildingCatalogItem("gold", "골드 생산소", "방어를 이어갈 골드 생산", null),
                new BuildingCatalogItem("support", "지원 건물", "아군의 전투를 보조", null)
            });
            _unitBinding.Initialize(_unitInfo);
            _unit.Initialize(null);
            _counts.TryRegister(_unitSource);
            _buildingInfo.HideBuildingInfo();
            _actions.HideActions();
            IsReady = true;
            HandlePhase(_flow.CurPhase);
        }

        private void OnDisable()
        {
            _catalog.ItemSelected -= HandleCatalogSelected;
            _unitButton.onClick.RemoveListener(HandleUnit);
            _campButton.onClick.RemoveListener(HandleCamp);
            _actions.ActionRequested -= HandleUnavailableAction;
            if (_flow != null) _flow.PhaseChanged -= HandlePhase;
        }

        private void HandleCatalogSelected(string id)
        {
            string name;
            string category;
            string description;
            string production = null;
            switch (id)
            {
                case "warrior": name = "전사 훈련소"; category = "유닛 생산"; description = "공격과 방어가 균형 잡힌 전열 병력을 준비합니다."; production = "전사 · 근거리"; break;
                case "archer": name = "궁사 훈련소"; category = "유닛 생산"; description = "전열 뒤에서 적을 공격하는 원거리 병력을 준비합니다."; production = "궁사 · 원거리"; break;
                case "gold": name = "골드 생산소"; category = "자원"; description = "건설과 강화에 사용할 골드를 생산합니다."; break;
                case "support": name = "지원 건물"; category = "지원"; description = "아군의 전투와 생존을 보조합니다."; break;
                default: return;
            }
            _buildingInfo.ShowBuildingInfo(new BuildingInfoData("preview-" + id, name, category, 1, description, production));
            // 현재 실제 건물/가격 API는 미연동이다. 원래 있던 전사 견적만 표시하고 실행은 막는다.
            var offer = id == "warrior" ? new BuildingActionOffer(BuildingUiAction.Build, name,
                MvpBuildingPhaseSample.CreateSlot().Build.GoldAmount, false, "건설 기능 준비 중", id) : null;
            _actions.ShowActions(new BuildingActionViewData("preview-" + id, name, offer));
        }

        private void HandleCamp()
        {
            _buildingInfo.ShowBuildingInfo(new BuildingInfoData("preview-camp", "베이스캠프", "거점", 1,
                "마계의 마지막 방어선입니다. 적의 침공으로부터 지켜내세요."));
            _actions.ShowActions(new BuildingActionViewData("preview-camp", "베이스캠프"));
        }

        private void HandleUnit() { if (IsReady) _unitBinding.TrySelect(_unitSource); }

        private void HandleUnavailableAction(BuildingActionRequest request) =>
            _actions.TryResolveRequest(request.RequestId, false, "건설 기능 준비 중");

        private void HandlePhase(GamePhase phase)
        {
            _actions.SetActionsAllowed(phase == GamePhase.Preparation);
            _hint.text = phase == GamePhase.Preparation ? "건설할 위치를 선택하세요" :
                phase == GamePhase.Battle ? "마계의 관문을 지켜내세요" : "";
        }
    }
}
