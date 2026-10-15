using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI;
using UnityEngine;

// 보상을 파일에 확정한 다음 같은 기록과 지급액을 UI에 전달합니다.
public class RunRewardManager : MonoBehaviour, IRunSettlementRewards
{
    [SerializeField] private CurrencyCatalog _currencyCatalog;
    [SerializeField] private RunScoreSettings _settings;
    public RunScoreSettings Settings => _settings;

    private PersistentCurrencyManager _persistentWallet;
    private EffectManager _effectManager;
    private IRunSettlementUI _settlementUI;
    private IPersistentSaveWriter _saveWriter;
    private bool _isSettling;
    private readonly RunRewardCalculator _rewardCalculator = new RunRewardCalculator();

    // 저장 재시도에도 같은 계산을 사용합니다. 결과 확정과 지급 완료는 별개입니다.
    private readonly Dictionary<RunSummary, RunResult> _results = new();
    private readonly HashSet<RunSummary> _savedRewards = new();

    public void Initialize(WaveController waveController, EffectManager effectManager,
        PersistentCurrencyManager persistentWallet, TotemRunApplier totemRunApplier,
        IRunSettlementUI settlementUI = null, IPersistentSaveWriter saveWriter = null)
    {
        if (_isSettling)
            throw new InvalidOperationException("정산 대기 중에는 RunSettlementManager를 다시 초기화할 수 없습니다.");
        if (_results.Count > 0 &&
            (!ReferenceEquals(_persistentWallet, persistentWallet) || !ReferenceEquals(_saveWriter, saveWriter)))
            throw new InvalidOperationException("지급 기록이 있는 정산 매니저의 지갑이나 저장 대상을 변경할 수 없습니다.");

        _effectManager = effectManager;
        _persistentWallet = persistentWallet;
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
            if (!_results.TryGetValue(summary, out RunResult result))
            {
                if (!TryCalculateReward(summary, out result)) return false;
                _results.Add(summary, result);
            }
            token.ThrowIfCancellationRequested();
            if (!_savedRewards.Contains(summary) && !TrySaveReward(summary, result.FinalBloodstones)) return false;

            token.ThrowIfCancellationRequested();
            await _settlementUI.ShowAndWaitAsync(summary, result, token);
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

    private bool TryCalculateReward(RunSummary summary, out RunResult result)
    {
        result = null;
        if (_currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
            return Fail("혈석 재화 설정을 확인하세요.");

        result = _rewardCalculator.Calculate(summary, _settings,
            _effectManager != null ? _effectManager.CurrencyModifiers : null);
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
        _savedRewards.Add(summary);
        _persistentWallet.RestoreSaveData(next.Wallet, false);
        _persistentWallet.NotifyChanged();
        return true;
    }

    private bool Fail(string message)
    {
        Debug.LogError("[Economy/RunRewardManager] " + message, this);
        return false;
    }
}
