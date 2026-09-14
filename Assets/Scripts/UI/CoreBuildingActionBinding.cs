using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>코어의 건설 가능 상태만 표시한다. 실제 건설/비용 검사와 요청 완료는 건물 담당자가 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CoreBuildingActionBinding : MonoBehaviour
    {
        [SerializeField] private BuildingActionPanel _panel;
        [SerializeField] private GameFlowController _flow;

        private GameFlowController _subscribedFlow;
        private CancellationTokenSource _lifetime;
        private CancellationTokenRegistration _sourceDestruction;
        private int _refreshVersion;
        private bool _sourceLost;

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        public void Initialize(BuildingActionPanel panel, GameFlowController flow)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            Unbind();
            _panel = panel;
            _flow = flow;
            if (isActiveAndEnabled) Bind();
            else Refresh();
        }

        /// <summary>같은 프레임의 전환 잠금을 확인하고, 준비 단계라면 다음 프레임에 한 번 재확인한다.</summary>
        public void Refresh()
        {
            int version = ++_refreshVersion;
            if (_panel == null) return;
            if (!isActiveAndEnabled || _lifetime == null || _flow == null || _sourceLost)
            {
                _panel.SetActionsAllowed(false, "코어 연결을 기다리고 있습니다.");
                return;
            }
            if (_flow.CurPhase == GamePhase.None) _panel.HideActions();
            ApplyCurrentState();
            if (_flow.isActiveAndEnabled && _flow.CurPhase == GamePhase.Preparation && !_flow.CanEnterBuildMode())
                RefreshAfterTransitionAsync(version, _lifetime.Token).Forget();
        }

        private void HandlePhaseChanged(GamePhase phase) => Refresh();

        private void HandleFlowDestroyed()
        {
            _sourceLost = true;
            _refreshVersion++;
            if (_panel == null) return;
            _panel.SetActionsAllowed(false, "코어 연결이 종료되었습니다.");
            _panel.HideActions();
        }

        private void Bind()
        {
            if (_lifetime != null) return;
            if (_panel == null || _flow == null) { Refresh(); return; }
            _sourceLost = false;
            _lifetime = new CancellationTokenSource();
            _subscribedFlow = _flow;
            _subscribedFlow.PhaseChanged += HandlePhaseChanged;
            _sourceDestruction = _flow.destroyCancellationToken.Register(HandleFlowDestroyed);
            Refresh();
        }

        private void Unbind()
        {
            _refreshVersion++;
            if (_subscribedFlow != null) _subscribedFlow.PhaseChanged -= HandlePhaseChanged;
            _subscribedFlow = null;
            _sourceDestruction.Dispose();
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            if (_panel != null) _panel.SetActionsAllowed(false, "코어 연결을 기다리고 있습니다.");
        }

        private async UniTask RefreshAfterTransitionAsync(int version, CancellationToken token)
        {
            try
            {
                // PhaseChanged는 _isTransitioning 해제 전에 발생한다. 매 프레임 폴링하지 않는다.
                await UniTask.NextFrame(cancellationToken: token);
                if (this != null && isActiveAndEnabled && !_sourceLost && _panel != null &&
                    _flow != null && version == _refreshVersion && !token.IsCancellationRequested)
                    ApplyCurrentState();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }

        private void ApplyCurrentState()
        {
            bool active = _flow.isActiveAndEnabled;
            _panel.SetActionsAllowed(active && _flow.CanEnterBuildMode(),
                active ? GetBlockedReason(_flow.CurPhase) : "코어가 비활성화되어 있습니다.");
        }

        private static string GetBlockedReason(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Preparation: return "건설 단계 전환을 기다리고 있습니다.";
                case GamePhase.BattlePreparing: return "전투 준비 중에는 건물 조작을 할 수 없습니다.";
                case GamePhase.Battle: return "전투 중에는 건물 조작을 할 수 없습니다.";
                case GamePhase.BattleResolving: return "전투 정산 중에는 건물 조작을 할 수 없습니다.";
                case GamePhase.Reward: return "보상 처리 중에는 건물 조작을 할 수 없습니다.";
                case GamePhase.QuarterComplete: return "분기 완료 선택 중에는 건물 조작을 할 수 없습니다.";
                case GamePhase.Finished: return "플레이가 종료되어 건물 조작을 할 수 없습니다.";
                default: return "코어 준비를 기다리고 있습니다.";
            }
        }
    }
}
