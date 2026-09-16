// Current date KDH 2026-09-08
// 건설 메뉴 필터용. 본진은 슬롯에서 짓는 목록에서 제외하기 쉽습니다.
namespace OZGL.KDH
{
    public enum BuildingType
    {
        Core = 0,      // 본진. 미리 배치하고, 플레이어가 짓지 않음
        Producer = 1,  // 자원 건물
        Barracks = 2,  // 아군 소환
        Support = 3,    // 지원 건물
        Tower = 4,     // 공격 타워
    }
}
