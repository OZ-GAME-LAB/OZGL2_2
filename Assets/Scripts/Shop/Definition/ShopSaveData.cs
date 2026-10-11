using System;
using System.Collections.Generic;

[Serializable]
public class ShopSaveData
{
    // 상점 상품 추첨을 완료했는지 나타냅니다. 상품이 없어도 true이면 재추첨하지 않습니다.
    public bool HasStock;
    // 후보 생성 분기. 0은 미생성 또는 분기 정보가 없는 이전 저장 데이터입니다.
    public int StockQuarter;
    // 판매 아티팩트 목록입니다. 인덱스가 상품 슬롯 번호이며 구매한 상품도 포함합니다.
    public List<ShopPurchaseSaveEntry> Artifacts = new();
    // 판매 소모성 아이템 목록입니다. 인덱스가 상품 슬롯 번호이며 구매한 상품도 포함합니다.
    public List<ShopPurchaseSaveEntry> Consumables = new();
    // 교환 아티팩트 목록입니다. 슬롯 순서와 교환 완료 상태를 유지합니다.
    public List<ShopExchangeSaveEntry> Exchanges = new();
}

[Serializable]
public class ShopPurchaseSaveEntry
{
    // 소속 목록에 따라 아티팩트 또는 소모성 아이템을 카탈로그에서 찾기 위한 ID입니다.
    public string ItemId;
    // 이 상품의 구매에 사용하는 재화 종류입니다.
    public CurrencyType Currency;
    // 상품 생성 시 확정된 가격입니다. 복원할 때 다시 추첨하지 않습니다.
    public int Price;
    // 이미 구매한 상품인지 나타냅니다. 복원 후 중복 구매를 방지합니다.
    public bool Purchased;
}

[Serializable]
public class ShopExchangeSaveEntry
{
    // 교환 대상으로 제시된 아티팩트의 고유 ID입니다.
    public string ArtifactId;
    // 해당 슬롯의 교환을 이미 완료했는지 나타냅니다.
    public bool Exchanged;
}
