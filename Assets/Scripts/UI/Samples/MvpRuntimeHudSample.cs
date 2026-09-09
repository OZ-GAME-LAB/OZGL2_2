using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>실제 재화·코어를 사용하는 독립 통합 샘플. 전투/보상 입력은 테스트용이다.</summary>
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

        private int _rewardedWave;

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
        }

        private void Start()
        {
            _ui.Initialize();
            _rewardGate.Initialize(_flow, _waves);
            _flow.Initialize(_waves, _rewardGate);
            _waves.Initialize(_flow, new TestSpawner());
            _goldBinding.Initialize(_ui, _currencyManager);
            _coreBinding.Initialize(_ui, _flow, _waves);
            IsReady = _currencyManager.TryInitialize();
            _goldBinding.Refresh();
            if (!IsReady)
            {
                _statusText.text = "재화 초기화 실패: Console 확인";
                return;
            }
            _flow.BeginRun();
            _statusText.text = "실제 잔액·코어 연결 완료 / 전투 판정과 보상 수치는 테스트 입력";
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
        }

        private void HandleAddGold()
        {
            _statusText.text = _currencyManager.TryAdd(new CurrencyAmount(_gold, 50))
                ? "실제 재화 매니저: 골드 +50" : "골드 추가 실패";
        }

        private void HandleSpendGold()
        {
            _statusText.text = _currencyManager.TrySpend(new CurrencyAmount(_gold, 30))
                ? "실제 재화 매니저: 골드 -30" : "골드 부족: 잔액 유지";
        }

        private void HandleRejectSpend()
        {
            _statusText.text = _currencyManager.TrySpend(new CurrencyAmount(_gold, 10000))
                ? "골드 10,000 사용" : "골드 부족: 차감 없이 거절";
        }

        private void HandleWin()
        {
            _waves.SetSuccess();
            _statusText.text = "테스트 적 전멸 입력 / 보상 처리 버튼으로 이어서 진행";
        }

        private void HandleReward()
        {
            if (_flow.CurPhase != GamePhase.Reward || _rewardedWave == _waves.CurWave) return;
            if (!_currencyManager.TryApplyWaveReward(new CurrencyAmount(_gold, 30)))
            {
                _statusText.text = "테스트 보상 지급 실패 / 재시도 가능";
                return;
            }
            _rewardedWave = _waves.CurWave;
            // 테스트 보상이 실제 매니저에 반영된 뒤에만 코어의 테스트 대기를 완료한다.
            _rewardGate.ChooseResultBtn();
            _statusText.text = "테스트 보상 +30 처리 완료 / 다음 코어 상태 대기";
        }

        private void HandleLose()
        {
            _waves.SetFail();
            _statusText.text = "테스트 아군 전멸 입력 / 최종 승패 패널용 데이터 API는 연결 대기";
        }

        private void HandleReset()
        {
            _currencyManager.TryEndRun();
            IsReady = _currencyManager.TryInitialize();
            _goldBinding.Refresh();
            if (!IsReady) return;
            _rewardedWave = 0;
            _flow.ResetRun();
            _statusText.text = "실제 재화·코어 리셋 완료";
        }

        private void HandleToggleHud()
        {
            _ui.gameObject.SetActive(!_ui.gameObject.activeSelf);
            _statusText.text = _ui.gameObject.activeSelf
                ? "HUD 다시 표시 / 최신 잔액·단계 동기화" : "HUD 숨김 / 코어·재화는 계속 유지";
        }
    }
}
