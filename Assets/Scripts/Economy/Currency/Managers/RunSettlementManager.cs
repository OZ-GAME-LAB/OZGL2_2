using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI;
using UnityEngine;

// 보상을 파일에 확정한 다음 같은 기록과 지급액을 UI에 전달합니다.
public class RunSettlementManager : MonoBehaviour, IRunSettlementRewards
{
    [SerializeField] private CurrencyCatalog _currencyCatalog;

    private PersistentCurrencyManager _persistentWallet;
    private EffectManager _effectManager;
    private TotemRunApplier _totemRunApplier;
    private IRunSettlementUI _settlementUI;
    private IPersistentSaveWriter _saveWriter;
    private bool _isSettling;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    // UI가 취소되어도 이미 저장한 보상은 유지합니다. 같은 기록을 다시 보여줄 때 재지급하지 않습니다.
    private readonly Dictionary<RunSummary, int> _savedRewards = new Dictionary<RunSummary, int>();

    public void Initialize(WaveController waveController, EffectManager effectManager,
        PersistentCurrencyManager persistentWallet, TotemRunApplier totemRunApplier,
        IRunSettlementUI settlementUI = null, IPersistentSaveWriter saveWriter = null)
    {
        if (_isSettling)
            throw new InvalidOperationException("정산 대기 중에는 RunSettlementManager를 다시 초기화할 수 없습니다.");
        if (_savedRewards.Count > 0 &&
            (!ReferenceEquals(_persistentWallet, persistentWallet) || !ReferenceEquals(_saveWriter, saveWriter)))
            throw new InvalidOperationException("지급 기록이 있는 정산 매니저의 지갑이나 저장 대상을 변경할 수 없습니다.");

        _effectManager = effectManager;
        _persistentWallet = persistentWallet;
        _totemRunApplier = totemRunApplier;
        _settlementUI = settlementUI;
        _saveWriter = saveWriter;
    }

    // true는 저장과 메인 버튼 확인이 모두 끝났다는 뜻입니다. 취소는 호출자에게 전달합니다.
    public async UniTask<bool> TryApplyReward(RunSummary summary, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (_isSettling) return Fail("이미 정산을 진행하고 있습니다.");
        if (!CheckReady(summary)) return false;

        _isSettling = true;
        try
        {
            if (!_savedRewards.TryGetValue(summary, out int finalBloodstones))
            {
                if (!TryCalculateReward(summary, out finalBloodstones)) return false;
                token.ThrowIfCancellationRequested();
                if (!TrySaveReward(summary, finalBloodstones)) return false;
            }

            token.ThrowIfCancellationRequested();
            await _settlementUI.ShowAndWaitAsync(summary, finalBloodstones, token);
            token.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Fail("정산을 완료하지 못했습니다. " + exception.Message);
        }
        finally
        {
            _isSettling = false;
        }
    }

    private bool CheckReady(RunSummary summary)
    {
        if (summary == null || summary.ClearWaveCount < 0 || summary.ClearBossCount < 0 ||
            summary.ClearBossCount > summary.ClearWaveCount)
            return Fail("정산 기록의 웨이브·보스 클리어 횟수를 확인하세요.");
        if (_settlementUI == null || _saveWriter == null || !_saveWriter.IsReady ||
            _persistentWallet == null || !_persistentWallet.IsInitialized)
            return Fail("정산 UI, 영구 저장과 혈석 지갑을 먼저 초기화해주세요.");
        return true;
    }

    private bool TryCalculateReward(RunSummary summary, out int amount)
    {
        amount = 0;
        if (_currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
            return Fail("혈석 재화 설정을 확인하세요.");

        CurrencyAmount reward = _rewardCalculator.CalculateRunSettlementReward(
            currency, summary.ClearWaveCount, summary.ClearBossCount,
            _effectManager != null ? _effectManager.CurrencyModifiers : null);
        if (reward.Amount < 0) return Fail("기본 정산 보상을 계산하지 못했습니다.");

        // CalculateBonusBloodstone은 기본 보상을 포함하지 않는 추가분입니다.
        int bonus = _totemRunApplier != null ? _totemRunApplier.CalculateBonusBloodstone(reward.Amount) : 0;
        long total = (long)reward.Amount + bonus;
        if (bonus < 0 || total > int.MaxValue) return Fail("정산 보상이 허용 범위를 초과했습니다.");
        amount = (int)total;
        return true;
    }

    private bool TrySaveReward(RunSummary summary, int amount)
    {
        PersistentSaveData next = _saveWriter.CaptureSaveData();
        if (next == null || next.Traits == null)
            return Fail("아웃게임 전체 저장 데이터를 가져오지 못했습니다.");
        if (!_persistentWallet.TryValidateSaveData(next.Wallet, out string error))
            return Fail(error);
        if (next.Wallet.Amount != _persistentWallet.GetBalance(CurrencyType.Bloodstone))
            return Fail("저장할 혈석 잔액과 현재 지갑 잔액이 다릅니다.");
        if (amount > int.MaxValue - next.Wallet.Amount)
            return Fail("혈석 잔액이 허용 범위를 초과했습니다.");

        // 특성 등 기존 아웃게임 데이터는 보존하고 혈석만 바꿉니다.
        next.Wallet = new PersistentWalletSaveData(next.Wallet.Amount + amount);
        if (!_saveWriter.TrySave(next, out error)) return Fail("혈석 저장에 실패했습니다. " + error);

        // 파일 확정 뒤 먼저 지급 기록을 남깁니다. UI/알림에서 오류가 나도 재지급하지 않습니다.
        _savedRewards.Add(summary, amount);
        _persistentWallet.RestoreSaveData(next.Wallet, false);
        _persistentWallet.NotifyChanged();
        return true;
    }

    private bool Fail(string message)
    {
        Debug.LogError("[Economy/RunSettlementManager] " + message, this);
        return false;
    }
}
