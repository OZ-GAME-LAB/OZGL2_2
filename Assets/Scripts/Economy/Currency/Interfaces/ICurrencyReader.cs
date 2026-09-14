using System;
using System.Collections.Generic;

/// <summary>재화 보유량 조회와 UI 갱신에 사용하는 인터페이스</summary>
public interface ICurrencyReader
{
    /// <summary>전체 보유량입니다. 지갑이 없으면 null을 반환</summary>
    IReadOnlyDictionary<CurrencyData, int> Balances { get; }

    /// <summary>잔액 변경 후 재화 정보, 이전 잔액, 변경 후 잔액을 전달</summary>
    event Action<CurrencyData, int, int> BalanceChanged;

    /// <summary>해당 재화의 보유량을 조회, 조회할 수 없으면 0을 반환</summary>
    int GetBalance(CurrencyType type);
}
