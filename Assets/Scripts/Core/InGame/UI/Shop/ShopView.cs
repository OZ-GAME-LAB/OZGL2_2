using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>상점 진열과 구매·교환 입력을 연결하고, 나가기까지 기다린다. 거래는 ShopManager가 처리한다.</summary>
    public sealed class ShopView : MonoBehaviour, IShopUI
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private ShopItemCardView[] _artifactCards;
        [SerializeField] private ShopItemCardView[] _consumableCards;
        [SerializeField] private ArtifactPopupView _artifactPopup;
        [SerializeField] private UIItemSlot _ownedSlot;
        [SerializeField] private UIItemSlot _offeredSlot;
        [SerializeField] private Button _exchangeButton;
        [SerializeField] private TMP_Text _exchangeButtonText;
        [SerializeField] private Sprite _defaultArtifactIcon;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private Button _leaveButton;

        public bool IsPending => _pending != null;

        private ICurrencyReader _wallet;
        private ShopManager _shop;
        private UniTaskCompletionSource _pending;
        private ArtifactInventoryPopupView _inventoryPopup;
        private ShopArtifactExchangeSlot _exchangeSlot;
        private ArtifactData _selectedMaterial;
        private bool _isTrading;
        private bool _isSelectingMaterial;
        private bool _refreshRequested;

        public void Initialize(ICurrencyReader wallet)
        {
            if (IsPending) throw new InvalidOperationException("상점을 닫은 뒤 UI를 연결해주세요.");
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public void InitializeExchangeUI(ArtifactInventoryPopupView inventoryPopup)
        {
            if (IsPending) throw new InvalidOperationException("상점을 닫은 뒤 UI를 연결해주세요.");
            if (inventoryPopup == null) throw new ArgumentNullException(nameof(inventoryPopup));
            _inventoryPopup = inventoryPopup;
        }

        public async UniTask OpenAndWaitForCloseAsync(ShopManager shop, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (IsPending) throw new InvalidOperationException("상점이 이미 열려 있습니다.");
            if (shop == null || !shop.IsInitialized || _wallet == null || !isActiveAndEnabled ||
                _screen == null || _screen.Manager == null || _goldText == null ||
                _artifactCards == null || _consumableCards == null ||
                _feedbackText == null || _leaveButton == null || _artifactPopup == null ||
                _inventoryPopup == null || _ownedSlot == null || _offeredSlot == null ||
                _exchangeButton == null || _exchangeButtonText == null)
                throw new InvalidOperationException("상점 UI와 시스템 참조를 먼저 연결해주세요.");

            var completion = new UniTaskCompletionSource();
            _shop = shop;
            _pending = completion;
            bool completed = false;
            try
            {
                //물품 상태바뀌면 화면 다시 표시
                _shop.ShopChanged += RequestRefresh;
                //잔액 변경 시 상품표시 갱신
                _wallet.BalanceChanged += HandleBalanceChanged;
                //외부에서 화면 닫히면 비동기 대기 취소(TrySetCanceled)
                _screen.Closed += HandleScreenClosed;
                //상점 나가기 버튼 입력시 비동기 대기 완료(TrySetResult)
                _leaveButton.onClick.AddListener(HandleLeave);
                _exchangeButton.onClick.AddListener(HandleExchange);
                _leaveButton.interactable = true;
                _feedbackText.text = string.Empty;
                Refresh();
                using (token.Register(() => completion.TrySetCanceled(token)))
                {
                    token.ThrowIfCancellationRequested();
                    if (!_screen.Manager.ReplacePopup(_screen.Id) || !_screen.IsVisible)
                        throw new InvalidOperationException("상점 화면을 열지 못했습니다.");
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(_leaveButton.gameObject);
                    await completion.Task;
                    completed = true;
                }
            }
            finally
            {
                // 토큰은 다른 스레드에서도 취소될 수 있으므로 화면 정리는 Unity 스레드에서 한다.
                await UniTask.SwitchToMainThread();
                if (_shop != null) _shop.ShopChanged -= RequestRefresh;
                _wallet.BalanceChanged -= HandleBalanceChanged;
                if (_isSelectingMaterial && _inventoryPopup != null) _inventoryPopup.CancelSelection();
                _isSelectingMaterial = false;
                if (_exchangeButton != null)
                {
                    _exchangeButton.onClick.RemoveListener(HandleExchange);
                    _exchangeButton.interactable = false;
                }
                if (_ownedSlot != null) _ownedSlot.Unbind();
                if (_offeredSlot != null) _offeredSlot.Unbind();
                if (_leaveButton != null)
                {
                    _leaveButton.onClick.RemoveListener(HandleLeave);
                    _leaveButton.interactable = false;
                }
                foreach (ShopItemCardView card in _artifactCards) if (card != null) card.Unbind();
                foreach (ShopItemCardView card in _consumableCards) if (card != null) card.Unbind();
                if (_screen != null)
                {
                    _screen.Closed -= HandleScreenClosed;
                    if (_screen.Manager != null)
                        _screen.Manager.ClosePopup(_screen.Id,
                            completed ? UICloseReason.Completed : UICloseReason.ContextLost);
                }
                _shop = null;
                _pending = null;
                _exchangeSlot = null;
                _selectedMaterial = null;
                _isTrading = false;
                _refreshRequested = false;
            }
        }

        /// <summary> 상점의 아이템들을 연결하는 메서드 </summary>
        public void Refresh()
        {
            _refreshRequested = false;
            if (_shop == null) return;
            _goldText.text = $"골드 {_wallet.GetBalance(CurrencyType.Gold):N0}";
            for (int i = 0; i < _artifactCards.Length; i++)
            {
                //현재 아티팩트 구매칸보다 ShopManager에서 만든 수량이 적으면 공란으로 표시
                if (i >= _shop.PurchaseSlots.Count)
                {
                    _artifactCards[i].ShowEmpty();
                    continue;
                }
                // 현재 상점의 슬롯을 그대로 전달해야 ShopManager의 거래 검증을 통과한다.
                ShopArtifactSlot slot = _shop.PurchaseSlots[i];
                _artifactCards[i].Bind(
                    slot.Artifact,
                    FormatPrice(slot.Currency, slot.Price), 
                    slot.IsPurchased, 
                    () => PurchaseArtifact(slot), OpenArtifact);
            }
            for (int i = 0; i < _consumableCards.Length; i++)
            {
                //현재 소비아이템 구매칸보다 ShopManager에서 만든 수량이 적으면 공란으로 표시
                if (i >= _shop.ConsumableSlots.Count)
                {
                    _consumableCards[i].ShowEmpty();
                    continue;
                }
                ShopConsumableSlot slot = _shop.ConsumableSlots[i];
                ConsumableItemData item = slot.Item;
                _consumableCards[i].ShowItem(
                    item.Icon, 
                    item.DisplayName, 
                    item.Description,
                    FormatPrice(slot.Currency, slot.Price), 
                    slot.IsPurchased, 
                    () => PurchaseConsumable(slot));
            }
            RefreshExchange();
        }

        private void RefreshExchange()
        {
            ShopArtifactExchangeSlot slot = _shop.ExchangeSlots.Count > 0 ? _shop.ExchangeSlots[0] : null;
            if (_exchangeSlot != slot)
            {
                _exchangeSlot = slot;
                _selectedMaterial = null;
            }

            _exchangeButton.interactable = false;
            _exchangeButtonText.text = slot != null && slot.IsExchanged ? "교환 완료" : "교환";
            if (slot == null)
            {
                _selectedMaterial = null;
                _ownedSlot.Bind(_defaultArtifactIcon, "재료 선택", string.Empty, false, false, null);
                _offeredSlot.Bind(_defaultArtifactIcon, "교환 상품 없음", string.Empty, false, false, null);
                return;
            }

            ArtifactData reward = slot.Artifact;
            _offeredSlot.Bind(reward.Icon != null ? reward.Icon : _defaultArtifactIcon,
                reward.DisplayName, "받을 유물 ×1", false, true,
                () => OpenExchangeReward(reward));

            List<ArtifactInstance> candidates = _shop.GetExchangeCandidates(slot);
            ArtifactInstance material = FindCandidate(candidates, _selectedMaterial);
            if (material == null) _selectedMaterial = null;
            bool canSelect = !slot.IsExchanged && candidates.Count > 0;
            string materialInfo = "같은 등급 유물 1개";
            if (slot.IsExchanged) materialInfo = "교환 완료";
            else if (!canSelect) materialInfo = "교환 가능한 재료 없음";
            else if (material != null) materialInfo = $"보유 {material.StackCount}개 · 1개 소비";
            _ownedSlot.Bind(material != null && material.Data.Icon != null ? material.Data.Icon : _defaultArtifactIcon,
                material != null ? material.Data.DisplayName : "재료 선택",
                materialInfo, false, canSelect, OpenExchangeMaterials);
            _exchangeButton.interactable = material != null && !slot.IsExchanged && !_isTrading;
        }

        private void OpenExchangeMaterials()
        {
            if (!CanInteract() || _exchangeSlot == null || _exchangeSlot.IsExchanged) return;
            List<ArtifactInstance> candidates = _shop.GetExchangeCandidates(_exchangeSlot);
            if (candidates.Count == 0)
            {
                _feedbackText.text = "교환 가능한 재료가 없습니다.";
                Refresh();
                return;
            }

            // 선택창이 닫히는 동안 상점 요청이나 제안이 바뀌었으면 결과를 사용하지 않는다.
            UniTaskCompletionSource request = _pending;
            ShopArtifactExchangeSlot offer = _exchangeSlot;
            _isSelectingMaterial = true;
            bool opened = _inventoryPopup.OpenSelection(candidates, artifact =>
            {
                _isSelectingMaterial = false;
                if (_pending == request && _exchangeSlot == offer)
                    HandleMaterialSelected(artifact);
            }, () =>
            {
                _isSelectingMaterial = false;
                RequestRefresh();
            });
            if (!opened)
            {
                _isSelectingMaterial = false;
                _feedbackText.text = "재료 선택창을 열지 못했습니다.";
            }
        }

        private void HandleMaterialSelected(ArtifactData artifact)
        {
            if (!CanInteract()) return;
            ArtifactInstance material = FindCandidate(_shop.GetExchangeCandidates(_exchangeSlot), artifact);
            _selectedMaterial = material != null ? material.Data : null;
            _feedbackText.text = material != null ? "교환 버튼을 누르면 선택한 유물 1개를 소비합니다." :
                "선택한 재료를 사용할 수 없습니다. 다시 선택해주세요.";
            Refresh();
        }

        private void HandleExchange()
        {
            if (!CanInteract() || _exchangeSlot == null || _exchangeSlot.IsExchanged) return;
            if (FindCandidate(_shop.GetExchangeCandidates(_exchangeSlot), _selectedMaterial) == null)
            {
                _selectedMaterial = null;
                _feedbackText.text = "교환할 재료를 다시 선택해주세요.";
                Refresh();
                return;
            }

            _isTrading = true;
            try
            {
                bool succeeded = _shop.TryExchange(_exchangeSlot, _selectedMaterial);
                if (succeeded) _selectedMaterial = null;
                _feedbackText.text = succeeded ? "교환했습니다." : "교환하지 못했습니다. 보유 상태를 확인해주세요.";
            }
            finally { _isTrading = false; Refresh(); }
        }

        private static ArtifactInstance FindCandidate(IReadOnlyList<ArtifactInstance> candidates, ArtifactData artifact)
        {
            if (artifact == null) return null;
            foreach (ArtifactInstance candidate in candidates)
                if (candidate.Data == artifact) return candidate;
            return null;
        }

        private void OpenExchangeReward(ArtifactData artifact)
        {
            RectTransform rect = (RectTransform)_offeredSlot.transform;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            OpenArtifact(artifact, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)));
        }

        private void PurchaseArtifact(ShopArtifactSlot slot)
        {
            if (!CanPurchase(slot.Currency, slot.Price)) return;
            _isTrading = true;
            try { ShowPurchaseResult(_shop.TryPurchase(slot)); }
            finally { _isTrading = false; Refresh(); }
        }

        private void PurchaseConsumable(ShopConsumableSlot slot)
        {
            if (!CanPurchase(slot.Currency, slot.Price)) return;
            _isTrading = true;
            try { ShowPurchaseResult(_shop.TryPurchaseConsumable(slot)); }
            finally { _isTrading = false; Refresh(); }
        }

        private void OpenArtifact(ArtifactData artifact, Vector2 screenPosition)
        {
            if (!CanInteract() || artifact == null || _artifactPopup == null) return;
            if (!_artifactPopup.Open(artifact, screenPosition))
                _feedbackText.text = "유물 정보를 열지 못했습니다.";
        }
        /// <summary> 현재 상태가 구매할 수 있는 상태인지, 구매할 재화가 있는지 확인</summary>
        private bool CanPurchase(CurrencyType currency, int price)
        {
            if (!CanInteract()) return false;
            if (_wallet.GetBalance(currency) >= price) return true;
            _feedbackText.text = "구매할 재화가 부족합니다.";
            return false;
        }

        private void ShowPurchaseResult(bool succeeded)
        {
            _feedbackText.text = succeeded ? "구매했습니다." :
                "구매하지 못했습니다. 보유 상태나 소모품 공간을 확인해주세요.";
        }

        private static string FormatPrice(CurrencyType currency, int price) =>
            $"{price:N0} {(currency == CurrencyType.Gold ? "" : "보석")}";

        private bool CanInteract() => isActiveAndEnabled && IsPending &&
            _pending.Task.Status == UniTaskStatus.Pending && !_isTrading &&
            _shop != null && _shop.IsInitialized && _screen != null && _screen.IsVisible &&
            _screen.Manager != null && _screen.Manager.TopPopup == _screen;

        // 거래 알림은 매니저의 거래 잠금 중 발생하므로, 후보 조회는 잠금이 풀린 뒤 한다.
        private void RequestRefresh() => _refreshRequested = true;
        private void HandleBalanceChanged(CurrencyData currency, int previous, int current) => RequestRefresh();
        private void LateUpdate() { if (_refreshRequested) Refresh(); }
        /// <summary> 특정한 이유로 창이 닫혔을 때 예외전달하는 코드 </summary>
        private void HandleScreenClosed(UIScreen screen, UICloseReason reason) => _pending?.TrySetCanceled();

        private void HandleLeave()
        {
            if (!CanInteract() || !_leaveButton.IsInteractable()) return;
            _leaveButton.interactable = false;
            _pending.TrySetResult();
        }

        private void OnDisable() => _pending?.TrySetCanceled();
        private void OnDestroy() => _pending?.TrySetCanceled();
    }
}
