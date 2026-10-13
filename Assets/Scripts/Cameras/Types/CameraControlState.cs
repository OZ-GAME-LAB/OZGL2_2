namespace Game.Cameras
{
    // 현재 구역(FocusField)과 별개로 카메라 입력 및 연출 상태를 나타낸다.
    public enum CameraControlState
    {
        Locked,
        BuildControl,
        BuildingFocus,
        BattleOverview,
        ReturningToBattle,
        BattleControl
    }
}
