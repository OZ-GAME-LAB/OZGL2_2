using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

// 인게임 종료 보상 계산과 정산 UI 흐름 담당
public class RunSettlementManager : MonoBehaviour, IRunSettlementRewards
{
    [SerializeField] private CurrencyCatalog _currencyCatalog;

    private PersistentCurrencyManager _persistentWallet;
    private EffectManager _effectManager;
    private WaveController _waveController;
    private readonly CurrencyRewardCalculator _rewardCalculator = new CurrencyRewardCalculator();

    public void Initialize(WaveController waveController, EffectManager effectManager,
        PersistentCurrencyManager persistentWallet)
    {
        _waveController = waveController;
        _effectManager = effectManager;
        _persistentWallet = persistentWallet;
    }

    // 종료한 전투의 웨이브·분기 번호를 변경하기 전에 호출
    public UniTask TryApplyReward()
    {
        // 추후 구조체로 변경 예정 부분 (현재는 waveController의 웨이브·분기 번호 검증용)
        if (_waveController == null || _waveController.CurQuarter < 1 ||
            _waveController.CurWave < 1 || _waveController.CurWave > WaveController.MAX_WAVE)
        {
            Debug.LogError("[Economy/RunSettlementManager] 정산할 웨이브 위치를 확인하세요.", this);
            return UniTask.CompletedTask;
        }

        // 규성님이 게임 플로우에서 제공할 구조체로 교체. 현재는 waveController의 프로퍼티들 사용
        int totalWaveCleared = (_waveController.CurQuarter - 1) * WaveController.MAX_WAVE
            + _waveController.CurWave;
        int totalBossesCleared = totalWaveCleared / WaveController.MAX_WAVE;

        if (_currencyCatalog == null ||
            !_currencyCatalog.TryGetByType(CurrencyType.Bloodstone, out CurrencyData currency))
        {
            Debug.LogError("[Economy/RunSettlementManager] 혈석 재화 설정을 확인하세요.", this);
            return UniTask.CompletedTask;
        }

        CurrencyAmount reward = _rewardCalculator.CalculateRunSettlementReward(
            currency, totalWaveCleared, totalBossesCleared,
            _effectManager != null ? _effectManager.CurrencyModifiers : null);

        // 정산 UI를 Open하는 함수를 호출해야하는 부분. 
        // 계산된 reward와 totalWaveCleared, totalBossesCleared 등 전체 항목 전달해야함
        // Figma UI 기준으로는 토템 내역, 진행도, 아티팩트 내역(희귀도 기준), 계산식 전달해야함

        // 정산 부분
        if (_persistentWallet == null || !_persistentWallet.TryAdd(currency.Type, reward.Amount))
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
