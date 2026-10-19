using System;

/// <summary>
/// 게임 진행 데이터와 별도로 저장하는 화면 설정과 BGM/SFX 음량 데이터.
/// </summary>
[Serializable]
public sealed class GameSettingsData
{
    // ============================================================
    // Display
    // ============================================================

    // 전체 화면에서는 현재 디스플레이에 적합한 16:9 해상도를 자동으로 사용한다.
    // 창 모드에서는 아래 ResolutionWidth / ResolutionHeight 값을 사용한다.
    public bool Fullscreen = true;

    // 지원 목록이 달라져도 복원할 수 있도록 인덱스 대신 실제 크기를 저장한다.
    public int ResolutionWidth = 1920;
    public int ResolutionHeight = 1080;

    // ============================================================
    // Volume
    // ============================================================

    // 0은 무음, 1은 원래 음량이다.
    public float BGMVolume = 1f;
    public float SFXVolume = 1f;

    // 음소거를 해제했을 때 기존 음량으로 돌아오도록 별도로 저장한다.
    public bool BGMMuted;
    public bool SFXMuted;
}