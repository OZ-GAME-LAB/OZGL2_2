using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

public class PersistentCurrencyManager : MonoBehaviour, ICurrencyReader, ICurrencySpender, IRunSettlementRewards
{
    public event Action<CurrencyData, int, int> BalanceChanged;

    public IReadOnlyDictionary<CurrencyData, int> Balances => _wallet?.Balances;

    [SerializeField] private CurrencyCatalog _currencyCatalog;
    private EffectManager _effectManager;
    private WaveController _waveController;
    private GameFlowController _gameFlowController;

    private CurrencyWallet _wallet;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    // 현재 Run의 웨이브·게임 플로우·효과 매니저 참조 연결 (기존 지갑과 잔액 유지)
    public void Initialize(WaveController waveController, GameFlowController gameFlowController, EffectManager effectManager)
    {
        _waveController = waveController;
        _gameFlowController = gameFlowController;
        _effectManager = effectManager;
    }

    private void Awake()
    {
        if (!ValidateSettings())
        {
            return;
        }

        InitializeWallet();
    }

    private void OnDestroy()
    {
        _wallet = null;

        _effectManager = null;
        _waveController = null;
        _gameFlowController = null;
    }

    public int GetBalance(CurrencyType type)
    {
        if (_wallet == null)
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
        if (!TryGetCurrency(type, out CurrencyData currency) || !CanUseCurrency(currency))
        {
            return false;
        }

        return _wallet.CanSpend(new CurrencyAmount(currency, amount));
    }

    public bool TrySpend(CurrencyType type, int amount)
    {
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

    public bool TryAdd(CurrencyType type, int amount)
    {
        if (!TryGetCurrency(type, out CurrencyData currency))
        {
            return false;
        }

        return TryAddInternal(new CurrencyAmount(currency, amount));
    }

    private bool TryAddInternal(CurrencyAmount currencyAmount)
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

    // 종료한 전투의 웨이브·분기 번호를 변경하기 전에 호출
    public UniTask TryApplyReward()
    {
        // 추후 구조체로 변경 예정 부분 (현재는 waveController의 웨이브·분기 번호 검증용)
        if (_waveController == null || _waveController.CurQuarter < 1 ||
            _waveController.CurWave < 1 || _waveController.CurWave > WaveController.MAX_WAVE)
        {
            Debug.LogError("[Economy/PersistentCurrencyManager] 정산할 웨이브 위치를 확인하세요.", this);
            return UniTask.CompletedTask;
        }

        // 규성님이 게임 플로우에서 제공할 구조체로 교체. 현재는 waveController의 프로퍼티들 사용
        int totalWaveCleared = (_waveController.CurQuarter - 1) * WaveController.MAX_WAVE
            + _waveController.CurWave;
        int totalBossesCleared = totalWaveCleared / WaveController.MAX_WAVE;

        if (_currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
        {
            Debug.LogError("[Economy/PersistentCurrencyManager] 혈석 재화 설정을 확인하세요.", this);
            return UniTask.CompletedTask;
        }

        CurrencyAmount reward = _rewardCalculator.CalculateRunSettlementReward(
            currency, totalWaveCleared, totalBossesCleared,
            _effectManager != null ? _effectManager.CurrencyModifiers : null);

        // 정산 UI를 Open하는 함수를 호출해야하는 부분. 
        // 계산된 reward와 totalWaveCleared, totalBossesCleared 등 전체 항목 전달해야함
        // Figma UI 기준으로는 토템 내역, 진행도, 아티팩트 내역(희귀도 기준), 계산식 전달해야함

        // 실제 지급 부분 (TryAddInternal)
        if (!TryAddInternal(reward))
        {
            Debug.LogError("[Economy/PersistentCurrencyManager] 혈석 정산 지급에 실패했습니다.", this);
            return UniTask.CompletedTask;
        }

        // UI쪽과 연결 시 async UniTask로 변경하고 종료 버튼 클릭까지 await
        // UI 종료 버튼 대기
        // 예시 UI 함수 호출
        // await _settlementUI.WaitForCloseAsync(...);

        // UI 닫기 및 정리 후 완료 (현재는 UI 연결이 되지 않은 상태이기에 지급 직후 완료하도록)
        return UniTask.CompletedTask;
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

    private bool TryGetCurrency(CurrencyType type, out CurrencyData currency)
    {
        currency = null;

        if (!ValidateSettings())
        {
            return false;
        }

        if (!_currencyCatalog.TryGetByType(type, out currency))
        {
            Debug.LogError($"[Economy/PersistentCurrencyManager] Catalog에서 재화를 찾을 수 없습니다. Type: {type}", this);
            return false;
        }

        return true;
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
