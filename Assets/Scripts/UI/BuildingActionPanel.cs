using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>건물 행동 요청/표시 전용. 한 요청 처리 주체만 연결하고 Unity 메인 스레드에서 응답한다.</summary>
    public sealed class BuildingActionPanel : MonoBehaviour
    {
        public event Action<BuildingActionRequest> ActionRequested
        {
            add { _actionRequested += value; Refresh(); }
            remove { _actionRequested -= value; Refresh(); }
        }

        public string TargetId => _data?.TargetId;
        public bool IsRequestPending => _pending != null;
        public bool IsAwaitingRefresh => _awaitingRefresh;

        [Serializable]
        private sealed class ActionRow
        {
            [SerializeField] private Button _button;
            [SerializeField] private TMP_Text _quote;
            [SerializeField] private TMP_Text _reason;

            public Button Button => _button;

            public void Render(BuildingActionOffer offer, string blockReason)
            {
                _button.interactable = offer != null && offer.CanExecute && blockReason == null;
                _quote.text = offer == null ? "해당 없음" :
                    $"{offer.DisplayName} · {(offer.Action == BuildingUiAction.Dismantle ? "환급" : "비용")} {offer.GoldAmount:N0} 골드";
                _reason.text = offer == null ? "현재 선택에서는 사용할 수 없습니다." :
                    blockReason ?? (offer.CanExecute ? "실행 가능" : offer.DisabledReason);
            }
        }

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private ActionRow _build = new ActionRow();
        [SerializeField] private ActionRow _upgrade = new ActionRow();
        [SerializeField] private ActionRow _dismantle = new ActionRow();

        private Action<BuildingActionRequest> _actionRequested;
        private BuildingActionViewData _data;
        private BuildingActionRequest _pending;
        private ulong _selectionVersion;
        private ulong _pendingSelectionVersion;
        private bool _awaitingRefresh;
        private bool _actionsAllowed;
        private string _phaseReason = "건설 단계 상태를 기다리는 중입니다.";
        private string _resultMessage;

        private void OnEnable()
        {
            _build.Button.onClick.RemoveListener(HandleBuildClicked);
            _upgrade.Button.onClick.RemoveListener(HandleUpgradeClicked);
            _dismantle.Button.onClick.RemoveListener(HandleDismantleClicked);
            _build.Button.onClick.AddListener(HandleBuildClicked);
            _upgrade.Button.onClick.AddListener(HandleUpgradeClicked);
            _dismantle.Button.onClick.AddListener(HandleDismantleClicked);
            Refresh();
        }

        private void OnDisable()
        {
            _build.Button.onClick.RemoveListener(HandleBuildClicked);
            _upgrade.Button.onClick.RemoveListener(HandleUpgradeClicked);
            _dismantle.Button.onClick.RemoveListener(HandleDismantleClicked);
            // 비활성화/선택 해제는 실제 작업 취소가 아니다. 대기 요청은 응답까지 유지한다.
        }

        public void ShowActions(BuildingActionViewData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (TargetId != data.TargetId) _selectionVersion++;
            _data = data;
            _awaitingRefresh = false;
            _resultMessage = null;
            Refresh();
        }

        public void HideActions()
        {
            _selectionVersion++;
            _data = null;
            _awaitingRefresh = false;
            _resultMessage = null;
            Refresh();
        }

        public void SetActionsAllowed(bool allowed, string reason = null)
        {
            _actionsAllowed = allowed;
            _phaseReason = string.IsNullOrWhiteSpace(reason) ? "전투 중에는 건물 조작을 할 수 없습니다." : reason;
            Refresh();
        }

        /// <summary>요청 결과 접수. 성공 후 ShowActions로 최신 견적을 전달해야 다시 실행할 수 있다.</summary>
        public bool TryResolveRequest(Guid requestId, bool succeeded, string message = null)
        {
            if (_pending == null || _pending.RequestId != requestId) return false;
            var sameSelection = _data != null && _selectionVersion == _pendingSelectionVersion;
            _pending = null;
            if (sameSelection)
            {
                _awaitingRefresh = succeeded;
                _resultMessage = string.IsNullOrWhiteSpace(message)
                    ? (succeeded ? "완료되었습니다. 최신 건물 정보를 기다립니다." : "실행하지 못했습니다. 다시 확인해 주세요.")
                    : message;
            }
            Refresh();
            return true;
        }

        private void HandleBuildClicked() => RequestAction(_data?.Build);
        private void HandleUpgradeClicked() => RequestAction(_data?.Upgrade);
        private void HandleDismantleClicked() => RequestAction(_data?.Dismantle);

        private void RequestAction(BuildingActionOffer offer)
        {
            if (!isActiveAndEnabled || _data == null || offer == null || !offer.CanExecute ||
                !_actionsAllowed || _pending != null || _awaitingRefresh || _actionRequested == null) return;
            var request = new BuildingActionRequest(_data.TargetId, offer);
            _pending = request;
            _pendingSelectionVersion = _selectionVersion;
            _resultMessage = null;
            Refresh();
            // 호출 전 잠금: 동기 응답/중첩 클릭에서도 중복 요청을 보내지 않는다.
            _actionRequested.Invoke(request);
        }

        private void Refresh()
        {
            // Editor 제작 중 직렬화 참조를 연결하기 전에는 표시하지 않는다.
            if (_title == null || _build?.Button == null || _upgrade?.Button == null || _dismantle?.Button == null) return;
            _title.text = _data?.DisplayName ?? "건물 또는 건설 공간을 선택하세요";
            var blocked = _pending != null ? "요청 처리 중… 중복 실행을 막고 있습니다." :
                !_actionsAllowed ? _phaseReason : _actionRequested == null ? "담당 시스템이 연결되지 않았습니다." :
                _awaitingRefresh ? "최신 건물 정보를 기다립니다." : null;
            _build.Render(_data?.Build, blocked);
            _upgrade.Render(_data?.Upgrade, blocked);
            _dismantle.Render(_data?.Dismantle, blocked);
            _status.text = _resultMessage ?? blocked ?? (_data == null ? "선택 대기" : "표시된 비용과 조건을 확인한 뒤 선택하세요.");
        }
    }
}
