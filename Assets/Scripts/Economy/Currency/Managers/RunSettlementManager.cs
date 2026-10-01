using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

// 인게임 종료 보상 계산과 정산 UI 흐름 담당
public class RunSettlementManager : MonoBehaviour, IRunSettlementRewards
{
    [SerializeField] private CurrencyCatalog _currencyCatalog;

    private PersistentCurrencyManager _persistentWallet;
    private EffectManager _effectManager;
    private TotemRunApplier _totemRunApplier;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    public void Initialize(WaveController waveController, EffectManager effectManager,
        PersistentCurrencyManager persistentWallet, TotemRunApplier totemRunApplier)
    {
        _effectManager = effectManager;
        _persistentWallet = persistentWallet;
        _totemRunApplier = totemRunApplier;
    }

    // 게임 종료 시 확정된 기록으로 보상을 계산합니다.
    public UniTask TryApplyReward(RunSummary summary)
    {
        if (summary == null || summary.ClearWaveCount < 0 || summary.ClearBossCount < 0 ||
            summary.ClearBossCount > summary.ClearWaveCount)
        {
            Debug.LogError("[Economy/RunSettlementManager] 정산 기록의 웨이브·보스 클리어 횟수를 확인하세요.", this);
            return UniTask.CompletedTask;
        }

        int totalWaveCleared = summary.ClearWaveCount;
        int totalBossesCleared = summary.ClearBossCount;

        if (_currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
        {
            Debug.LogError("[Economy/RunSettlementManager] 혈석 재화 설정을 확인하세요.", this);
            return UniTask.CompletedTask;
        }


        CurrencyAmount reward = _rewardCalculator.CalculateRunSettlementReward(
            currency, totalWaveCleared, totalBossesCleared,
            _effectManager != null ? _effectManager.CurrencyModifiers : null);

        // 계산식 이후 토템 및 제단 적용
        int finalAmouont = _totemRunApplier.CalculateBonusBloodstone(reward.Amount);

        // 정산 UI를 Open하는 함수를 호출해야하는 부분. 
        // 계산된 reward와 summary(진행도·처치 수·등급별 아티팩트 기록)를 전달해야함
        // Figma UI 기준으로는 토템 내역, 진행도, 아티팩트 내역(희귀도 기준), 계산식 전달해야함

        // 정산 부분
        if (_persistentWallet == null || !_persistentWallet.TryAdd(currency.Type, finalAmouont))
        {
            Debug.LogError("[Economy/RunSettlementManager] 혈석 정산 보상 지급에 실패했습니다. 지갑 초기화 상태와 보상 금액을 확인하세요.", this);
            return UniTask.CompletedTask;
        }

        // UI쪽과 연결 시 async UniTask로 변경하고 종료 버튼 클릭까지 await
        // UI 종료 버튼 대기
        // 예시 UI 함수 호출
        // await _settlementUI.WaitForCloseAsync(...);

        

        // UI 닫기 및 정리 후 완료 (현재는 UI 연결 대기 중)
        return UniTask.CompletedTask;
    }

}
