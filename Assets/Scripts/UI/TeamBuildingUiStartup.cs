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
        [SerializeField] private TMP_Text _gemText;
        [SerializeField] private TMP_Text _hint;

        private void OnEnable()
        {
            if (_wallet != null) _wallet.BalanceChanged += HandleBalanceChanged;
            if (_flow != null) _flow.PhaseChanged += HandlePhaseChanged;
            if (IsReady) Refresh();
        }

        private void Start()
        {
            if (_ui == null || _wallet == null || _waves == null || _flow == null || _gold == null || _core == null)
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
            Refresh();
            if (!IsReady) _ui.ShowMessage("재화 연결을 확인해 주세요.");
        }

        private void OnDisable()
        {
            if (_wallet != null) _wallet.BalanceChanged -= HandleBalanceChanged;
            if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
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
