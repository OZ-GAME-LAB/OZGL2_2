// Current date KDH 2026-09-16
// 제단 전용 트리거 종류입니다. 기존 CurrencyRewardType을 수정하지 않고
// 시작 지급·이자처럼 아직 재화 시스템이 다루지 않는 타이밍만 여기서 확장합니다.

/// <summary>제단 효과가 발동하는 시점입니다.</summary>
public enum AltarTriggerMoment
{
    None = 0,
    // Run 시작(효과 적용 시점)에 1회 지급합니다. 예: 시작 Gold +50
    OnRunStart = 1,
    // 웨이브 승리 후 Reward 페이즈 진입 시 1회 지급합니다. 예: 보유 Gold 10% 이자
    OnWaveCleared = 2
}

/// <summary>제단 트리거 효과의 수량 계산 방식입니다.</summary>
public enum AltarTriggerCalc
{
    None = 0,
    // Value만큼 고정 지급합니다. 시작 Gold +50이면 Value = 50
    FlatGrant = 1,
    // 현재 잔액 * Value입니다. 10% 이자면 Value = 0.1
    PercentOfBalance = 2
}
