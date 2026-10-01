using System;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Game.UI.InGame
{
    /// <summary>
    /// Core가 소유한 진행 상태를 플레이어용 보상·결과 UI에 연결한다.
    /// 전투 판정, 재화 지급, 노드 이동은 직접 수행하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunFlowPresenter : MonoBehaviour
    {
        public bool IsReady => _ui != null && _flow != null && _waves != null &&
            _wallet != null && _contentGate != null && _artifacts != null && _artifactUi != null;

        [SerializeField] private GameHudView _ui;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private WaveController _waves;
        [SerializeField] private RunCurrencyManager _wallet;
        [SerializeField] private TestWaitingScript _contentGate;

        private bool _isSubscribed;
        private bool _isReloading;
        private ArtifactManager _artifacts;
        private ArtifactRewardPresenter _artifactUi;
        private bool _rewardAcknowledged;
        private bool _pendingArtifactOpen;
        private int _acknowledgedFrame;
        private int _rewardSequence;

        private void Update()
        {
            // Reward 이벤트는 매니저 후보 준비보다 먼저 발생한다. 클릭 다음 프레임에 읽는다.
            if (!_pendingArtifactOpen || !IsReady || Time.frameCount <= _acknowledgedFrame) return;
            _pendingArtifactOpen = false;
            if (_flow.CurPhase != GamePhase.Reward) return;
            if (_artifacts == null || !_artifacts.IsInitialized || _artifactUi == null)
            {
                ShowRewardConnectionError();
                return;
            }
            if (!_artifacts.IsSelectingReward)
            {
                // 후보가 없으면 매니저가 이미 완료했다. Core의 기존 완료 입력만 전달한다.
                _contentGate.ChooseResultBtn();
                return;
            }
            if (!_artifactUi.TryInitialize(_artifacts) ||
                !_artifactUi.TryShowReward("team-reward-" + _rewardSequence,
                    _wallet.CurrentGoldReward, _wallet.CurrentGemReward))
                ShowRewardConnectionError();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Initialize(GameHudView ui, GameFlowController flow, WaveController waves,
            RunCurrencyManager wallet, TestWaitingScript contentGate,
            ArtifactManager artifacts, ArtifactRewardPresenter artifactUi)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (contentGate == null) throw new ArgumentNullException(nameof(contentGate));
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            if (artifactUi == null) throw new ArgumentNullException(nameof(artifactUi));

            Unbind();
            _ui = ui;
            _flow = flow;
            _waves = waves;
            _wallet = wallet;
            _contentGate = contentGate;
            _artifacts = artifacts;
            _artifactUi = artifactUi;
            // 재초기화는 UI 대기만 다시 시작한다. 이미 준비/지급된 게임 보상은 변경하지 않는다.
            _rewardAcknowledged = false;
            _pendingArtifactOpen = false;
            if (isActiveAndEnabled) Bind();
        }

        public void Refresh()
        {
            if (!IsReady || !isActiveAndEnabled) return;

            switch (_flow.CurPhase)
            {
                case GamePhase.Reward:
                    if (!_rewardAcknowledged) _ui.ShowProgressPrompt(CreateRewardMessage(), true);
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
            _pendingArtifactOpen = false;
            _rewardAcknowledged = false;
            if (phase == GamePhase.Reward) _rewardSequence++;
            else if (_artifactUi != null) _artifactUi.ResetReward();
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

            if (_flow.CurPhase == GamePhase.Reward)
            {
                if (_rewardAcknowledged) return;
                _rewardAcknowledged = true;
                _pendingArtifactOpen = true;
                _acknowledgedFrame = Time.frameCount;
                _ui.HideWaveReward();
                return;
            }
            _ui.HideWaveReward();
            _contentGate.ChooseResultBtn();
        }

        private void HandleArtifactCompleted(string rewardId)
        {
            if (IsReady && _flow.CurPhase == GamePhase.Reward && _rewardAcknowledged &&
                _artifacts != null && _artifacts.IsRewardApplied)
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

        private void ShowRewardConnectionError()
        {
            _rewardAcknowledged = false;
            _ui.ShowProgressPrompt("유물 보상 연결을 확인해주세요.\n계속을 눌러 다시 시도할 수 있습니다.", true);
            Debug.LogError("[UI/RunFlowPresenter] Artifact reward UI could not open.", this);
        }

        private void Bind()
        {
            if (_isSubscribed || !IsReady) return;
            if (_artifactUi != null) _artifactUi.Completed += HandleArtifactCompleted;
            _flow.PhaseChanged += HandlePhaseChanged;
            _ui.ContinueRequested += HandleContinueRequested;
            _ui.RestartRequested += HandleRestartRequested;
            _isSubscribed = true;
            Refresh();
        }

        private void Unbind()
        {
            if (!_isSubscribed) return;
            if (_artifactUi != null) _artifactUi.Completed -= HandleArtifactCompleted;
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
