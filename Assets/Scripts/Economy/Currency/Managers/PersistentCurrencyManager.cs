using System;
using System.Collections.Generic;
using UnityEngine;

public class PersistentCurrencyManager : MonoBehaviour
{
    public static PersistentCurrencyManager Instance { get; private set; }

    public event Action<CurrencyData, int, int> BalanceChanged;

    public IReadOnlyDictionary<CurrencyData, int> Balances => _wallet?.Balances;

    [SerializeField] private CurrencyCatalog _currencyCatalog;

    private CurrencyWallet _wallet;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        if (!ValidateSettings())
        {
            return;
        }

        InitializeWallet();
    }

    private void OnDestroy()
    {
        _wallet = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public int GetBalance(CurrencyType type)
    {
        if (_wallet == null || _currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(type, out CurrencyData currency))
        {
            return 0;
        }

        return GetBalance(currency);
    }

    public int GetBalance(CurrencyData currency)
    {
        if (!CanUseCurrency(currency))
        {
            return 0;
        }

        return _wallet.GetBalance(currency);
    }

    public bool CanSpend(CurrencyAmount currencyAmount)
    {
        if (!CanUseCurrency(currencyAmount.Currency))
        {
            return false;
        }

        return _wallet.CanSpend(currencyAmount);
    }

    public bool TrySpend(CurrencyAmount currencyAmount)
    {
        if (!CanUseCurrency(currencyAmount.Currency))
        {
            return false;
        }

        CurrencyData currency = currencyAmount.Currency;
        int previousBalance = _wallet.GetBalance(currency);

        if (!_wallet.TrySpend(currencyAmount))
        {
            return false;
        }

        NotifyBalanceChanged(currency, previousBalance);
        return true;
    }

    public bool TryAdd(CurrencyAmount currencyAmount)
    {
        if (!CanUseCurrency(currencyAmount.Currency))
        {
            return false;
        }

        CurrencyData currency = currencyAmount.Currency;
        int previousBalance = _wallet.GetBalance(currency);

        if (!_wallet.TryAdd(currencyAmount))
        {
            return false;
        }

        Debug.Log(
            $"[Economy/PersistentCurrencyManager] 영구 재화 지급 | {currency.DisplayName} +{currencyAmount.Amount}",
            this
        );

        NotifyBalanceChanged(currency, previousBalance);
        return true;
    }

    // Run 기록으로 혈석 정산 보상을 계산하고 지급합니다.
    // 임시로 파라마터로 웨이브 클리어 수, 유닛 처치 수, 보스 처치 수를 받습니다.
    // 협의 이후 어떤 방식으로 받을지 논의 필요
    public bool TryApplyReward(int totalWaveCleared, int totalUnitsKilled, int totalBossesKilled)
    {
        if (_currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
        {
            Debug.LogError("[Economy/PersistentCurrencyManager] 혈석 재화 설정을 확인하세요.", this);
            return false;
        }

        CurrencyAmount reward = _rewardCalculator.CalculateRunSettlementReward(
            currency, totalWaveCleared, totalUnitsKilled, totalBossesKilled);

        return TryAdd(reward);
    }

    private void NotifyBalanceChanged(CurrencyData currency, int previousBalance)
    {
        int newBalance = _wallet.GetBalance(currency);

        if (previousBalance != newBalance)
        {
            BalanceChanged?.Invoke(currency, previousBalance, newBalance);
        }
    }

    private void InitializeWallet()
    {
        _wallet =
            new CurrencyWallet(CurrencyLifetime.Persistent);

        foreach (CurrencyData currency in _currencyCatalog.Currencies)
        {
            if (currency == null)
            {
                continue;
            }

            if (currency.Lifetime != CurrencyLifetime.Persistent)
            {
                continue;
            }

            if (!_currencyCatalog.Contains(currency))
            {
                continue;
            }

            _wallet.TryRegister(currency);
        }
    }

    private bool ValidateSettings()
    {
        if (_currencyCatalog != null)
        {
            return true;
        }

        Debug.LogError(
            "[Economy/PersistentCurrencyManager] CurrencyCatalog가 연결되지 않았습니다.",
            this
        );

        return false;
    }

    private bool CanUseCurrency(CurrencyData currency)
    {
        if (currency == null)
        {
            Debug.LogError(
                "[Economy/PersistentCurrencyManager] CurrencyData가 null입니다.",
                this
            );

            return false;
        }

        if (_currencyCatalog == null)
        {
            Debug.LogError(
                "[Economy/PersistentCurrencyManager] CurrencyCatalog가 연결되지 않았습니다.",
                this
            );

            return false;
        }

        if (_wallet == null)
        {
            Debug.LogError(
                "[Economy/PersistentCurrencyManager] Persistent Wallet이 초기화되지 않았습니다.",
                this
            );

            return false;
        }

        if (!_currencyCatalog.Contains(currency))
        {
            Debug.LogError(
                $"[Economy/PersistentCurrencyManager] Catalog에 등록되지 않은 재화입니다. Currency: {currency.name}",
                currency
            );

            return false;
        }

        if (currency.Lifetime != CurrencyLifetime.Persistent)
        {
            Debug.LogError(
                $"[Economy/PersistentCurrencyManager] Persistent 재화가 아닙니다. Currency: {currency.name}",
                currency
            );

            return false;
        }

        return true;
    }
}
