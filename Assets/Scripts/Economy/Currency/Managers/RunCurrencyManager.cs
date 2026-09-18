using System;
using System.Collections.Generic;
using Game.Core;
using OZGL.KDH;
using UnityEngine;

public class RunCurrencyManager : MonoBehaviour, ICurrencyReader, ICurrencySpender, IRunCurrencyRewards
{
    public event Action<CurrencyData, int, int> BalanceChanged;

    public IReadOnlyDictionary<CurrencyData, int> Balances => _wallet?.Balances;

    public bool IsInitialized => _wallet != null;

    // 보상 UI 표시용 : BattlePreparing에서 확정한 현재 웨이브의 지급 예정 재화량
    public int CurrentGoldReward { get; private set; }
    public int CurrentGemReward { get; private set; }

    [SerializeField] private CurrencyCatalog _currencyCatalog;
    [SerializeField] private WaveRewardTable _waveRewardTable;
    private EffectManager _effectManager;
    private BuildingCoreProgress _buildingCoreProgress;
    [SerializeField] private List<CurrencyAmount> _baseStartingCurrencies =
        new List<CurrencyAmount>();

    private CurrencyWallet _wallet;
    private WaveController _waveController;
    private GameFlowController _gameFlowController;
    private CurrencyAmount[] _preparedRewards;
    private int _preparedQuarter;
    private int _preparedWave;
    private bool _waveRewardApplied;
    private bool _isTrading;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    private void OnDestroy()
    {
        UnsubscribeGameFlow();
        ClearWaveReward();
        _wallet = null;
        _waveController = null;
    }

    public int GetBalance(CurrencyType type)
    {
        if (!IsInitialized)
        {
            return 0;
        }

        if (!TryGetCurrency(type, out CurrencyData currency) || !CanUseCurrency(currency))
        {
            return 0;
        }

        return _wallet.GetBalance(currency);
    }

    public bool CanSpend(CurrencyType type, int amount)
    {
        if (!IsInitialized)
        {
            return false;
        }

        if (!TryGetCurrency(type, out CurrencyData currency) || !CanUseCurrency(currency))
        {
            return false;
        }

        return _wallet.CanSpend(new CurrencyAmount(currency, amount));
    }

    public bool TryAdd(CurrencyType type, int amount)
    {
        if (!TryGetCurrency(type, out CurrencyData currency))
        {
            return false;
        }

        return TryAddInternal(new CurrencyAmount(currency, amount));
    }

    // 실질적으로 Wallet에 추가하고 이벤트를 발생시키는 내부 메서드
    private bool TryAddInternal(CurrencyAmount currencyAmount)
    {
        if (_isTrading)
        {
            return false;
        }

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

    public bool TrySpend(CurrencyType type, int amount)
    {
        if (_isTrading)
        {
            return false;
        }

        if (!IsInitialized)
        {
            Debug.LogWarning(
                "[Economy/RunCurrencyManager] Run 재화가 초기화되지 않았습니다.",
                this
            );

            return false;
        }

        if (!TryGetCurrency(type, out CurrencyData currency) || !CanUseCurrency(currency))
        {
            return false;
        }

        int previousBalance = _wallet.GetBalance(currency);

        if (!_wallet.TrySpend(new CurrencyAmount(currency, amount)))
        {
            return false;
        }

        NotifyBalanceChanged(currency, previousBalance);
        return true;
    }

    // 상점 거래용. 양수는 지급, 음수는 소비. 품목 처리 실패 시 잔액 복원 후 알림 X
    // applyItemChange는 실패 시 품목 상태를 유지하는 동기 함수만 전달
    public bool TryApplyTrade(CurrencyType type, int balanceChange, Func<bool> applyItemChange)
    {
        if (!IsInitialized || _isTrading || applyItemChange == null ||
            !TryGetCurrency(type, out CurrencyData currency) || !CanUseCurrency(currency) ||
            !_wallet.Balances.ContainsKey(currency))
        {
            return false;
        }

        CurrencyWallet wallet = _wallet;
        int before = wallet.GetBalance(currency);
        long after = (long)before + balanceChange;
        if (after < 0 || after > int.MaxValue)
        {
            return false;
        }

        _isTrading = true;
        wallet.TrySetBalance(currency, (int)after);
        if (!applyItemChange())
        {
            wallet.TrySetBalance(currency, before);
            _isTrading = false;
            return false;
        }

        if (before != after)
        {
            BalanceChanged?.Invoke(currency, before, (int)after);
        }
        _isTrading = false;
        return true;
    }

    // BattlePreparing 시 호출 : 현재 효과로 웨이브 보상을 계산하고 고정
    public bool TryPrepareWaveReward()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Economy/RunCurrencyManager] Run 재화가 초기화되지 않았습니다.", this);
            return false;
        }

        if (_waveRewardTable == null)
        {
            Debug.LogError("[Economy/RunCurrencyManager] WaveRewardTable이 연결되지 않았습니다.", this);
            return false;
        }

        if (_waveController == null)
        {
            Debug.LogError("[Economy/RunCurrencyManager] WaveController 참조가 없습니다.", this);
            return false;
        }

        if (_preparedRewards != null && _preparedQuarter == _waveController.CurQuarter &&
            _preparedWave == _waveController.CurWave)
        {
            return true;
        }

        ClearWaveReward();
        int quarterNumber = _waveController.CurQuarter;
        int waveNumber = _waveController.CurWave;
        if (quarterNumber < 1 || waveNumber < 1)
        {
            return false;
        }

        //건물 스크립트에서 실제 베이스캠프 레벨 프로퍼티를 읽어와야 함. 현재는 임시로 1 고정
        int baseCampLevel = _buildingCoreProgress.CurrentLevel;

        // 4분기 이후에는 3분기의 같은 웨이브 보상을 사용하도록 설정(임시)
        int rewardQuarterNumber = Mathf.Min(quarterNumber, WaveController.MAIN_QUARTERS);

        if (!_waveRewardTable.TryGetRewards(rewardQuarterNumber, waveNumber, out IReadOnlyList<CurrencyAmount> rewards))
        {
            Debug.LogError($"[Economy/RunCurrencyManager] 웨이브 보상 설정을 확인하세요. Quarter: {rewardQuarterNumber}, Wave: {waveNumber}", this);
            return false;
        }

        CurrencyAmount[] calculatedRewards = new CurrencyAmount[rewards.Count];

        int goldReward = 0;
        int gemReward = 0;

        // 보상 테이블은 중복 재화를 거부하도록 구성
        for (int i = 0; i < rewards.Count; i++)
        {
            CurrencyAmount reward = _rewardCalculator.CalculateWaveReward(rewards[i], baseCampLevel,
                _effectManager != null ? _effectManager.CurrencyModifiers : null);

            if (!CanUseCurrency(reward.Currency) || reward.Currency != rewards[i].Currency ||
                !_wallet.Balances.ContainsKey(reward.Currency) || reward.Amount < 0)
                return false;

            calculatedRewards[i] = reward;

            if (reward.Currency.Type == CurrencyType.Gold)
            {
                goldReward = reward.Amount;
            }
            else if (reward.Currency.Type == CurrencyType.Gem)
            {
                gemReward = reward.Amount;
            }
        }

        _preparedRewards = calculatedRewards;
        _preparedQuarter = quarterNumber;
        _preparedWave = waveNumber;
        CurrentGoldReward = goldReward;
        CurrentGemReward = gemReward;
        return true;
    }

    // 웨이브 클리어 시 호출 : 미리 확정한 보상을 재계산 없이 한 번만 지급
    public bool TryApplyWaveReward()
    {
        if (!IsInitialized || _isTrading || _waveController == null || _preparedRewards == null ||
            _waveRewardApplied || _preparedQuarter != _waveController.CurQuarter ||
            _preparedWave != _waveController.CurWave)
        {
            return false;
        }

        CurrencyAmount[] rewards = _preparedRewards;
        int[] previousBalances = new int[rewards.Length];
        // 전투 중 잔액이 바뀔 수 있으므로 실제 지급 직전에 전체 검증
        for (int i = 0; i < rewards.Length; i++)
        {
            CurrencyAmount reward = rewards[i];
            if (!CanUseCurrency(reward.Currency) || !_wallet.Balances.ContainsKey(reward.Currency) || reward.Amount < 0)
            {
                return false;
            }
            int balance = _wallet.GetBalance(reward.Currency);
            if (balance > int.MaxValue - reward.Amount)
            {
                return false;
            }
            previousBalances[i] = balance;
        }

        foreach (CurrencyAmount reward in rewards)
        {
            _wallet.TryAdd(reward);
        }

        // 이벤트에서 다시 지급을 요청해도 중복 지급되지 않도록 먼저 완료 처리
        _waveRewardApplied = true;
        for (int i = 0; i < rewards.Length; i++)
        {
            CurrencyAmount reward = rewards[i];
            if (reward.Amount > 0)
            {
                BalanceChanged?.Invoke(reward.Currency, previousBalances[i], previousBalances[i] + reward.Amount);
            }
        }
        return true;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.BattlePreparing:
                if (!TryPrepareWaveReward())
                {
                    Debug.LogError("[Economy/RunCurrencyManager] 웨이브 보상 준비에 실패했습니다.", this);
                }
                break;

            case GamePhase.Reward:
                // 같은 웨이브의 Reward 재진입 시 중복 지급 방지
                if (_waveRewardApplied)
                {
                    return;
                }

                if (!TryApplyWaveReward())
                {
                    Debug.LogError("[Economy/RunCurrencyManager] 준비된 웨이브 보상 지급에 실패했습니다.", this);
                }
                break;

            case GamePhase.None:
            case GamePhase.Finished:
                ClearWaveReward();
                break;
        }
    }

    private void ClearWaveReward()
    {
        _preparedRewards = null;
        _preparedQuarter = 0;
        _preparedWave = 0;
        _waveRewardApplied = false;
        CurrentGoldReward = 0;
        CurrentGemReward = 0;
    }

    private void UnsubscribeGameFlow()
    {
        if (_gameFlowController != null)
        {
            _gameFlowController.PhaseChanged -= HandlePhaseChanged;
        }
        _gameFlowController = null;
    }

    // 새로운 웨이브 준비 페이즈 돌입 시 호출 : 생산 건물에서 생산한 재화 지급
    public bool TryApplyProductionReward(CurrencyType type, int amount)
    {
        if (!TryGetCurrency(type, out CurrencyData currency))
        {
            return false;
        }

        CurrencyAmount calculatedReward =
            _rewardCalculator.CalculateProductionReward(new CurrencyAmount(currency, amount),
                _effectManager != null ? _effectManager.CurrencyModifiers : null);

        return TryAddInternal(calculatedReward);
    }

    // 적 유닛 사망 시 드랍하는 재화 지급
    // public bool TryApplyEnemyDropReward(CurrencyType type, int amount)
    // {
    //     if (!TryGetCurrency(type, out CurrencyData currency))
    //     {
    //         return false;
    //     }

    //     // 적 유닛 실제 사망 확정 시 기본 드랍 재화와 수량을 전달
    //     CurrencyAmount calculatedReward =
    //         _rewardCalculator.CalculateEnemyDropReward(new CurrencyAmount(currency, amount));

    //     return TryAddInternal(calculatedReward);
    // }

    // 게임 시작 시에 Run 재화 초기화
    // effectManager가 null이면 효과 보정 없이 기본 보상을 사용합니다.
    public void Initialize(WaveController waveController, GameFlowController gameFlowController, EffectManager effectManager, BuildingCoreProgress buildingCoreProgress)
    {
        if (IsInitialized)
        {
            Debug.LogWarning(
                "[Economy/RunCurrencyManager] Run 재화가 이미 초기화되어 있습니다.",
                this
            );

            return;
        }

        if (waveController == null)
        {
            Debug.LogError("[Economy/RunCurrencyManager] WaveController 참조가 없습니다.", this);
            return;
        }

        if (!ValidateSettings())
        {
            return;
        }

        CurrencyWallet newWallet =
            new CurrencyWallet(CurrencyLifetime.Run);

        if (!TryRegisterRunCurrencies(newWallet))
        {
            return;
        }

        if (_baseStartingCurrencies != null)
        {
            for (int i = 0; i < _baseStartingCurrencies.Count; i++)
            {
                CurrencyAmount startingCurrency =
                    _baseStartingCurrencies[i];

                if (!CanUseCurrency(startingCurrency.Currency))
                {
                    return;
                }

                CurrencyAmount calculatedReward =
                    _rewardCalculator.CalculateStartingReward(startingCurrency);

                if (!newWallet.TryAdd(calculatedReward))
                {
                    return;
                }
            }
        }

        UnsubscribeGameFlow();
        _gameFlowController = gameFlowController;
        if (_gameFlowController != null)
        {
            _gameFlowController.PhaseChanged += HandlePhaseChanged;
        }
        ClearWaveReward();
        _waveController = waveController;
        _effectManager = effectManager;
        _buildingCoreProgress = buildingCoreProgress;
        _wallet = newWallet;
        CurrentGoldReward = 0;
        CurrentGemReward = 0;

        Debug.Log(
            "[Economy/RunCurrencyManager] Run 재화를 초기화했습니다.",
            this
        );
    }

    // 게임 종료 시에 Run 재화를 제거 (혈석 정산 메서드와 같이 호출)
    public bool TryEndRun()
    {
        if (!IsInitialized || _isTrading)
        {
            return false;
        }

        UnsubscribeGameFlow();
        ClearWaveReward();
        _wallet = null;
        CurrentGoldReward = 0;
        CurrentGemReward = 0;

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

    private bool TryGetCurrency(CurrencyType type, out CurrencyData currency)
    {
        currency = null;

        if (!ValidateSettings())
        {
            return false;
        }

        if (!_currencyCatalog.TryGetByType(type, out currency))
        {
            Debug.LogError($"[Economy/RunCurrencyManager] Catalog에서 재화를 찾을 수 없습니다. Type: {type}", this);
            return false;
        }

        return true;
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
