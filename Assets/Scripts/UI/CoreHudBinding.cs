using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>코어의 현재 단계·웨이브를 표시하고 시작 완료까지 중복 요청을 잠근다.</summary>
    public sealed class CoreHudBinding : MonoBehaviour
    {
        public bool IsStartPending => _isStartPending;

        [SerializeField] private GameUIController _ui;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private WaveController _waves;

        private CancellationTokenSource _bindingLifetime;
        private bool _isSubscribed;
        private bool _isStartPending;
        private int _requestVersion;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Initialize(GameUIController ui, GameFlowController flow, WaveController waves)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            Unbind();
            _ui = ui;
            _flow = flow;
            _waves = waves;
            if (isActiveAndEnabled) Bind();
        }

        public void Refresh()
        {
            if (_ui == null) return;
            if (_flow == null || _waves == null)
            {
                _ui.SetPhaseLabel("연결 대기");
                _ui.ClearWaveProgress();
                _ui.SetWaveStartInteractable(false);
                return;
            }

            _ui.SetPhaseLabel(GetPhaseLabel(_flow.CurPhase));
            if (_flow.CurPhase != GamePhase.None && _waves.CurWave >= 1 &&
                _waves.CurWave <= WaveController.MAX_WAVE)
                _ui.SetWaveProgress(_waves.CurWave, WaveController.MAX_WAVE);
            else
                _ui.ClearWaveProgress();

            // PhaseChanged는 코어의 전환 잠금 해제 전 발생한다. 여기서는 단계만 표시하고
            // 실제 실행 조건은 클릭 시 TryStartWave가 다시 검사한다.
            _ui.SetWaveStartInteractable(_isSubscribed && !_isStartPending &&
                _flow.CurPhase == GamePhase.Preparation);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.None)
            {
                // 리셋 전 요청의 비동기 완료가 새 판의 버튼/메시지를 덮어쓰지 않게 한다.
                _requestVersion++;
                _isStartPending = false;
                _ui.HideWaveReward();
                _ui.HideRunResult();
                _ui.HideMessage();
            }
            Refresh();
        }

        private void HandleWaveChanged(int currentWave)
        {
            Refresh();
        }

        private void HandleWaveStartRequested()
        {
            if (!_isSubscribed || _isStartPending) return;
            if (_flow == null || _waves == null)
            {
                Refresh();
                _ui.ShowMessage("코어 연결을 확인해주세요.");
                return;
            }

            StartWaveAsync().Forget();
        }

        private async UniTask StartWaveAsync()
        {
            int requestVersion = ++_requestVersion;
            CancellationToken token = _bindingLifetime.Token;
            _isStartPending = true;
            _ui.HideMessage();
            _ui.SetWaveStartInteractable(false);
            try
            {
                bool started = await _flow.TryStartWave().AttachExternalCancellation(token);
                if (IsCurrentRequest(requestVersion) && !started)
                    _ui.ShowMessage("웨이브를 시작하지 못했습니다. 현재 상태를 확인해주세요.");
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentRequest(requestVersion))
                    _ui.ShowMessage("웨이브 시작 요청이 취소되었습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (IsCurrentRequest(requestVersion))
                    _ui.ShowMessage("전투 준비 중 오류가 발생했습니다. 코어 상태를 확인해주세요.");
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

        private bool IsCurrentRequest(int requestVersion)
        {
            return this != null && isActiveAndEnabled && _isSubscribed &&
                _ui != null && requestVersion == _requestVersion;
        }

        private void Bind()
        {
            if (_isSubscribed || _ui == null || _flow == null || _waves == null) return;
            _bindingLifetime = new CancellationTokenSource();
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
                case GamePhase.Reward: return "보상";
                case GamePhase.Finished: return "결과";
                default: return "연결 대기";
            }
        }
    }
}
