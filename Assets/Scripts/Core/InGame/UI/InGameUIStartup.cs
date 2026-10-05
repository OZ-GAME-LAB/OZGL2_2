using Game.Cameras;
using Game.Core;
using OZGL.KDH;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>UI 내부 참조를 보관하고 화면과 게임 시스템의 연결을 초기화한다.</summary>
    [DisallowMultipleComponent]
    public sealed class InGameUIStartup : MonoBehaviour
    {
        [SerializeField] private InGameUIManager _uiManager;
        [SerializeField] private GameHudView _hudView;
        [SerializeField] private HudPresenter _hudPresenter;
        [SerializeField] private ArtifactRewardPresenter _artifactSelectionUI;
        [SerializeField] private ContinueView _continueUI;
        [SerializeField] private RunDecisionView _runDecisionUI;
        [SerializeField] private SettlementView _settlementUI;
        [SerializeField] private ShopView _shopView;
        [SerializeField] private BuildingUIPresenter _buildingUI;
        [SerializeField] private BuildingUIConnection _buildingUIConnection;

        public InGameUIManager UIManager => _uiManager;
        public IArtifactSelectionUI ArtifactSelectionUI => _artifactSelectionUI;
        public IContinueUI ContinueUI => _continueUI;
        public IRunDecisionUI RunDecisionUI => _runDecisionUI;
        public IRunSettlementUI SettlementUI => _settlementUI;
        public IShopUI ShopUI => _shopView;
        public bool IsReady { get; private set; }

        // 비활성 UI 루트도 연결한다. 게임 시작과 루트 활성화는 Bootstrap이 담당한다.
        public bool Initialize(RunCurrencyManager wallet, GameFlowController flow, WaveController waves,
            BuildingBuildController controller, BuildingCoreProgress core, BuildingSlot[] slots, InGameCameraController cameraController)
        {
            IsReady = false;
            if (_uiManager == null || _hudView == null || _hudPresenter == null ||
                _artifactSelectionUI == null || _continueUI == null || _runDecisionUI == null ||
                _settlementUI == null || _shopView == null || _buildingUI == null || _buildingUIConnection == null)
            {
                Debug.LogError("[InGameUIStartup] UI 연결 참조를 확인해주세요.", this);
                return false;
            }
            if (!_uiManager.InitializeScreens()) return false;
            _shopView.Initialize(wallet);
            _hudView.Initialize(cameraController);
            _hudPresenter.Initialize(_hudView, wallet, flow, waves);
            flow.InitializeUI(_continueUI, _runDecisionUI);
            _buildingUIConnection.Initialize(controller, wallet, flow, core, slots, _buildingUI);
            if (!_buildingUIConnection.IsReady) return false;
            IsReady = true;
            _uiManager.ShowHud();
            return true;
        }
    }
}
