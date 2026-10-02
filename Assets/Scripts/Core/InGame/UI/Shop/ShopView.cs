using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>상점 진열과 구매 입력을 연결하고, 나가기까지 기다린다. 거래는 ShopManager가 처리한다.</summary>
    public sealed class ShopView : MonoBehaviour, IShopUI
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private ShopItemCardView[] _artifactCards;
        [SerializeField] private ShopItemCardView[] _consumableCards;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private UnityEngine.UI.Button _leaveButton;

        public bool IsPending => _pending != null;

        private ICurrencyReader _wallet;
        private ShopManager _shop;
        private UniTaskCompletionSource _pending;
        private bool _isBuying;

        public void Initialize(ICurrencyReader wallet)
        {
            if (IsPending) throw new InvalidOperationException("상점을 닫은 뒤 UI를 연결해주세요.");
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public async UniTask OpenAndWaitForCloseAsync(ShopManager shop, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (IsPending) throw new InvalidOperationException("상점이 이미 열려 있습니다.");
            if (shop == null || !shop.IsInitialized || _wallet == null || !isActiveAndEnabled ||
                _screen == null || _screen.Manager == null || _goldText == null ||
                _artifactCards == null || _consumableCards == null ||
                _feedbackText == null || _leaveButton == null)
                throw new InvalidOperationException("상점 UI와 시스템 참조를 먼저 연결해주세요.");

            var completion = new UniTaskCompletionSource();
            _shop = shop;
            _pending = completion;
            bool completed = false;
            try
            {
                //물품 상태바뀌면 화면 다시 표시
                _shop.ShopChanged += Refresh;
                //잔액 변경 시 상품표시 갱신
                _wallet.BalanceChanged += HandleBalanceChanged;
                //외부에서 화면 닫히면 비동기 대기 취소(TrySetCanceled)
                _screen.Closed += HandleScreenClosed;
                //상점 나가기 버튼 입력시 비동기 대기 완료(TrySetResult)
                _leaveButton.onClick.AddListener(HandleLeave);
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
                if (_shop != null) _shop.ShopChanged -= Refresh;
                _wallet.BalanceChanged -= HandleBalanceChanged;
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
                    _screen.Manager?.ClosePopup(_screen.Id,
                        completed ? UICloseReason.Completed : UICloseReason.ContextLost);
                }
                _shop = null;
                _pending = null;
                _isBuying = false;
            }
        }

        /// <summary> 상점의 아이템들을 연결하는 메서드 </summary>
        public void Refresh()
        {
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
                ArtifactData item = slot.Artifact;
                _artifactCards[i].ShowItem(
                    item.Icon, 
                    item.DisplayName, 
                    item.Description,
                    FormatPrice(slot.Currency, slot.Price), 
                    slot.IsPurchased, 
                    () => PurchaseArtifact(slot));
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
        }

        private void PurchaseArtifact(ShopArtifactSlot slot)
        {
            if (!CanPurchase(slot.Currency, slot.Price)) return;
            _isBuying = true;
            try { ShowPurchaseResult(_shop.TryPurchase(slot)); }
            finally { _isBuying = false; Refresh(); }
        }

        private void PurchaseConsumable(ShopConsumableSlot slot)
        {
            if (!CanPurchase(slot.Currency, slot.Price)) return;
            _isBuying = true;
            try { ShowPurchaseResult(_shop.TryPurchaseConsumable(slot)); }
            finally { _isBuying = false; Refresh(); }
        }
        /// <summary> 현재 상태가 구매할 수 있는 상태인지, 구매할 재화가 있는지 확인</summary>
        private bool CanPurchase(CurrencyType currency, int price)
        {
            if (!IsPending || _pending.Task.Status != UniTaskStatus.Pending || _isBuying ||
                _shop == null || !_shop.IsInitialized || !_screen.IsVisible ||
                _screen.Manager.TopPopup != _screen) return false;
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
            $"{price:N0} {(currency == CurrencyType.Gold ? "골드" : "보석")}";

        private void HandleBalanceChanged(CurrencyData currency, int previous, int current) => Refresh();
        /// <summary> 특정한 이유로 창이 닫혔을 때 예외전달하는 코드 </summary>
        private void HandleScreenClosed(UIScreen screen, UICloseReason reason) => _pending?.TrySetCanceled();

        private void HandleLeave()
        {
            if (!IsPending || _isBuying || !_screen.IsVisible || !_leaveButton.IsInteractable()) return;
            _leaveButton.interactable = false;
            _pending.TrySetResult();
        }

        private void OnDisable() => _pending?.TrySetCanceled();
        private void OnDestroy() => _pending?.TrySetCanceled();
    }
}
