using System;
using System.Collections.Generic;
using Game.Core;
using OZGL.KDH;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>건물 원본 API를 플레이어 UI에 연결한다. 생성·차감·환불은 원본 컨트롤러만 수행한다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeBuildingUiBinding : MonoBehaviour
    {
        public BuildingSlot SelectedSlot => _slot;

        [SerializeField] private BuildingCatalogPanel _catalog;
        [SerializeField] private BuildingInfoPanel _info;
        [SerializeField] private BuildingActionPanel _actions;
        [SerializeField] private BuildingBuildController _controller;
        [SerializeField] private BuildingDatabase _database;
        [SerializeField] private RunCurrencyManager _wallet;
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private BuildingSlot[] _slots = Array.Empty<BuildingSlot>();

        private readonly List<BuildingData> _candidates = new List<BuildingData>();
        private readonly List<BuildingCatalogItem> _items = new List<BuildingCatalogItem>();
        private readonly Dictionary<string, BuildingData> _byId = new Dictionary<string, BuildingData>();
        private BuildingSlot _slot;
        private Collider2D _slotCollider;
        private Building _displayedBuilding;
        private BuildingData _candidate;
        private BuildingCoreProgress _coreProgress;
        private string _selectionId;
        private bool _bound;
        private bool _executing;
        private bool _clearing;
        private bool _wasAllowed;

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void LateUpdate()
        {
            if (_selectionId == null || _executing) return;
            // 외부에서 대상이 바뀌면 이전 견적의 클릭을 막는다. 매 프레임 검색하거나 할당하지 않는다.
            if ((!ReferenceEquals(_displayedBuilding, null) && _displayedBuilding == null) ||
                _controller == null || _wallet == null || _flow == null || !HasLiveSlot() ||
                _slot.CurrentBuilding != _displayedBuilding)
            {
                ClearSelection();
                return;
            }
            bool allowed = CanOperate();
            if (_wasAllowed != allowed) Refresh();
        }

        public void SelectSlot(BuildingSlot slot)
        {
            if (_executing || !isActiveAndEnabled || !_bound) return;
            ClearSelection();
            if (slot == null || !slot.isActiveAndEnabled || !CanOperate()) return;
            _slot = slot;
            _slotCollider = slot.GetComponent<Collider2D>();
            if (!HasLiveSlot()) { ClearSelection(); return; }
            _selectionId = Guid.NewGuid().ToString("N");
            _displayedBuilding = slot.IsOccupied ? slot.CurrentBuilding : null;
            if (slot.IsOccupied)
            {
                PopulateUpgradeCatalog();
                if (_candidates.Count == 1) _candidate = _candidates[0];
                if (_candidates.Count > 1) _catalog.Show();
                else Refresh();
            }
            else
            {
                PopulateCatalog();
                _catalog.Show();
            }
        }

        public void SelectFirstEmptySlot()
        {
            foreach (var slot in _slots)
                if (slot != null && slot.isActiveAndEnabled && !slot.IsOccupied)
                {
                    var collider = slot.GetComponent<Collider2D>();
                    if (collider == null || !collider.enabled) continue;
                    SelectSlot(slot);
                    return;
                }
            if (_feedbackText != null) _feedbackText.text = "빈 건설 공간을 먼저 확보해 주세요.";
        }

        public void ClearSelection()
        {
            if (_clearing) return;
            _clearing = true;
            _slot = null;
            _slotCollider = null;
            _displayedBuilding = null;
            _candidate = null;
            _selectionId = null;
            _byId.Clear();
            if (_catalog != null) _catalog.Popup.Hide();
            if (_info != null) _info.HideBuildingInfo();
            if (_actions != null) _actions.HideActions();
            _clearing = false;
        }

        public void Refresh()
        {
            if (_executing || !HasLiveSlot() || _selectionId == null) return;
            _wasAllowed = CanOperate();
            _actions.SetActionsAllowed(_wasAllowed, "지금은 건물 조작을 할 수 없습니다.");
            BuildingData data = _slot.IsOccupied ? _slot.CurrentBuilding.Data : _candidate;
            if (data == null) return;
            if (string.IsNullOrWhiteSpace(data.BuildingId) || string.IsNullOrWhiteSpace(data.DisplayName))
            {
                ClearSelection();
                if (_feedbackText != null) _feedbackText.text = "건물 표시 설정을 확인해 주세요.";
                return;
            }
            bool valid = BuildingUiQuote.TryRead(data, _controller.RefundRate, out int gold, out int gems,
                out int refundGold, out int refundGems);
            string reason = !valid ? "건물 비용 설정을 확인해 주세요." : !_wallet.IsInitialized
                ? "재화 연결을 기다리고 있습니다." : "재화가 부족합니다.";
            bool occupied = _slot.IsOccupied;
            bool coreSlot = occupied && _controller.IsCoreSlot(_slot);
            var offer = occupied
                ? new BuildingActionOffer(BuildingUiAction.Dismantle, "해체", refundGold, refundGems,
                    valid && _wallet.IsInitialized, reason)
                : new BuildingActionOffer(BuildingUiAction.Build, data.DisplayName, gold, gems,
                    valid && _wallet.IsInitialized && _controller.CanAffordCandidate(_slot, data), reason, data.BuildingId);
            BuildingActionOffer upgrade = null;
            if (occupied && _candidate != null && IsCurrentUpgrade(_candidate))
            {
                var costs = _controller.GetUpgradeCost(_slot, _candidate);
                bool quoteValid = BuildingUiQuote.TryReadUpgrade(costs, out int upgradeGold, out int upgradeGems);
                bool affordable = quoteValid && _controller.CanAffordUpgrade(_slot, _candidate);
                upgrade = new BuildingActionOffer(BuildingUiAction.Upgrade, _candidate.DisplayName,
                    upgradeGold, upgradeGems, affordable,
                    !quoteValid ? "업그레이드 비용 설정을 확인해 주세요." : "재화가 부족합니다.",
                    _candidate.BuildingId);
            }
            _info.ShowBuildingInfo(new BuildingInfoData(_selectionId, data.DisplayName,
                BuildingUiQuote.Category(data.BuildingType), 1, data.Description,
                BuildingUiQuote.Production(data), icon: data.Icon));
            _actions.ShowActions(new BuildingActionViewData(_selectionId, data.DisplayName,
                build: occupied ? null : offer, upgrade: upgrade,
                dismantle: occupied && !coreSlot ? offer : null));
        }

        private void HandleCandidateSelected(string id)
        {
            if (!CanOperate() || !HasLiveSlot() || !_byId.TryGetValue(id, out var data)) return;
            if (_slot.IsOccupied && !IsCurrentUpgrade(data)) return;
            _candidate = data;
            Refresh();
        }

        private void HandleAction(BuildingActionRequest request)
        {
            if (_executing) return;
            bool succeeded = false;
            string message = "조건이 바뀌었습니다. 건설 장소를 다시 선택해 주세요.";
            _executing = true;
            try
            {
                if (request.TargetId != _selectionId || !CanOperate() || !HasLiveSlot() ||
                    _slot.CurrentBuilding != _displayedBuilding) return;
                BuildingData data = _slot.IsOccupied ? _slot.CurrentBuilding.Data : _candidate;
                if (data == null || !BuildingUiQuote.TryRead(data, _controller.RefundRate,
                    out _, out _, out _, out _)) return;
                if (request.Action == BuildingUiAction.Build && !_slot.IsOccupied &&
                    request.OptionId == data.BuildingId && IsCurrentCandidate(data))
                    succeeded = _controller.TryBuild(_slot, data);
                else if (request.Action == BuildingUiAction.Upgrade && _slot.IsOccupied &&
                    _candidate != null && request.OptionId == _candidate.BuildingId &&
                    IsCurrentUpgrade(_candidate) &&
                    BuildingUiQuote.TryReadUpgrade(_controller.GetUpgradeCost(_slot, _candidate), out _, out _))
                    succeeded = _controller.TryUpgrade(_slot, _candidate);
                else if (request.Action == BuildingUiAction.Dismantle && _slot.IsOccupied &&
                    !_controller.IsCoreSlot(_slot))
                    succeeded = _controller.TryDemolish(_slot);
                message = succeeded ? "완료되었습니다." : "실행하지 못했습니다. 현재 재화와 건물을 확인해 주세요.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                message = "건물 처리 중 오류가 발생했습니다. 재화와 건물 상태를 확인해 주세요.";
            }
            finally
            {
                _executing = false;
                _actions.TryResolveRequest(request.RequestId, succeeded, message);
                // 원본 Try API의 실패도 자동 재시도하지 않는다. 새 선택은 실제 슬롯과 재화를 다시 읽는다.
                if (succeeded && HasLiveSlot())
                {
                    _displayedBuilding = _slot.CurrentBuilding;
                    if (_slot.IsOccupied)
                    {
                        _candidate = null;
                        PopulateUpgradeCatalog();
                        if (_candidates.Count == 1) _candidate = _candidates[0];
                        Refresh();
                    }
                    else ClearSelection();
                }
                else if (!succeeded) ClearSelection();
                if (_feedbackText != null) _feedbackText.text = message;
            }
        }

        private void HandleBalanceChanged(CurrencyData currency, int previous, int current) => Refresh();
        private void HandleInfoClosed(string id) { if (!_executing) ClearSelection(); }
        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Preparation) ClearSelection();
            _actions.SetActionsAllowed(false, "건설 단계 전환을 기다리고 있습니다.");
        }

        private bool CanOperate() => _bound && _controller != null && _controller.isActiveAndEnabled &&
            _wallet != null && _wallet.isActiveAndEnabled && _wallet.IsInitialized &&
            _flow != null && _flow.isActiveAndEnabled && _flow.CanEnterBuildMode();

        private bool HasLiveSlot() => _slot != null && _slot.isActiveAndEnabled &&
            _slotCollider != null && _slotCollider.enabled;

        private bool IsCurrentCandidate(BuildingData data)
        {
            _slot.CollectCandidates(_candidates, _database, GetCurrentCoreLevel(), GetCensus());
            return _candidates.Contains(data);
        }

        private bool IsCurrentUpgrade(BuildingData data)
        {
            if (!_slot.IsOccupied || _slot.CurrentBuilding.Data == null) return false;
            _slot.CurrentBuilding.Data.CollectUpgrades(_candidates, GetCurrentCoreLevel());
            return _candidates.Contains(data);
        }

        private void PopulateUpgradeCatalog()
        {
            _slot.CurrentBuilding.Data.CollectUpgrades(_candidates, GetCurrentCoreLevel());
            _items.Clear(); _byId.Clear();
            foreach (var next in _candidates)
            {
                if (next == null || string.IsNullOrWhiteSpace(next.BuildingId) ||
                    string.IsNullOrWhiteSpace(next.DisplayName) || _byId.ContainsKey(next.BuildingId)) continue;
                _byId.Add(next.BuildingId, next);
                bool valid = BuildingUiQuote.TryReadUpgrade(_controller.GetUpgradeCost(_slot, next),
                    out int gold, out int gems);
                _items.Add(new BuildingCatalogItem(next.BuildingId, next.DisplayName,
                    BuildingUiQuote.Category(next.BuildingType), valid ? gold : (int?)null,
                    valid ? gems : (int?)null));
            }
            _catalog.SetItems(_items);
        }

        private void PopulateCatalog()
        {
            _slot.CollectCandidates(_candidates, _database, GetCurrentCoreLevel(), GetCensus());
            _items.Clear(); _byId.Clear();
            var duplicates = new HashSet<string>();
            foreach (var data in _candidates)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.BuildingId) ||
                    string.IsNullOrWhiteSpace(data.DisplayName) || data.BuildingType == BuildingType.Core) continue;
                if (_byId.ContainsKey(data.BuildingId)) { duplicates.Add(data.BuildingId); continue; }
                _byId.Add(data.BuildingId, data);
            }
            foreach (string id in duplicates) _byId.Remove(id);
            foreach (var pair in _byId)
            {
                var data = pair.Value;
                bool valid = BuildingUiQuote.TryRead(data, _controller.RefundRate,
                    out int gold, out int gems, out _, out _);
                _items.Add(new BuildingCatalogItem(pair.Key, data.DisplayName,
                    BuildingUiQuote.Category(data.BuildingType), valid ? gold : (int?)null, valid ? gems : (int?)null));
            }
            _catalog.SetItems(_items);
        }

        private int GetCurrentCoreLevel()
        {
            if (_coreProgress == null && _controller != null)
                _coreProgress = _controller.GetComponent<BuildingCoreProgress>() ??
                    FindFirstObjectByType<BuildingCoreProgress>();
            return _coreProgress != null ? _coreProgress.CurrentLevel : 0;
        }

        private BuildingCensus GetCensus()
        {
            return _controller != null ? _controller.Census : null;
        }

        private void Bind()
        {
            if (_bound || _catalog == null || _info == null || _actions == null ||
                _controller == null || _wallet == null || _flow == null) return;
            _catalog.ItemSelected += HandleCandidateSelected;
            _actions.ActionRequested += HandleAction;
            _info.InfoPanelClosed += HandleInfoClosed;
            _wallet.BalanceChanged += HandleBalanceChanged;
            _flow.PhaseChanged += HandlePhaseChanged;
            _bound = true;
        }

        private void Unbind()
        {
            if (_bound)
            {
                if (_catalog != null) _catalog.ItemSelected -= HandleCandidateSelected;
                if (_actions != null) _actions.ActionRequested -= HandleAction;
                if (_info != null) _info.InfoPanelClosed -= HandleInfoClosed;
                if (_wallet != null) _wallet.BalanceChanged -= HandleBalanceChanged;
                if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
            }
            _bound = false;
            ClearSelection();
        }
    }
}
