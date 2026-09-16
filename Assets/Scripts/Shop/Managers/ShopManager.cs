using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 상점 품목을 보관하고 재화·아티팩트 매니저에 거래 요청
public class ShopManager : MonoBehaviour
{
    public bool IsInitialized => _selector != null && _artifactManager != null &&
        _artifactManager.IsInitialized && _runCurrencyManager != null && _runCurrencyManager.IsInitialized;
    public bool HasStock { get; private set; }
    public IReadOnlyList<ShopArtifactSlot> PurchaseSlots => _purchaseSlots;
    public IReadOnlyList<ShopArtifactExchangeSlot> ExchangeSlots => _exchangeSlots;
    public event Action ShopChanged;

    [SerializeField] private ShopTable _shopTable;

    private ArtifactManager _artifactManager;
    private RunCurrencyManager _runCurrencyManager;
    private ShopArtifactSelector _selector;
    private List<ShopArtifactSlot> _purchaseSlots = new List<ShopArtifactSlot>();
    private List<ShopArtifactExchangeSlot> _exchangeSlots = new List<ShopArtifactExchangeSlot>();
    private bool _isTrading;

    // 두 매니저 초기화 후 호출. 품목 생성은 TryGenerateStock에서 별도로 처리
    public void Initialize(ArtifactManager artifactManager, RunCurrencyManager runCurrencyManager)
    {
        if (_isTrading || IsInitialized)
        {
            return;
        }

        if (_shopTable == null || artifactManager == null || !artifactManager.IsInitialized ||
            artifactManager.ArtifactCatalog == null || runCurrencyManager == null || !runCurrencyManager.IsInitialized)
        {
            Debug.LogError("[Shop/ShopManager] ShopTable 및 초기화된 아티팩트·Run 재화 매니저 연결 필요", this);
            return;
        }

        _artifactManager = artifactManager;
        _runCurrencyManager = runCurrencyManager;
        _selector = new ShopArtifactSelector(artifactManager.ArtifactCatalog, artifactManager, _shopTable);
        _purchaseSlots.Clear();
        _exchangeSlots.Clear();
        HasStock = false;
    }

    // 게임 플로우에서 호출 : 상점 UI를 열고 종료 버튼을 누를 때까지 대기 (UI 연결 예정)
    public UniTask OpenShopAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (!IsInitialized || _isTrading)
        {
            Debug.LogWarning("[Shop/ShopManager] 초기화 및 거래 처리 완료 후 상점을 열어주세요.", this);
            return UniTask.CompletedTask;
        }

        if (!HasStock && !TryGenerateStock())
        {
            Debug.LogError("[Shop/ShopManager] 상점 품목 생성에 실패했습니다.", this);
            return UniTask.CompletedTask;
        }

        // UI 연결 시 async UniTask로 변경
        // UI에 이 ShopManager를 전달하여 목록 조회 및 구매·판매·교환 요청 연결
        // 상점 UI Open 및 Close 버튼 대기를 한 함수로 제공받는 경우의 예시
        // await _shopUI.OpenAndWaitForCloseAsync(this, token);
        // token.ThrowIfCancellationRequested();
        // UI 연결 시 중복 Open 요청 방지 및 Run 종료·취소 시 UI 닫기와 대기 상태 정리 필요
        // UI를 열어둔 동안에는 구매·판매가 가능해야 하므로 _isTrading으로 대기 잠금 X

        // UI Close 이후 완료. 현재는 UI 미연결로 즉시 완료
        return UniTask.CompletedTask;
    }

    // 최초 생성 또는 명시적인 갱신 시 호출. UI를 다시 열 때는 기존 목록 사용
    public bool TryGenerateStock()
    {
        if (!IsInitialized || _isTrading ||
            (_shopTable.PurchaseCurrency != CurrencyType.Gold && _shopTable.PurchaseCurrency != CurrencyType.Gem))
        {
            return false;
        }

        // 한쪽 추첨이 실패하면 기존 상점 목록 유지
        if (!_selector.TryCreatePurchaseSlots(out List<ShopArtifactSlot> purchases) ||
            !_selector.TryCreateExchangeSlots(out List<ShopArtifactExchangeSlot> exchanges))
        {
            return false;
        }

        _purchaseSlots = purchases;
        _exchangeSlots = exchanges;
        HasStock = true;
        _isTrading = true;
        ShopChanged?.Invoke();
        _isTrading = false;
        return true;
    }

    // 현재 상점에 있는 미구매 슬롯만 처리. 이전 상점의 슬롯은 사용 불가
    public bool TryPurchase(ShopArtifactSlot slot)
    {
        if (!IsInitialized || _isTrading || slot == null || !_purchaseSlots.Contains(slot) ||
            slot.IsPurchased || slot.Price < 0 ||
            !_runCurrencyManager.CanSpend(slot.Currency, slot.Price))
        {
            return false;
        }

        _isTrading = true;
        bool succeeded = _runCurrencyManager.TryApplyTrade(slot.Currency, -slot.Price, () =>
        {
            if (!_artifactManager.TryAdd(slot.Artifact))
            {
                return false;
            }

            slot.MarkPurchased();
            return true;
        });

        if (succeeded)
        {
            ShopChanged?.Invoke();
        }
        _isTrading = false;
        return succeeded;
    }

    // 판매 UI에서 중첩 1개당 골드 지급량 조회
    public bool TryGetSellPrice(ArtifactData artifact, out int price)
    {
        price = 0;
        if (!IsInitialized || artifact == null || !_artifactManager.ArtifactCatalog.Contains(artifact) ||
            _shopTable.ShopArtifactTable == null || _shopTable.ShopArtifactTable.RaritySettings == null)
        {
            return false;
        }

        bool found = false;
        foreach (ShopArtifactRarityData setting in _shopTable.ShopArtifactTable.RaritySettings)
        {
            if (setting == null)
            {
                return false;
            }
            if (setting.Rarity != artifact.Rarity)
            {
                continue;
            }
            if (found || setting.SellPrice < 0 || setting.SellPrice > setting.MinPrice)
            {
                return false;
            }

            price = setting.SellPrice;
            found = true;
        }
        return found;
    }

    // 보유 중첩 1개 판매. 지급 불가 또는 차감 실패 시 보유 상태와 잔액 유지
    public bool TrySell(ArtifactData artifact)
    {
        if (!IsInitialized || _isTrading || !_artifactManager.Contains(artifact) ||
            !TryGetSellPrice(artifact, out int price))
        {
            return false;
        }

        _isTrading = true;
        bool succeeded = _runCurrencyManager.TryApplyTrade(CurrencyType.Gold, price,
            () => _artifactManager.TryRemove(artifact));
        if (succeeded)
        {
            ShopChanged?.Invoke();
        }
        _isTrading = false;
        return succeeded;
    }

    public List<ArtifactInstance> GetExchangeCandidates(ShopArtifactExchangeSlot slot)
    {
        if (!IsInitialized || _isTrading || slot == null || slot.IsExchanged || !_exchangeSlots.Contains(slot))
        {
            return new List<ArtifactInstance>();
        }

        return _artifactManager.GetExchangeCandidates(slot.Artifact);
    }

    // 교환 UI에서 재료 선택 후 호출. 조건은 ArtifactManager에서 다시 확인
    public bool TryExchange(ShopArtifactExchangeSlot slot, ArtifactData ownedArtifact)
    {
        if (!IsInitialized || _isTrading || slot == null || slot.IsExchanged || !_exchangeSlots.Contains(slot))
        {
            return false;
        }

        _isTrading = true;
        bool succeeded = _artifactManager.TryExchange(ownedArtifact, slot.Artifact);
        if (succeeded)
        {
            slot.MarkExchanged();
            ShopChanged?.Invoke();
        }
        _isTrading = false;
        return succeeded;
    }

    // Run 종료 시 호출. UI를 잠깐 닫는 경우에는 호출하지 않음
    public bool TryEndRun()
    {
        if (_isTrading)
        {
            return false;
        }

        _purchaseSlots.Clear();
        _exchangeSlots.Clear();
        _selector = null;
        _artifactManager = null;
        _runCurrencyManager = null;
        HasStock = false;
        _isTrading = true;
        ShopChanged?.Invoke();
        _isTrading = false;
        return true;
    }
}
