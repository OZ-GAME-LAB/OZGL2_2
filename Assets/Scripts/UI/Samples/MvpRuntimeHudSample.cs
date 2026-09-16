using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>실제 재화·코어를 사용하는 독립 수동 지급 테스트. 자동 지급은 TeamBuildingUiStartup에서 연결한다.</summary>
    public sealed class MvpRuntimeHudSample : MonoBehaviour
    {
        public bool IsReady { get; private set; }

        [SerializeField] private GameUIController _ui;
        [SerializeField] private RunCurrencyManager _currencyManager;
        [SerializeField] private CurrencyData _gold;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private WaveController _waves;
        [SerializeField] private TestWaitingScript _rewardGate;
        [SerializeField] private CoreHudBinding _coreBinding;
        [SerializeField] private RunGoldHudBinding _goldBinding;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _addGoldButton;
        [SerializeField] private Button _spendGoldButton;
        [SerializeField] private Button _rejectSpendButton;
        [SerializeField] private Button _winButton;
        [SerializeField] private Button _rewardButton;
        [SerializeField] private Button _loseButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _toggleHudButton;
        [SerializeField] private Button _lastWaveButton;
        [SerializeField] private Button _lastQuarterButton;
        [SerializeField] private CoreRunDecisionBinding _runDecisionBinding;

        [SerializeField] private ArtifactRewardBinding _artifactRewards;
        [SerializeField] private ArtifactManager _artifactManager;
        [SerializeField] private EffectManager _effectManager;

        private string _runId;
        private string _pendingRewardId;
        private int _awardedGold;
        private int _awardedGems;
        private int _rewardVersion;
        private bool _rewardGateReady;
        private bool _rewardPaid;
        private bool _gateReleased;
        private bool _rewardFaulted;
        private bool UsesArtifactRewards => _artifactRewards != null;
        private bool IsTransactionBusy => _isApplyingReward || (_artifactRewards != null && _artifactRewards.IsApplying);

        private int _rewardedWave;
        private int _rewardedQuarter;
        private bool _isApplyingReward;

        private void OnEnable()
        {
            _addGoldButton.onClick.AddListener(HandleAddGold);
            _spendGoldButton.onClick.AddListener(HandleSpendGold);
            _rejectSpendButton.onClick.AddListener(HandleRejectSpend);
            _winButton.onClick.AddListener(HandleWin);
            _rewardButton.onClick.AddListener(HandleReward);
            _loseButton.onClick.AddListener(HandleLose);
            _resetButton.onClick.AddListener(HandleReset);
            _toggleHudButton.onClick.AddListener(HandleToggleHud);
            if (_lastWaveButton != null) _lastWaveButton.onClick.AddListener(HandleLastWave);
            if (_lastQuarterButton != null) _lastQuarterButton.onClick.AddListener(HandleLastQuarter);
            _flow.PhaseChanged += HandlePhaseChanged;
            if (_artifactRewards != null) _artifactRewards.Completed += HandleArtifactCompleted;
            if (IsReady) HandlePhaseChanged(_flow.CurPhase);
        }

        private void Start()
        {
            _ui.Initialize();
            _rewardGate.Initialize(_flow, _waves);
            _flow.Initialize(_waves, _rewardGate, _artifactManager); //
            _waves.Initialize(_flow, new TestSpawner());
            _goldBinding.Initialize(_ui, _currencyManager);
            _coreBinding.Initialize(_ui, _flow, _waves);
            if (_runDecisionBinding != null) _runDecisionBinding.Initialize(_flow, _waves);
            _runId = Guid.NewGuid().ToString("N");
            InitializeRewardSystems();
            _goldBinding.Refresh();
            if (!IsReady)
            {
                _statusText.text = "재화 초기화 실패: Console 확인";
                return;
            }
            var rewardLabel = _rewardButton.GetComponentInChildren<TMP_Text>(true);
            if (rewardLabel != null) rewardLabel.text = UsesArtifactRewards ? "승리 보상 다시 열기" : "웨이브 보상 지급";
            _flow.BeginRun();
            _statusText.text = "실제 재화·코어 연결 / 전투 판정은 테스트 입력 · 승리 유물 연동: " + UsesArtifactRewards;
        }

        private void OnDisable()
        {
            _addGoldButton.onClick.RemoveListener(HandleAddGold);
            _spendGoldButton.onClick.RemoveListener(HandleSpendGold);
            _rejectSpendButton.onClick.RemoveListener(HandleRejectSpend);
            _winButton.onClick.RemoveListener(HandleWin);
            _rewardButton.onClick.RemoveListener(HandleReward);
            _loseButton.onClick.RemoveListener(HandleLose);
            _resetButton.onClick.RemoveListener(HandleReset);
            _toggleHudButton.onClick.RemoveListener(HandleToggleHud);
            if (_lastWaveButton != null) _lastWaveButton.onClick.RemoveListener(HandleLastWave);
            if (_lastQuarterButton != null) _lastQuarterButton.onClick.RemoveListener(HandleLastQuarter);
            _flow.PhaseChanged -= HandlePhaseChanged;
            if (_artifactRewards != null) _artifactRewards.Completed -= HandleArtifactCompleted;
            _rewardVersion++;
            _rewardGateReady = false;
        }

        private void HandleAddGold()
        {
            if (IsTransactionBusy) return;
            _statusText.text = _currencyManager.TryAdd(CurrencyType.Gold, 50)
                ? "실제 재화 매니저: 골드 +50" : "골드 추가 실패";
        }

        private void HandleSpendGold()
        {
            if (IsTransactionBusy) return;
            _statusText.text = _currencyManager.TrySpend(CurrencyType.Gold, 30)
                ? "실제 재화 매니저: 골드 -30" : "골드 부족: 잔액 유지";
        }

        private void HandleRejectSpend()
        {
            if (IsTransactionBusy) return;
            _statusText.text = _currencyManager.TrySpend(CurrencyType.Gold, 10000)
                ? "골드 10,000 사용" : "골드 부족: 차감 없이 거절";
        }

        private void HandleWin()
        {
            if (IsTransactionBusy) return;
            _waves.SetSuccess();
            _statusText.text = "테스트 적 전멸 입력 / 보상 처리 버튼으로 이어서 진행";
        }

        private void HandleReward()
        {
            if (!isActiveAndEnabled || !IsReady || IsTransactionBusy || _rewardFaulted ||
                _flow.CurPhase != GamePhase.Reward || (UsesArtifactRewards && !_rewardGateReady)) return;
            bool sameWave = _rewardedQuarter == _waves.CurQuarter && _rewardedWave == _waves.CurWave;
            if (sameWave && _gateReleased) return;

            _isApplyingReward = true;
            try
            {
                if (!sameWave)
                {
                    _rewardedQuarter = _waves.CurQuarter;
                    _rewardedWave = _waves.CurWave;
                    _pendingRewardId = _runId + "/" + _rewardedQuarter + "/" + _rewardedWave;
                    _rewardPaid = false;
                    _gateReleased = false;
                }
                if (!_rewardPaid)
                {
                    int beforeGold = _currencyManager.GetBalance(CurrencyType.Gold);
                    int beforeGems = _currencyManager.GetBalance(CurrencyType.Gem);
                    if (!_currencyManager.TryApplyWaveReward())
                    {
                        _statusText.text = "보상 지급 실패 / 테이블·잔액 확인 후 재시도";
                        return;
                    }
                    _rewardPaid = true;
                    // 이 독립 샘플의 동기 거래 전후 차이. 프로덕션에는 담당자의 지급 결과 데이터가 필요하다.
                    _awardedGold = _currencyManager.GetBalance(CurrencyType.Gold) - beforeGold;
                    _awardedGems = _currencyManager.GetBalance(CurrencyType.Gem) - beforeGems;
                }
                if (UsesArtifactRewards)
                {
                    _statusText.text = "재화 지급 완료 / 유물 선택 또는 모두 포기 대기";
                    if (!_artifactRewards.TryShowReward(_pendingRewardId, _awardedGold, _awardedGems))
                        _statusText.text = "재화는 지급됨 / 유물 후보·참조 확인 후 다시 열기";
                    else if (!_artifactRewards.IsChoosing && !_artifactRewards.IsFaulted)
                        HandleArtifactCompleted(_pendingRewardId);
                }
                else ReleaseRewardGate();
            }
            catch (Exception exception)
            {
                _rewardFaulted = true;
                _statusText.text = "보상 처리 오류 / 중복 지급 방지를 위해 중단: Console 확인";
                Debug.LogException(exception, this);
            }
            finally { _isApplyingReward = false; }
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            _rewardVersion++;
            _rewardGateReady = false;
            // 수동 테스트에서도 전투 시작 시 보상을 고정한다. 실제 씬의 자동 지급 구독과 혼합하지 않는다.
            if (phase == GamePhase.BattlePreparing && IsReady && !_currencyManager.TryPrepareWaveReward())
                _statusText.text = "보상 준비 실패 / 팀 보상 테이블 확인";
            if (!UsesArtifactRewards) return;
            if (phase == GamePhase.Reward && IsReady)
                OpenRewardAfterGateAsync(_rewardVersion).Forget();
            else if (phase == GamePhase.None || phase == GamePhase.Finished)
            {
                _pendingRewardId = null;
                _artifactRewards.ResetReward();
            }
        }

        private async UniTask OpenRewardAfterGateAsync(int version)
        {
            // PhaseChanged는 Core의 WaitToggle 설치보다 먼저 발생한다. 다음 프레임에만 대기를 해제한다.
            await UniTask.NextFrame();
            if (this == null || !isActiveAndEnabled || version != _rewardVersion ||
                !IsReady || _flow.CurPhase != GamePhase.Reward) return;
            _rewardGateReady = true;
            HandleReward();
        }

        private void HandleArtifactCompleted(string rewardId)
        {
            if (!isActiveAndEnabled || !IsReady || !_rewardPaid || _gateReleased ||
                rewardId != _pendingRewardId || _flow.CurPhase != GamePhase.Reward || !_rewardGateReady) return;
            ReleaseRewardGate();
        }

        private void ReleaseRewardGate()
        {
            _gateReleased = true;
            _rewardGate.ChooseResultBtn();
            _statusText.text = "보상 처리 완료 / 다음 코어 상태 대기";
        }

        private void InitializeRewardSystems()
        {
            bool artifactsReady = !UsesArtifactRewards;
            if (UsesArtifactRewards && _artifactManager != null && _effectManager != null)
            {
                if (!_artifactManager.IsInitialized) _artifactManager.Initialize(_waves, _effectManager);
                artifactsReady = _artifactRewards.TryInitialize(_artifactManager);
            }
            // 이 샘플은 지급 실패/재시도를 검사하는 유일한 지급 주체다.
            // flow=null로 자동 지급을 구독하지 않는다. 실제 팀 씬은 flow를 전달하고 UI에서 지급하지 않는다.
            _currencyManager.Initialize(_waves, null, _effectManager);
            IsReady = _currencyManager.IsInitialized && artifactsReady;
        }

        private void HandleLose()
        {
            if (IsTransactionBusy) return;
            _waves.SetFail();
            _statusText.text = "테스트 아군 전멸 입력 / 최종 승패 패널용 데이터 API는 연결 대기";
        }

        private void HandleReset()
        {
            if (IsTransactionBusy) return;
            _isApplyingReward = true;
            try
            {
                _rewardVersion++;
                _rewardGateReady = false;
                if (_artifactRewards != null) _artifactRewards.ResetReward();
                if (_artifactManager != null && _artifactManager.IsInitialized && !_artifactManager.TryEndRun())
                {
                    _statusText.text = "유물 종료 실패 / 현재 플레이 유지: Console 확인";
                    return;
                }
                _currencyManager.TryEndRun();
                InitializeRewardSystems();
                _goldBinding.Refresh();
                if (!IsReady)
                {
                    _statusText.text = "재화·유물 초기화 실패: Console 확인";
                    return;
                }
                _runId = Guid.NewGuid().ToString("N");
                _pendingRewardId = null;
                _rewardedWave = 0;
                _rewardedQuarter = 0;
                _rewardPaid = false;
                _gateReleased = false;
                _rewardFaulted = false;
                _flow.ResetRun();
                _statusText.text = "실제 재화·코어·유물 리셋 완료";
            }
            finally { _isApplyingReward = false; }
        }

        private void HandleToggleHud()
        {
            _ui.gameObject.SetActive(!_ui.gameObject.activeSelf);
            _statusText.text = _ui.gameObject.activeSelf
                ? "HUD 다시 표시 / 최신 잔액·단계 동기화" : "HUD 숨김 / 코어·재화는 계속 유지";
        }

        private void HandleLastWave()
        {
            if (IsTransactionBusy) return;
            _waves.JumpToLastWaveForTest();
        }

        private void HandleLastQuarter()
        {
            if (IsTransactionBusy) return;
            _waves.JumpToLastQuarterForTest();
        }
    }
}
