/// <summary>건설 및 구매 등 재화 소비에 사용하는 인터페이스</summary>
public interface ICurrencySpender
{
    /// <summary>실제 차감 없이 소비 가능 여부를 확인</summary>
    bool CanSpend(CurrencyType type, int amount);

    /// <summary>성공하면 차감 후 true를 반환, 실패하면 잔액을 유지</summary>
    bool TrySpend(CurrencyType type, int amount);
}
