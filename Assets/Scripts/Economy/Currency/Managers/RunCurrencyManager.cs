using System;
using System.Collections.Generic;
// using Game.Core;
using UnityEngine;

public class RunCurrencyManager : MonoBehaviour
{
    public event Action<CurrencyData, int, int> BalanceChanged;

    public IReadOnlyDictionary<CurrencyData, int> Balances => _wallet?.Balances;

    public bool IsInitialized => _wallet != null;

    [SerializeField] private CurrencyCatalog _currencyCatalog;
    [SerializeField] private WaveRewardTable _waveRewardTable;
    [SerializeField] private List<CurrencyAmount> _baseStartingCurrencies =
        new List<CurrencyAmount>();

    private CurrencyWallet _wallet;
    // private WaveController _waveController;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    private void OnDestroy()
    {
        _wallet = null;
        // _waveController = null;
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

    // 웨이브 클리어 시 호출 : 웨이브 클리어로 지급되는 보상 (웨이브 자체 보상)
    // public bool TryApplyWaveReward()
    // {
    //     if (!IsInitialized)
    //     {
    //         Debug.LogWarning("[Economy/RunCurrencyManager] Run 재화가 초기화되지 않았습니다.", this);
    //         return false;
    //     }

    //     if (_waveRewardTable == null)
    //     {
    //         Debug.LogError("[Economy/RunCurrencyManager] WaveRewardTable이 연결되지 않았습니다.", this);
    //         return false;
    //     }

    //     if (_waveController == null)
    //     {
    //         Debug.LogError("[Economy/RunCurrencyManager] WaveController 참조가 없습니다.", this);
    //         return false;
    //     }

    //     int quarterNumber = _waveController.CurQuarter;
    //     int waveNumber = _waveController.CurWave;

    //     //건물 스크립트에서 실제 베이스캠프 레벨 프로퍼티를 읽어와야 함. 현재는 임시로 1 고정
    //     int baseCampLevel = 1;

    //     // 6분기 이후에는 5분기의 같은 웨이브 보상을 사용하도록 설정(임시)
    //     int rewardQuarterNumber = Mathf.Min(quarterNumber, 5);

    //     if (!_waveRewardTable.TryGetRewards(rewardQuarterNumber, waveNumber, out IReadOnlyList<CurrencyAmount> rewards))
    //     {
    //         Debug.LogError($"[Economy/RunCurrencyManager] 웨이브 보상 설정을 확인하세요. Quarter: {rewardQuarterNumber}, Wave: {waveNumber}", this);
    //         return false;
    //     }

    //     CurrencyAmount[] calculatedRewards = new CurrencyAmount[rewards.Count];
    //     int[] previousBalances = new int[rewards.Count];

    //     // 보상 테이블은 중복 재화를 거부하도록 구성
    //     for (int i = 0; i < rewards.Count; i++)
    //     {
    //         CurrencyAmount reward = _rewardCalculator.CalculateWaveReward(rewards[i], baseCampLevel);

    //         if (!CanUseCurrency(reward.Currency) || reward.Currency != rewards[i].Currency ||
    //             !_wallet.Balances.ContainsKey(reward.Currency) || reward.Amount < 0)
    //             return false;

    //         int balance = _wallet.GetBalance(reward.Currency);

    //         if (balance > int.MaxValue - reward.Amount)
    //         {
    //             return false;
    //         }

    //         calculatedRewards[i] = reward;
    //         previousBalances[i] = balance;
    //     }

    //     // RunCurrencyManager.TryAdd는 즉시 이벤트를 보내므로 여기서는 wallet.TryAdd 이후 아래에서 이벤트 발생
    //     foreach (CurrencyAmount reward in calculatedRewards)
    //     {
    //         _wallet.TryAdd(reward);
    //     }

    //     for (int i = 0; i < calculatedRewards.Length; i++)
    //     {
    //         CurrencyAmount reward = calculatedRewards[i];
    //         if (reward.Amount > 0)
    //         {
    //             BalanceChanged?.Invoke(reward.Currency, previousBalances[i], previousBalances[i] + reward.Amount);
    //         }
    //     }

    //     return true;
    // }

    // 새로운 웨이브 준비 페이즈 돌입 시 호출 : 생산 건물에서 생산한 재화 지급
    public bool TryApplyProductionReward(CurrencyAmount productionReward)
    {
        CurrencyAmount calculatedReward =
            _rewardCalculator.CalculateProductionReward(productionReward);

        return TryAdd(calculatedReward);
    }

    // 적 유닛 사망 시 드랍하는 재화 지급
    public bool TryApplyEnemyDropReward(CurrencyAmount dropReward)
    {
        // 적 유닛 실제 사망 확정 시 기본 드랍 재화와 수량을 전달
        CurrencyAmount calculatedReward =
            _rewardCalculator.CalculateEnemyDropReward(dropReward);

        return TryAdd(calculatedReward);
    }

    // 게임 시작 시에 Run 재화 초기화
    // public void Initialize(WaveController waveController)
    // {
    //     if (IsInitialized)
    //     {
    //         Debug.LogWarning(
    //             "[Economy/RunCurrencyManager] Run 재화가 이미 초기화되어 있습니다.",
    //             this
    //         );

    //         return;
    //     }

    //     if (waveController == null)
    //     {
    //         Debug.LogError("[Economy/RunCurrencyManager] WaveController 참조가 없습니다.", this);
    //         return;
    //     }

    //     if (!ValidateSettings())
    //     {
    //         return;
    //     }

    //     CurrencyWallet newWallet =
    //         new CurrencyWallet(CurrencyLifetime.Run);

    //     if (!TryRegisterRunCurrencies(newWallet))
    //     {
    //         return;
    //     }

    //     if (_baseStartingCurrencies != null)
    //     {
    //         for (int i = 0; i < _baseStartingCurrencies.Count; i++)
    //         {
    //             CurrencyAmount startingCurrency =
    //                 _baseStartingCurrencies[i];

    //             if (!CanUseCurrency(startingCurrency.Currency))
    //             {
    //                 return;
    //             }

    //             CurrencyAmount calculatedReward =
    //                 _rewardCalculator.CalculateStartingReward(startingCurrency);

    //             if (!newWallet.TryAdd(calculatedReward))
    //             {
    //                 return;
    //             }
    //         }
    //     }

    //     _waveController = waveController;
    //     _wallet = newWallet;

    //     Debug.Log(
    //         "[Economy/RunCurrencyManager] Run 재화를 초기화했습니다.",
    //         this
    //     );
    // }

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
