// Current date KDH 2026-09-11
// 모듈은 자체 Update를 쓰지 않습니다. 웨이브 이벤트나 Building의 Setup만 타면
// 건물 하나당 Update가 늘어나지 않습니다.
namespace OZGL.KDH
{
    public interface IBuildingModule
    {
        void Setup(Building owner);
        void Teardown();
    }
}
