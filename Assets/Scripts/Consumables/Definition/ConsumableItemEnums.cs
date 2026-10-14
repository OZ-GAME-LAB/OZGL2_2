// 플레이어 기준으로 효과를 적용할 팀
public enum ConsumableTargetTeam
{
    Ally = 0,
    Enemy = 1,
    All = 2
}

public enum ConsumableTargetMode
{
    Area = 1,   // 선택한 위치의 반경 내 유닛
    All = 2     // 대상 팀에 해당하는 전체 유닛
}
