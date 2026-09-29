using System;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Game.UI
{
    /// <summary>
    /// Core가 소유한 진행 상태를 플레이어용 보상·결과 UI에 연결한다.
    /// 전투 판정, 재화 지급, 노드 이동은 직접 수행하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoreGameLoopUiBinding : MonoBehaviour
    {
        public bool IsReady => _ui != null && _flow != null && _waves != null &&
            _wallet != null && _contentGate != null;

        [SerializeField] private GameUIController _ui;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private WaveController _waves;
        [SerializeField] private RunCurrencyManager _wallet;
        [SerializeField] private TestWaitingScript _contentGate;

        private bool _isSubscribed;
        private bool _isReloading;

        private void OnEnable()
        {
            Bind();
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Initialize(GameUIController ui, GameFlowController flow, WaveController waves,
            RunCurrencyManager wallet, TestWaitingScript contentGate)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (contentGate == null) throw new ArgumentNullException(nameof(contentGate));

            Unbind();
            _ui = ui;
            _flow = flow;
            _waves = waves;
            _wallet = wallet;
            _contentGate = contentGate;
            if (isActiveAndEnabled) Bind();
            Refresh();
        }

        public void Refresh()
        {
            if (!IsReady) return;

            switch (_flow.CurPhase)
            {
                case GamePhase.Reward:
                    _ui.ShowProgressPrompt(CreateRewardMessage(), true);
                    break;

                case GamePhase.Event:
                    _ui.ShowProgressPrompt("돌발 이벤트를 확인했습니다.\n계속해서 다음 전투를 준비합니다.", true);
                    break;

                case GamePhase.Store:
                    _ui.ShowProgressPrompt("상점 이용을 마쳤습니다.\n계속해서 다음 전투를 준비합니다.", true);
                    break;

                case GamePhase.Finished:
                    _ui.ShowRunResult(_flow.HasClearedMainGame,
                        _wallet.GetBalance(CurrencyType.Gold));
                    break;

                case GamePhase.None:
                case GamePhase.Preparation:
                case GamePhase.BattlePreparing:
                case GamePhase.Battle:
                case GamePhase.BattleResolving:
                case GamePhase.QuarterComplete:
                    _ui.HideWaveReward();
                    if (_flow.CurPhase != GamePhase.Finished) _ui.HideRunResult();
                    break;
            }
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            Refresh();
        }

        private void HandleContinueRequested()
        {
            if (!IsReady) return;
            if (_flow.CurPhase != GamePhase.Reward &&
                _flow.CurPhase != GamePhase.Event &&
                _flow.CurPhase != GamePhase.Store)
            {
                Refresh();
                return;
            }

            _ui.HideWaveReward();
            _contentGate.ChooseResultBtn();
        }

        private void HandleRestartRequested()
        {
            if (!IsReady || _flow.CurPhase != GamePhase.Finished || _isReloading) return;
            _isReloading = true;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                _isReloading = false;
                _ui.ShowMessage("현재 씬을 다시 시작할 수 없습니다.");
                return;
            }

#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                EditorSceneManager.LoadSceneInPlayMode(scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            if (scene.buildIndex < 0)
            {
                _isReloading = false;
                _ui.ShowMessage("Build Settings에 현재 씬을 추가해주세요.");
                return;
            }

            SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
        }

        private string CreateRewardMessage()
        {
            int gold = Mathf.Max(0, _wallet.CurrentGoldReward);
            int gems = Mathf.Max(0, _wallet.CurrentGemReward);
            return gems > 0
                ? $"웨이브 보상\n골드 +{gold:N0}  ·  보석 +{gems:N0}"
                : $"웨이브 보상\n골드 +{gold:N0}";
        }

        private void Bind()
        {
            if (_isSubscribed || !IsReady) return;
            _flow.PhaseChanged += HandlePhaseChanged;
            _ui.ContinueRequested += HandleContinueRequested;
            _ui.RestartRequested += HandleRestartRequested;
            _isSubscribed = true;
        }

        private void Unbind()
        {
            if (!_isSubscribed) return;
            if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
            if (_ui != null)
            {
                _ui.ContinueRequested -= HandleContinueRequested;
                _ui.RestartRequested -= HandleRestartRequested;
            }
            _isSubscribed = false;
        }
    }
}
