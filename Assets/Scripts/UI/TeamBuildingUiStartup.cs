using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>팀 건설 씬 복사본의 표시·재화 초기화만 연결한다. Core의 시작/승패는 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class TeamBuildingUiStartup : MonoBehaviour
    {
        public bool IsReady { get; private set; }

        [SerializeField] private GameUIController _ui;
        [SerializeField] private RunCurrencyManager _wallet;
        [SerializeField] private WaveController _waves;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private EffectManager _effects;
        [SerializeField] private BuildingCoreProgress _coreProgress;
        [SerializeField] private RunGoldHudBinding _gold;
        [SerializeField] private CoreHudBinding _core;
        [SerializeField] private CoreGameLoopUiBinding _gameLoop;
        [SerializeField] private CoreRunDecisionBinding _runDecision;
        [SerializeField] private TestWaitingScript _contentGate;
        [SerializeField] private TMP_Text _gemText;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private PlayerUiNavigation _navigation;
        [SerializeField] private RuntimeBuildingUiBinding _buildingUi;
        [SerializeField] private RuntimeBuildingSlotButton[] _slotButtons;

        private bool _isSubscribed;

        private void OnEnable()
        {
            Bind();
            if (IsReady) Refresh();
        }

        private void Start()
        {
            /*
            if (_ui == null || _wallet == null || _waves == null || _flow == null || _gold == null ||
                _core == null || _gameLoop == null || _runDecision == null || _contentGate == null)
            {
                Debug.LogError("[UI/TeamBuildingUiStartup] 필수 UI/팀 시스템 참조가 없습니다.", this);
                return;
            }
            _ui.Initialize();
            _gold.Initialize(_ui, _wallet);
            _core.Initialize(_ui, _flow, _waves);
            // 팀 원본 테스트 패널은 복사 씬에서 비활성화한다. UI는 Run 시작만 한 번 연결한다.
            if (!_wallet.IsInitialized)
            {
                if (_coreProgress == null) _coreProgress = FindFirstObjectByType<BuildingCoreProgress>();
                if (_coreProgress == null)
                {
                    Debug.LogError("[UI/TeamBuildingUiStartup] BuildingCoreProgress 참조가 없습니다.", this);
                    return;
                }
                _wallet.Initialize(_waves, _flow, _effects, _coreProgress);
            }
            IsReady = _wallet.IsInitialized;
            _gameLoop.Initialize(_ui, _flow, _waves, _wallet, _contentGate);
            _runDecision.Initialize(_flow, _waves);
            Refresh();
            if (!IsReady) _ui.ShowMessage("재화 연결을 확인해 주세요.");
            */
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Initialize(
                    RunCurrencyManager wallet,
                    WaveController waves,
                    GameFlowController flow,
                    EffectManager effects,
                    BuildingCoreProgress building,
                    TestWaitingScript contentGate,
                    BuildingBuildController controller,
                    BuildingSlot[] slots)
        {
            Unbind();
            IsReady = false;
            _wallet = wallet;
            _waves = waves;
            _flow = flow;
            _effects = effects;
            _coreProgress = building;
            _contentGate = contentGate;

            if (_ui == null || _wallet == null || _waves == null || _flow == null || _gold == null ||
                _core == null || _gameLoop == null || _runDecision == null || _contentGate == null ||
                _navigation == null || _buildingUi == null || _slotButtons == null ||
                controller == null || _coreProgress == null || slots == null)
            {
                Debug.LogError("[UI/TeamBuildingUiStartup] 필수 UI/팀 시스템 참조가 없습니다.", this);
                return;
            }

            _ui.Initialize();
            _gold.Initialize(_ui, _wallet);
            _core.Initialize(_ui, _flow, _waves);
            // 팀 원본 테스트 패널은 복사 씬에서 비활성화한다. UI는 Run 시작만 한 번 연결한다.
            if (!_wallet.IsInitialized)
            {
                if (_coreProgress == null) _coreProgress = FindFirstObjectByType<BuildingCoreProgress>();
                if (_coreProgress == null)
                {
                    Debug.LogError("[UI/TeamBuildingUiStartup] BuildingCoreProgress 참조가 없습니다.", this);
                    return;
                }
                _wallet.Initialize(_waves, _flow, _effects, _coreProgress);
            }
            _gameLoop.Initialize(_ui, _flow, _waves, _wallet, _contentGate);
            _runDecision.Initialize(_flow, _waves);
            _buildingUi.Initialize(controller, _wallet, _flow, _coreProgress, slots);
            _navigation.Initialize(_flow);
            foreach (var button in _slotButtons)
                if (button != null) button.Initialize(_flow);
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var target = slot.GetComponent<RuntimeBuildingSelectionTarget>();
                if (target != null) target.Initialize(_buildingUi, slot, _flow);
            }
            IsReady = _wallet.IsInitialized;
            if (isActiveAndEnabled) Bind();
            Refresh();
            if (!IsReady) _ui.ShowMessage("재화 연결을 확인해 주세요.");
        }
        public void Refresh()
        {
            if (_gold != null) _gold.Refresh();
            if (_core != null) _core.Refresh();
            if (_gemText != null)
                _gemText.text = _wallet != null && _wallet.IsInitialized
                    ? $"보석 {_wallet.GetBalance(CurrencyType.Gem):N0}" : "보석 --";
            if (_flow != null) HandlePhaseChanged(_flow.CurPhase);
        }

        private void Bind()
        {
            if (_isSubscribed || !IsReady) return;
            _wallet.BalanceChanged += HandleBalanceChanged;
            _flow.PhaseChanged += HandlePhaseChanged;
            _isSubscribed = true;
        }

        private void Unbind()
        {
            if (!_isSubscribed) return;
            if (_wallet != null) _wallet.BalanceChanged -= HandleBalanceChanged;
            if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
            _isSubscribed = false;
        }

        private void HandleBalanceChanged(CurrencyData currency, int previous, int current)
        {
            if (currency != null && currency.Type == CurrencyType.Gem && _gemText != null)
                _gemText.text = $"보석 {current:N0}";
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (_hint == null) return;
            _hint.text = phase == GamePhase.Preparation ? "건설할 위치를 선택하세요" :
                phase == GamePhase.Battle || phase == GamePhase.BattlePreparing
                    ? "전투 중에는 건설할 수 없습니다" : "";
        }
    }
}
