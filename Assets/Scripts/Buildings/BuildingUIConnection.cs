using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI;
using Game.UI.InGame;
using Units;
using Units.UnitDatas;
using UnityEngine;

namespace OZGL.KDH
{
    /// <summary>건설 시스템의 조회/실행과 화면 요청을 연결한다. 게임 선택 문맥은 이곳에서 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingUIConnection : MonoBehaviour
    {
        public BuildingSlot SelectedSlot => _target?.Slot;
        public bool IsReady => _bound;

        private BuildingBuildController _controller;
        private RunCurrencyManager _wallet;
        private GameFlowController _flow;
        private BuildingUIPresenter _presenter;
        private ISpawnManager _spawnManager;
        private BuildingSlot[] _slots = Array.Empty<BuildingSlot>();
        private readonly Dictionary<string, BuildingInteractionCandidate> _byId =
            new Dictionary<string, BuildingInteractionCandidate>();
        private readonly List<BuildingCatalogItem> _items = new List<BuildingCatalogItem>();
        private readonly Stack<BuildingData> _previewPath = new Stack<BuildingData>();
        private readonly HashSet<BuildingSlot> _observedSlots = new HashSet<BuildingSlot>();
        private BuildingInteractionTarget _target;
        private BuildingData _candidate;
        private string _selectionId;
        private bool _bound;
        private bool _executing;
        private bool _wasAllowed;

        public void Initialize(BuildingBuildController controller, RunCurrencyManager wallet,
            GameFlowController flow, BuildingCoreProgress coreProgress, BuildingSlot[] slots,
            BuildingUIPresenter presenter, ISpawnManager spawnManager = null)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (coreProgress == null) throw new ArgumentNullException(nameof(coreProgress));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            Unbind();
            _controller = controller; _wallet = wallet; _flow = flow; _presenter = presenter;
            _spawnManager = spawnManager;
            _slots = slots;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void LateUpdate()
        {
            if (!_bound || _executing) return;
            // 활성화/Collider 변경은 별도 이벤트가 없으므로 이 유효성 확인은 유지한다.
            if (_selectionId == null) return;
            if (_presenter == null || !_presenter.isActiveAndEnabled || _controller == null ||
                !_controller.IsInteractionTargetCurrent(_target)) { ClearSelection(); return; }
            if (_wasAllowed != _controller.CanInteract) Refresh();
        }

        public void SelectSlot(BuildingSlot slot)
        {
            if (!_bound || _executing || !_presenter.isActiveAndEnabled) return;
            ClearSelection();
            BuildingInteractionState state = _controller.QueryInteraction(slot);
            if (!state.CanInteract) return;
            _target = state.Target;
            _selectionId = Guid.NewGuid().ToString("N");
            if (slot.IsOccupied && state.Candidates.Count == 1) _candidate = state.Candidates[0].Data;
            RenderSelection(state);
        }

        public void SelectFirstEmptySlot()
        {
            foreach (BuildingSlot slot in _slots)
                if (BuildingBuildController.IsLiveInteractionSlot(slot) && !slot.IsOccupied)
                { SelectSlot(slot); return; }
            _presenter.ShowFeedback("빈 건설 공간을 먼저 확보해 주세요.");
        }

        public void ClearSelection()
        {
            _target = null; _candidate = null; _selectionId = null;
            _byId.Clear();
            _previewPath.Clear();
            if (_presenter != null) _presenter.Hide();
        }

        public void Refresh()
        {
            if (!_bound || _executing || _selectionId == null) return;
            if (!_controller.IsInteractionTargetCurrent(_target)) { ClearSelection(); return; }
            RenderSelection(_controller.QueryInteraction(_target.Slot, _candidate));
        }

        private void RenderSelection(BuildingInteractionState state)
        {
            _wasAllowed = state.CanInteract;
            _presenter.SetWallet(_wallet.GetBalance(CurrencyType.Gold), _wallet.GetBalance(CurrencyType.Gem));
            _presenter.SetActionsAllowed(state.CanInteract, Reason(state.Failure));
            _byId.Clear(); _items.Clear();
            foreach (BuildingInteractionCandidate candidate in state.DisplayCandidates)
            {
                BuildingData data = candidate.Data;
                BuildingActionOffer offer = Offer(candidate.Offer);
                _byId.Add(data.BuildingId, candidate);
                _items.Add(new BuildingCatalogItem(data.BuildingId, data.DisplayName,
                    BuildingUiQuote.Category(data.BuildingType), candidate.HasValidCost ? candidate.Cost.Gold : (int?)null,
                    candidate.HasValidCost ? candidate.Cost.Gems : (int?)null, data.BuildingType, data.Icon, offer,
                    candidate.IsCoreLevelMet ? (int?)null : candidate.RequiredCoreLevel));
            }
            if (!_target.Slot.IsOccupied)
            {
                _presenter.ShowCatalog(_selectionId, _items);
                return;
            }
            BuildingInteractionCandidate selectedCandidate = null;
            if (_candidate != null && _byId.TryGetValue(_candidate.BuildingId, out selectedCandidate) &&
                selectedCandidate.IsCoreLevelMet)
            {
                _candidate = selectedCandidate.Data;
            }
            else
            {
                _candidate = null;
                selectedCandidate = null;
            }
            BuildingInteractionOffer upgrade = selectedCandidate?.Offer;
            BuildingData displayed = state.DisplayData;
            if (displayed == null) return;
            if (string.IsNullOrWhiteSpace(displayed.BuildingId) || string.IsNullOrWhiteSpace(displayed.DisplayName))
            { ClearSelection(); _presenter.ShowFeedback(Message(BuildingInteractionFailure.InvalidData)); return; }
            BuildingInfoData comparison = null;
            if (_candidate != null)
            {
                string costSummary = upgrade.Failure == BuildingInteractionFailure.InvalidCost
                    ? "업그레이드 비용 설정 오류"
                    : "업그레이드 비용: " + BuildingCurrencyText.Format(upgrade.Cost.Gold, upgrade.Cost.Gems);
                comparison = CreateInfo(_candidate, costSummary);
            }
            _presenter.ShowManagement(CreateInfo(displayed),
                new BuildingActionViewData(_selectionId, displayed.DisplayName,
                    Offer(state.Build), Offer(upgrade), Offer(state.Demolish)),
                _items, comparison, state.CanInteract, Reason(state.Failure), state.RequiredUpgradeCoreLevel);
        }

        private BuildingInfoData CreateInfo(BuildingData data, string costSummary = null)
        {
            UnitData unit = null;
            string spawnUnitName = null;
            int? spawnCount = null;
            string production = BuildingUiQuote.Production(data);
            if (data.HasSpawn)
            {
                if (_spawnManager != null && _spawnManager.TryGetAllyPrefab(data.Spawn.unitType, out GameObject prefab) &&
                    prefab != null && prefab.TryGetComponent<Unit_RuntimeStatus>(out var runtimeStatus))
                    unit = runtimeStatus.UnitData;
                spawnUnitName = unit != null && !string.IsNullOrWhiteSpace(unit.UnitName)
                    ? unit.UnitName : "알 수 없는 유닛";
                spawnCount = Mathf.Min(data.Spawn.countPerWave, data.Spawn.maxAlive);
                if (unit == null) production += "\n유닛 정보 없음";
            }
            if (costSummary == null)
            {
                costSummary = BuildingBuildController.TryReadInteractionCosts(data.BuildCost, out BuildingInteractionCost cost)
                    ? "건설 비용: " + BuildingCurrencyText.Format(cost.Gold, cost.Gems) : "건설 비용 설정 오류";
            }
            return new BuildingInfoData(_selectionId, data.DisplayName,
                BuildingUiQuote.Category(data.BuildingType), 1, data.Description, production,
                BuildingUiQuote.Effects(data), data.Icon, unit, costSummary, data.BuildingId,
                spawnUnitName, spawnCount);
        }

        private static BuildingActionOffer Offer(BuildingInteractionOffer offer)
        {
            if (offer == null) return null;
            BuildingUiAction action = offer.Action == BuildingInteractionAction.Build ? BuildingUiAction.Build :
                offer.Action == BuildingInteractionAction.Upgrade ? BuildingUiAction.Upgrade : BuildingUiAction.Dismantle;
            return new BuildingActionOffer(action, action == BuildingUiAction.Dismantle ? "철거" : offer.Data.DisplayName,
                offer.Cost.Gold, offer.Cost.Gems, offer.CanExecute,
                Reason(offer.Failure, offer.RequiredCoreLevel), offer.Data.BuildingId);
        }

        private void HandleCandidateSelected(string id)
        {
            if (_executing || _target == null || !_target.Slot.IsOccupied ||
                !_controller.IsInteractionTargetCurrent(_target) || !_controller.CanInteract) return;
            BuildingInteractionState state = _controller.QueryInteraction(_target.Slot);
            foreach (BuildingInteractionCandidate candidate in state.Candidates)
                if (candidate.Data.BuildingId == id)
                {
                    _candidate = candidate.Data;
                    RenderSelection(state);
                    return;
                }
        }

        private void HandleInfoRequested(string id)
        {
            if (_executing || !_controller.IsInteractionTargetCurrent(_target)) return;
            BuildingData data = null;
            if (_previewPath.Count > 0)
            {
                // 정보 탐색은 실제 슬롯의 업그레이드 후보 선택과 별도로 유지한다.
                foreach (BuildingUpgradeOption upgrade in _previewPath.Peek().Upgrades ?? Array.Empty<BuildingUpgradeOption>())
                    if (upgrade != null && upgrade.nextBuilding != null && upgrade.nextBuilding.BuildingId == id)
                    {
                        data = upgrade.nextBuilding;
                        break;
                    }
            }
            else if (_target.ExpectedData != null && _target.ExpectedData.BuildingId == id)
                data = _target.ExpectedData;
            else if (_byId.TryGetValue(id, out BuildingInteractionCandidate candidate))
                data = candidate.Data;

            if (data == null) return;
            _previewPath.Push(data);
            RenderPreview();
        }

        private void HandlePreviewBackRequested()
        {
            if (_previewPath.Count < 2 || !_controller.IsInteractionTargetCurrent(_target)) return;
            _previewPath.Pop();
            RenderPreview();
        }

        private void HandlePreviewClosed() => _previewPath.Clear();

        private void RenderPreview()
        {
            BuildingData data = _previewPath.Peek();
            var upgrades = new List<BuildingCatalogItem>();
            foreach (BuildingUpgradeOption upgrade in data.Upgrades ?? Array.Empty<BuildingUpgradeOption>())
            {
                if (upgrade == null || upgrade.nextBuilding == null || upgrade.nextBuilding == data) continue;
                BuildingData next = upgrade.nextBuilding;
                bool validCost = BuildingBuildController.TryReadInteractionCosts(upgrade.cost, out BuildingInteractionCost cost);
                string requirement = upgrade.requiredCoreLevel > 0 ? $"코어 {upgrade.requiredCoreLevel}단계 필요" : "코어 제한 없음";
                upgrades.Add(new BuildingCatalogItem(next.BuildingId, next.DisplayName, requirement,
                    validCost ? cost.Gold : (int?)null, validCost ? cost.Gems : (int?)null, next.BuildingType, next.Icon));
            }
            string costSummary = null;
            if (_previewPath.Count > 1)
            {
                BuildingData parent = _previewPath.ToArray()[1];
                foreach (BuildingUpgradeOption upgrade in parent.Upgrades)
                    if (upgrade != null && upgrade.nextBuilding == data)
                    {
                        costSummary = BuildingBuildController.TryReadInteractionCosts(upgrade.cost, out BuildingInteractionCost cost)
                            ? "업그레이드 비용: " + BuildingCurrencyText.Format(cost.Gold, cost.Gems) : "업그레이드 비용 설정 오류";
                        break;
                    }
            }
            else if (_target.Slot.IsOccupied &&
                _byId.TryGetValue(data.BuildingId, out BuildingInteractionCandidate candidate))
            {
                costSummary = candidate.HasValidCost
                    ? "업그레이드 비용: " + BuildingCurrencyText.Format(candidate.Cost.Gold, candidate.Cost.Gems)
                    : "업그레이드 비용 설정 오류";
            }
            _presenter.ShowBuildingPreview(CreateInfo(data, costSummary), upgrades, _previewPath.Count > 1);
        }

        private void HandleAction(BuildingActionRequest request)
        {
            if (_executing) return;
            _executing = true;
            var result = new BuildingInteractionResult(BuildingInteractionFailure.TargetChanged);
            BuildingSlot slot = SelectedSlot;
            string selectionId = _selectionId;
            try
            {
                if (request.TargetId != _selectionId || _target == null || _controller == null) return;
                BuildingData option = null;
                BuildingInteractionAction action;
                switch (request.Action)
                {
                    case BuildingUiAction.Build:
                        if (!_byId.TryGetValue(request.OptionId, out BuildingInteractionCandidate buildCandidate)) return;
                        option = buildCandidate.Data;
                        action = BuildingInteractionAction.Build;
                        break;
                    case BuildingUiAction.Upgrade:
                        if (_candidate == null || request.OptionId != _candidate.BuildingId) return;
                        option = _candidate;
                        action = BuildingInteractionAction.Upgrade;
                        break;
                    case BuildingUiAction.Dismantle: action = BuildingInteractionAction.Demolish; break;
                    default: return;
                }
                result = _controller.TryExecuteInteraction(_target, action, option);
            }
            finally
            {
                _executing = false;
                string message = Message(result.Failure, result.RequiredCoreLevel);
                _presenter.ResolveRequest(request.RequestId, result.Succeeded, message);
                if (selectionId != null && _selectionId == selectionId && request.TargetId == selectionId)
                {
                    if (result.Succeeded && _presenter.isActiveAndEnabled &&
                        BuildingBuildController.IsLiveInteractionSlot(slot) && slot.IsOccupied)
                    {
                        BuildingInteractionState state = _controller.QueryInteraction(slot);
                        _target = state.Target;
                        // 다음 단계는 다시 선택하게 해서 빠른 두 번째 클릭이 연속 업그레이드가 되지 않게 한다.
                        _candidate = null;
                        _previewPath.Clear();
                        RenderSelection(state);
                    }
                    else if (!result.MayHaveChangedState && _controller.IsInteractionTargetCurrent(_target))
                        Refresh();
                    else
                        ClearSelection();
                }
                // 기존 Try API의 실패는 부분 차감을 배제하지 않는다. 실패를 자동 재시도하지 않는다.
                _presenter.ShowFeedback(message);
            }
        }

        private void HandleClosed()
        {
            if (!_executing || _presenter == null || !_presenter.isActiveAndEnabled) ClearSelection();
        }
        private void HandleBalanceChanged(CurrencyData currency, int previous, int current) => Refresh();
        private void HandleCapacityChanged(int count, int limit) => Refresh();
        private void HandleOccupationChanged(BuildingSlot slot)
        {
            if (_executing) return;
            Refresh();
        }
        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Preparation) ClearSelection();
            else Refresh();
        }

        private void Bind()
        {
            if (_bound || _controller == null || _wallet == null || _flow == null || _presenter == null || !_presenter.IsReady) return;
            _presenter.CandidateSelected += HandleCandidateSelected;
            _presenter.InfoRequested += HandleInfoRequested;
            _presenter.PreviewBackRequested += HandlePreviewBackRequested;
            _presenter.PreviewClosed += HandlePreviewClosed;
            _presenter.ActionRequested += HandleAction;
            _presenter.Closed += HandleClosed;
            _controller.SlotSelected += SelectSlot;
            // Controller는 CoreProgress.Changed/Census.Changed마다 값이 같아도 이 이벤트를 발행한다.
            _controller.BuildCapacityChanged += HandleCapacityChanged;
            _wallet.BalanceChanged += HandleBalanceChanged;
            _flow.PhaseChanged += HandlePhaseChanged;
            foreach (BuildingSlot slot in _slots)
                if (slot != null && _observedSlots.Add(slot)) slot.OccupationChanged += HandleOccupationChanged;
            _bound = true;
        }

        private void Unbind()
        {
            if (_bound)
            {
                if (_presenter != null)
                {
                    _presenter.CandidateSelected -= HandleCandidateSelected;
                    _presenter.InfoRequested -= HandleInfoRequested;
                    _presenter.PreviewBackRequested -= HandlePreviewBackRequested;
                    _presenter.PreviewClosed -= HandlePreviewClosed;
                    _presenter.ActionRequested -= HandleAction;
                    _presenter.Closed -= HandleClosed;
                }
                if (_controller != null)
                {
                    _controller.SlotSelected -= SelectSlot;
                    _controller.BuildCapacityChanged -= HandleCapacityChanged;
                }
                if (_wallet != null) _wallet.BalanceChanged -= HandleBalanceChanged;
                if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
                foreach (BuildingSlot slot in _observedSlots)
                    if (slot != null) slot.OccupationChanged -= HandleOccupationChanged;
            }
            _observedSlots.Clear();
            _bound = false;
            ClearSelection();
        }

        private static string Reason(BuildingInteractionFailure failure, int? requiredCoreLevel = null) =>
            failure == BuildingInteractionFailure.None ? null : Message(failure, requiredCoreLevel);

        private static string Message(BuildingInteractionFailure failure, int? requiredCoreLevel = null)
        {
            switch (failure)
            {
                case BuildingInteractionFailure.None: return "완료되었습니다.";
                case BuildingInteractionFailure.NotReady: return "재화와 건설 시스템 연결을 기다리고 있습니다.";
                case BuildingInteractionFailure.PhaseBlocked: return "지금은 건물 조작을 할 수 없습니다.";
                case BuildingInteractionFailure.InvalidData: return "건물 표시 설정을 확인해 주세요.";
                case BuildingInteractionFailure.InvalidCost: return "건물 비용 설정을 확인해 주세요.";
                case BuildingInteractionFailure.CoreProtected: return "코어는 철거할 수 없습니다.";
                case BuildingInteractionFailure.BuildCapacity: return "건설 한도에 도달했습니다. 코어를 업그레이드하세요.";
                case BuildingInteractionFailure.InsufficientFunds: return "재화가 부족합니다.";
                case BuildingInteractionFailure.CoreLevelRequired: return requiredCoreLevel.HasValue
                    ? $"사령부 {requiredCoreLevel.Value}레벨 필요" : "사령부 레벨이 부족합니다.";
                case BuildingInteractionFailure.Busy: return "건물 처리 중입니다.";
                case BuildingInteractionFailure.ExecutionFailed: return "실행하지 못했습니다. 현재 재화와 건물을 확인해 주세요.";
                case BuildingInteractionFailure.UnexpectedError: return "건물 처리 중 오류가 발생했습니다. 재화와 건물 상태를 확인해 주세요.";
                default: return "조건이 바뀌었습니다. 건설 장소를 다시 선택해 주세요.";
            }
        }
    }
}
