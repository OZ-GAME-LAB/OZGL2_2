using System;
using System.Collections.Generic;
using UnityEngine;

public class RunCurrencyManager : MonoBehaviour
{
    public static RunCurrencyManager Instance { get; private set; }

    public event Action<CurrencyData, int, int> BalanceChanged;

    public IReadOnlyDictionary<CurrencyData, int> Balances => _wallet?.Balances;

    public bool IsInitialized => _wallet != null;

    [SerializeField] private CurrencyCatalog _currencyCatalog;
    [SerializeField] private List<CurrencyAmount> _baseStartingCurrencies =
        new List<CurrencyAmount>();

    private CurrencyWallet _wallet;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        if (!IsInitialized)
        {
            return 0;
        }

        if (!CanUseCurrency(currency))
        {
            return 0;
        }

        return _wallet.GetBalance(currency);
    }

    public bool CanSpend(CurrencyAmount currencyAmount)
    {
        if (!IsInitialized)
        {
            return false;
        }

        if (!CanUseCurrency(currencyAmount.Currency))
        {
            return false;
        }

        return _wallet.CanSpend(currencyAmount);
    }

    public bool TryAdd(CurrencyAmount currencyAmount)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning(
                "[Economy/RunCurrencyManager] Run 재화가 초기화되지 않았습니다.",
                this
            );

            return false;
        }

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

        NotifyBalanceChanged(currency, previousBalance);
        return true;
    }

    public bool TrySpend(CurrencyAmount currencyAmount)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning(
                "[Economy/RunCurrencyManager] Run 재화가 초기화되지 않았습니다.",
                this
            );

            return false;
        }

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

    // 웨이브 클리어 시 지급되는 보상 (웨이브 자체 보상)
    public bool TryApplyWaveReward(CurrencyAmount waveReward)
    {
        // 베이스 캠프 레벨에 비례한 보상 계산 로직을 추가해야 함
        // 도현님의 건물 관련 스크립트에서 베이스 캠프 레벨을 가져와서 보상 계산에 활용 예정
        // 계산 C# 스크립트를 생성해서 해당 스크립트 내에서 제단, 토템, 아티팩트를 가져와서 보상 계산에 활용하도록 구현 예정

        // 현재는 임시로 해둔 상태
        CurrencyAmount calculatedReward = waveReward;

        return TryAdd(calculatedReward);
    }

    // 웨이브 종료 시 생산 건물에서 생산한 재화 지급
    public bool TryApplyProductionReward(CurrencyAmount productionReward)
    {
        // 계산 스크립트에서 제단, 토템, 아티팩트의 생산량 증가 효과를 적용할 예정
        // 웨이브 자체 보상과 구분하여 생산에 해당하는 효과만 계산해야 함
        // 도현님의 건물 관련 스크립트에서 생산 건물의 재화 생산량을 가져와서 계산 예정
        // 현재는 임시로 해둔 상태
        CurrencyAmount calculatedReward = productionReward;

        return TryAdd(calculatedReward);
    }

    // 적 유닛 사망 시 드랍하는 재화 지급
    public bool TryApplyEnemyDropReward(CurrencyAmount dropReward)
    {
        // 계산 스크립트에서 제단, 토템, 아티팩트의 적 드랍 보상 관련 효과를 적용할 예정
        // 적 유닛 실제 사망 확정 시 기본 드랍 재화와 수량을 전달

        // 현재는 임시로 해둔 상태
        CurrencyAmount calculatedReward = dropReward;

        return TryAdd(calculatedReward);
    }

    // 게임 시작 시에 Run 재화 초기화
    public bool TryInitialize()
    {
        if (IsInitialized)
        {
            Debug.LogWarning(
                "[Economy/RunCurrencyManager] Run 재화가 이미 초기화되어 있습니다.",
                this
            );

            return false;
        }

        if (!ValidateSettings())
        {
            return false;
        }

        CurrencyWallet newWallet =
            new CurrencyWallet(CurrencyLifetime.Run);

        if (!TryRegisterRunCurrencies(newWallet))
        {
            return false;
        }

        if (_baseStartingCurrencies != null)
        {
            for (int i = 0; i < _baseStartingCurrencies.Count; i++)
            {
                CurrencyAmount startingCurrency =
                    _baseStartingCurrencies[i];

                if (!CanUseCurrency(startingCurrency.Currency))
                {
                    return false;
                }

                // 계산 스크립트에 기본 시작 재화를 전달해 제단,토템 등의 효과를 적용 예정
                // 현재는 임의로 Inspector에 설정한 수량을 그대로 사용
                CurrencyAmount calculatedReward = startingCurrency;

                if (!newWallet.TryAdd(calculatedReward))
                {
                    return false;
                }
            }
        }

        _wallet = newWallet;

        Debug.Log(
            "[Economy/RunCurrencyManager] Run 재화를 초기화했습니다.",
            this
        );

        return true;
    }


    // 게임 종료 시에 Run 재화를 제거 (혈석 정산 메서드와 같이 호출)
    public bool TryEndRun()
    {
        if (!IsInitialized)
        {
            return false;
        }

        _wallet = null;

        Debug.Log(
            "[Economy/RunCurrencyManager] 모든 Run 재화를 제거했습니다.",
            this
        );

        return true;
    }

    private void NotifyBalanceChanged(CurrencyData currency, int previousBalance)
    {
        int newBalance = _wallet.GetBalance(currency);

        if (previousBalance != newBalance)
        {
            BalanceChanged?.Invoke(currency, previousBalance, newBalance);
        }
    }

    private bool TryRegisterRunCurrencies(
        CurrencyWallet wallet)
    {
        foreach (CurrencyData currency in _currencyCatalog.Currencies)
        {
            if (currency == null)
            {
                continue;
            }

            if (currency.Lifetime != CurrencyLifetime.Run)
            {
                continue;
            }

            if (!_currencyCatalog.Contains(currency))
            {
                continue;
            }

            if (!wallet.TryRegister(currency))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateSettings()
    {
        if (_currencyCatalog != null)
        {
            return true;
        }

        Debug.LogError(
            "[Economy/RunCurrencyManager] CurrencyCatalog가 연결되지 않았습니다.",
            this
        );

        return false;
    }

    private bool CanUseCurrency(CurrencyData currency)
    {
        if (currency == null)
        {
            Debug.LogError(
                "[Economy/RunCurrencyManager] CurrencyData가 null입니다.",
                this
            );

            return false;
        }

        if (_currencyCatalog == null)
        {
            Debug.LogError(
                "[Economy/RunCurrencyManager] CurrencyCatalog가 연결되지 않았습니다.",
                this
            );

            return false;
        }

        if (!_currencyCatalog.Contains(currency))
        {
            Debug.LogError(
                $"[Economy/RunCurrencyManager] Catalog에 등록되지 않은 재화입니다. Currency: {currency.name}",
                currency
            );

            return false;
        }

        if (currency.Lifetime != CurrencyLifetime.Run)
        {
            Debug.LogError(
                $"[Economy/RunCurrencyManager] Run 재화가 아닙니다. Currency: {currency.name}",
                currency
            );

            return false;
        }

        return true;
    }
}
