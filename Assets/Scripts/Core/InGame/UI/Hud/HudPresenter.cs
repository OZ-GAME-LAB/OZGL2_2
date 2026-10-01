using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>현재 재화·진행 상태를 표시하고 웨이브 시작 입력을 Core에 전달한다.</summary>
    public sealed class HudPresenter : MonoBehaviour
    {
        public bool IsStartPending => _isStartPending;

        [SerializeField] private GameHudView _ui;
        [SerializeField] private TMP_Text _quarterText;
        [SerializeField] private TMP_Text _gemText;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private MessageView _messages;

        private RunCurrencyManager _wallet;
        private GameFlowController _flow;
        private WaveController _waves;
        private CancellationTokenSource _bindingLifetime;
        private bool _isSubscribed;
        private bool _isStartPending;
        private int _requestVersion;

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        public void Initialize(GameHudView ui, RunCurrencyManager wallet,
            GameFlowController flow, WaveController waves)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            Unbind();
            _ui = ui;
            _wallet = wallet;
            _flow = flow;
            _waves = waves;
            if (isActiveAndEnabled) Bind();
        }

        public void Refresh()
        {
            if (_ui == null) return;
            RefreshCurrency();
            if (_flow == null || _waves == null)
            {
                _ui.SetPhaseLabel("연결 대기");
                _ui.ClearWaveProgress();
                SetText(_quarterText, "분기 -- · 웨이브");
                SetText(_hint, string.Empty);
                _ui.SetWaveStartInteractable(false);
                return;
            }

            _ui.SetPhaseLabel(GetPhaseLabel(_flow.CurPhase));
            SetText(_quarterText, _flow.CurPhase != GamePhase.None && _waves.CurQuarter >= 1
                ? $"분기 {_waves.CurQuarter} · 웨이브" : "분기 -- · 웨이브");
            if (_flow.CurPhase != GamePhase.None && _waves.CurWave >= 1 &&
                _waves.CurWave <= WaveController.MAX_WAVE)
                _ui.SetWaveProgress(_waves.CurWave, WaveController.MAX_WAVE);
            else
                _ui.ClearWaveProgress();

            SetText(_hint, _flow.CurPhase == GamePhase.Preparation ? "건설할 위치를 선택하세요" :
                _flow.CurPhase == GamePhase.Battle || _flow.CurPhase == GamePhase.BattlePreparing
                    ? "전투 중에는 건설할 수 없습니다" : string.Empty);
            // PhaseChanged는 전환 잠금 해제 전에 발생한다. 실행 조건은 Core가 다시 확인한다.
            _ui.SetWaveStartInteractable(_isSubscribed && !_isStartPending &&
                _flow.CurPhase == GamePhase.Preparation && _waves.CurrentPreset != null);
        }

        private void RefreshCurrency()
        {
            if (_wallet == null || !_wallet.IsInitialized)
            {
                _ui.ClearGold();
                SetText(_gemText, "보석 --");
                return;
            }
            _ui.SetGold(_wallet.GetBalance(CurrencyType.Gold));
            SetText(_gemText, $"보석 {_wallet.GetBalance(CurrencyType.Gem):N0}");
        }

        private void HandleBalanceChanged(CurrencyData currency, int previous, int current)
        {
            if (currency == null || _ui == null) return;
            // 재화 갱신은 현재 팝업의 키보드 포커스를 바꾸지 않는다.
            if (currency.Type == CurrencyType.Gold) _ui.SetGold(current);
            else if (currency.Type == CurrencyType.Gem) SetText(_gemText, $"보석 {current:N0}");
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.None)
            {
                _requestVersion++;
                _isStartPending = false;
                _messages?.Hide();
            }
            Refresh();
        }

        private void HandleWaveChanged(WaveInfo info) => Refresh();

        private void HandleWaveStartRequested()
        {
            if (!_isSubscribed || _isStartPending) return;
            if (_flow == null || _waves == null)
            {
                Refresh();
                _messages?.Show("코어 연결을 확인해주세요.");
                return;
            }
            StartWaveAsync().Forget();
        }

        private async UniTask StartWaveAsync()
        {
            int requestVersion = ++_requestVersion;
            CancellationToken token = _bindingLifetime.Token;
            _isStartPending = true;
            _messages?.Hide();
            _ui.SetWaveStartInteractable(false);
            try
            {
                bool started = await _flow.TrySpawnUnits().AttachExternalCancellation(token);
                if (IsCurrentRequest(requestVersion) && !started)
                    _messages?.Show("웨이브를 시작하지 못했습니다. 현재 상태를 확인해주세요.");
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentRequest(requestVersion))
                    _messages?.Show("웨이브 시작 요청이 취소되었습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (IsCurrentRequest(requestVersion))
                    _messages?.Show("전투 준비 중 오류가 발생했습니다. 코어 상태를 확인해주세요.");
            }
            finally
            {
                if (IsCurrentRequest(requestVersion))
                {
                    _isStartPending = false;
                    Refresh();
                }
            }
        }

        private bool IsCurrentRequest(int requestVersion) =>
            this != null && isActiveAndEnabled && _isSubscribed &&
            _ui != null && requestVersion == _requestVersion;

        private void Bind()
        {
            if (_isSubscribed || _ui == null || _wallet == null || _flow == null || _waves == null) return;
            _bindingLifetime = new CancellationTokenSource();
            _wallet.BalanceChanged += HandleBalanceChanged;
            _flow.PhaseChanged += HandlePhaseChanged;
            _waves.WaveChanged += HandleWaveChanged;
            _ui.WaveStartRequested += HandleWaveStartRequested;
            _isSubscribed = true;
            Refresh();
        }

        private void Unbind()
        {
            _requestVersion++;
            _isStartPending = false;
            if (_isSubscribed)
            {
                if (_wallet != null) _wallet.BalanceChanged -= HandleBalanceChanged;
                if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
                if (_waves != null) _waves.WaveChanged -= HandleWaveChanged;
                if (_ui != null)
                {
                    _ui.WaveStartRequested -= HandleWaveStartRequested;
                    _ui.SetWaveStartInteractable(false);
                }
            }
            _isSubscribed = false;
            _bindingLifetime?.Cancel();
            _bindingLifetime?.Dispose();
            _bindingLifetime = null;
        }

        private static string GetPhaseLabel(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Preparation: return "건설";
                case GamePhase.BattlePreparing: return "전투 준비";
                case GamePhase.Battle: return "전투";
                case GamePhase.BattleResolving: return "전투 정산";
                case GamePhase.QuarterComplete: return "분기 완료";
                case GamePhase.Reward: return "보상";
                case GamePhase.Event: return "이벤트";
                case GamePhase.Store: return "상점";
                case GamePhase.Finished: return "결과";
                default: return "연결 대기";
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }
    }
}
