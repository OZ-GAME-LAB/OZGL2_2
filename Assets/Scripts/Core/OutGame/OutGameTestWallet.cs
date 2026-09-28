using System;
using UnityEngine;

/// <summary>테스트 혈석 상태를 관리합니다. 파일 저장은 PersistentSaveCoordinator가 담당합니다.</summary>
public class OutGameTestWallet : MonoBehaviour, IPersistentWallet
{
    [SerializeField, Min(0)] private int _initialBloodstone = 500;

    public int Balance { get; private set; }
    public bool IsInitialized => _initialized;
    public event Action Changed;

    private bool _initialized;

    public void Initialize()
    {
        Balance = Mathf.Max(0, _initialBloodstone);
        _initialized = true;
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
        Changed?.Invoke();
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
        if (data == null || data.Version != 1 || data.Bloodstone < 0)
        {
            error = "혈석 저장 영역/버전이 올바르지 않거나 잔액이 음수입니다.";
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
        Balance = data.Bloodstone;
        if (notifyChanged) NotifyChanged();
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
