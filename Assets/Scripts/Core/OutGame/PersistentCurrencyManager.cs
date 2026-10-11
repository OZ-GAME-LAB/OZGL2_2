using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>테스트 혈석 상태를 관리합니다. 파일 저장은 OutGameSaveCoordinator가 담당합니다.</summary>
public class PersistentCurrencyManager : MonoBehaviour, ICurrencyReader, ICurrencySpender, ISaveDataProvider<PersistentWalletSaveData>
{
    [SerializeField, Min(0)] private int _initialBloodstone = 500;
    [SerializeField] private CurrencyData _bloodstone;

    public int Balance { get; private set; }
    public bool IsInitialized => _initialized;
    public event Action Changed; // <--- 추후 제거해야할 이벤트

    public IReadOnlyDictionary<CurrencyData, int> Balances => _balances;
    public event Action<CurrencyData, int, int> BalanceChanged;

    private bool _initialized;
    private int _lastNotifiedBalance;
    private readonly Dictionary<CurrencyData, int> _balances = new Dictionary<CurrencyData, int>();

    public void Initialize()
    {
        Balance = Mathf.Max(0, _initialBloodstone);
        SyncBalances();
        _lastNotifiedBalance = Balance;
        _initialized = true;
    }

    public int GetBalance(CurrencyType type)
    {
        return _initialized && type == CurrencyType.Bloodstone ? Balance : 0;
    }

    public bool TryAdd(CurrencyType type, int amount)
    {
        if (!_initialized || type != CurrencyType.Bloodstone || amount < 0) return false;
        if (amount > int.MaxValue - Balance) return false;
        if (amount == 0) return true;

        Balance += amount;
        SyncBalances();
        NotifyChanged();
        return true;
    }

    public bool CanSpend(CurrencyType type, int amount)
    {
        return _initialized && type == CurrencyType.Bloodstone && amount >= 0 && Balance >= amount;
    }

    public bool TrySpend(CurrencyType type, int amount)
    {
        if (!CanSpend(type, amount)) return false;
        if (amount == 0) return true;

        Balance -= amount;
        SyncBalances();
        NotifyChanged();
        return true;
    }

    public PersistentWalletSaveData CaptureSaveData()
    {
        if (!_initialized) throw new InvalidOperationException("혈석 지갑을 먼저 초기화해주세요.");
        return new PersistentWalletSaveData(Balance);
    }

    public bool TryValidateSaveData(PersistentWalletSaveData data, out string error)
    {
        error = null;
        if (!_initialized)
        {
            error = "혈석 지갑을 먼저 초기화해주세요.";
            return false;
        }
        if (data == null || data.Type != CurrencyType.Bloodstone || data.Amount < 0)
        {
            error = "혈석 저장 데이터/재화 종류가 올바르지 않거나 잔액이 음수입니다.";
            return false;
        }
        return true;
    }

    public void RestoreSaveData(PersistentWalletSaveData data)
    {
        RestoreSaveData(data, true);
    }

    public void RestoreSaveData(PersistentWalletSaveData data, bool notifyChanged)
    {
        if (!TryValidateSaveData(data, out string error)) throw new ArgumentException(error);
        Balance = data.Amount;
        SyncBalances();
        if (notifyChanged) NotifyChanged();
    }

    // ICurrencyReader 내에 있는 Dictionary<CurrencyData, int> Balances를 갱신을 위한 함수
    private void SyncBalances()
    {
        _balances.Clear();
        if (_bloodstone == null) return;

        _balances[_bloodstone] = Balance;
    }

    public void NotifyChanged()
    {
        if (!_initialized)
        {
            return;
        }

        // 이전 잔액 + 현재 잔액을 비교하여 변경 이벤트를 발생
        int previousBalance = _lastNotifiedBalance;
        int currentBalance = Balance;
        _lastNotifiedBalance = currentBalance;

        if (previousBalance != currentBalance)
        {
            BalanceChanged?.Invoke(_bloodstone, previousBalance, currentBalance);
        }

        // 추후 삭제 예정인 이벤트
        Changed?.Invoke();
    }
}
