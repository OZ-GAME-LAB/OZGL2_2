using System;
using System.Collections.Generic;

// 상점 UI에서 품목·가격 조회 및 변경 알림에 사용
public interface IShopReader
{
    bool IsInitialized { get; }
    bool HasStock { get; }

    IReadOnlyList<ShopArtifactSlot> PurchaseSlots { get; }
    IReadOnlyList<ShopArtifactExchangeSlot> ExchangeSlots { get; }

    event Action ShopChanged;

    bool TryGetSellPrice(ArtifactData artifact, out int price);

    List<ArtifactInstance> GetExchangeCandidates(ShopArtifactExchangeSlot slot);
}
