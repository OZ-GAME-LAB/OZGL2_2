using System;
using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

/// <summary>
/// 화면 설정과 BGM/SFX 음량을 적용하고 저장한다.
/// <br/>Bootstrap에서 SaveManager를 전달받아 초기화하며,
/// 저장된 설정을 현재 환경에서 사용할 수 있는 값으로 보정하여 적용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSettingsManager : MonoBehaviour
{
    // ============================================================
    // Save Key / Resolution Options
    // ============================================================

    public static readonly SaveKey<GameSettingsData> SaveKey =
        new SaveKey<GameSettingsData>("game-settings");

    /// <summary>
    /// 창 모드에서 사용할 수 있는 16:9 해상도.
    /// 주사율이 달라도 너비와 높이가 같으면 같은 해상도로 취급한다.
    /// </summary>
    public readonly struct ResolutionOption
    {
        public int Width { get; }
        public int Height { get; }

        public ResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return $"{Width} x {Height}";
        }
    }

    private static readonly ResolutionOption[] StandardResolutions =
    {
        new ResolutionOption(960, 540),
        new ResolutionOption(1280, 720),
        new ResolutionOption(1600, 900),
        new ResolutionOption(1920, 1080),
        new ResolutionOption(2560, 1440),
        new ResolutionOption(3200, 1800),
        new ResolutionOption(3840, 2160)
    };

    // ============================================================
    // References / Runtime State
    // ============================================================

    private readonly List<ResolutionOption> _resolutions =
        new List<ResolutionOption>();

    private SaveManager _saveManager;
    private GameSettingsData _settings;
    private bool _backupBeforeSave;

    // ============================================================
    // Properties / Events
    // ============================================================

    public bool IsInitialized { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    public string LastError { get; private set; }

    /// <summary>
    /// 창 모드에서 선택 가능한 해상도 목록.
    /// 전체 화면은 이 목록에 포함되지 않으며 UI에서 0번 항목으로 별도 표시한다.
    /// </summary>
    public IReadOnlyList<ResolutionOption> AvailableResolutions =>
        _resolutions.AsReadOnly();

    public bool Fullscreen => _settings?.Fullscreen ?? true;

    public int ResolutionWidth =>
        _settings?.ResolutionWidth ?? 1920;

    public int ResolutionHeight =>
        _settings?.ResolutionHeight ?? 1080;

    public float BGMVolume =>
        _settings?.BGMVolume ?? 1f;

    public float SFXVolume =>
        _settings?.SFXVolume ?? 1f;

    public bool BGMMuted =>
        _settings?.BGMMuted ?? false;

    public bool SFXMuted =>
        _settings?.SFXMuted ?? false;

    /// <summary>
    /// 창 모드에서 현재 저장된 해상도의 인덱스.
    /// 목록에 존재하지 않으면 -1.
    /// </summary>
    public int SelectedResolutionIndex =>
        FindResolution(ResolutionWidth, ResolutionHeight);

    public event Action Changed;

    // ============================================================
    // Initialize
    // ============================================================

    /// <summary>
    /// 저장된 설정을 불러오고 현재 환경에서 사용할 수 있는 값으로
    /// 보정하여 적용한다.
    /// </summary>
    public void Initialize(SaveManager saveManager)
    {
        if (saveManager == null)
            throw new ArgumentNullException(nameof(saveManager));

        if (IsInitialized)
        {
            if (_saveManager != saveManager)
            {
                throw new InvalidOperationException(
                    "설정 초기화 후 SaveManager를 교체할 수 없습니다.");
            }

            return;
        }

        _saveManager = saveManager;

        BuildResolutionOptions();

        bool exists = saveManager.HasSaveFile(SaveKey);
        bool loaded = false;
        string error = null;

        if (exists)
            loaded = saveManager.TryLoad(SaveKey, out _settings, out error);

        // 파일이 없거나 복원에 실패하면 기본값으로 시작한다.
        // 손상된 파일은 사용자가 저장할 때 원본을 백업한 뒤 교체한다.
        if (!loaded)
            _settings = new GameSettingsData();

        _backupBeforeSave = exists && !loaded;
        HasUnsavedChanges = !loaded;

        if (_backupBeforeSave)
        {
            LastError = error;

            Debug.LogWarning(
                "[GameSettingsManager] 설정 복원 실패. 기본값을 적용합니다: " + error,
                this);
        }

        NormalizeLoadedSettings();

        IsInitialized = true;

        ApplySettings();

        Changed?.Invoke();
    }

    /// <summary>
    /// 저장 파일에서 읽은 값을 현재 실행 환경에서 사용할 수 있도록 보정한다.
    /// </summary>
    private void NormalizeLoadedSettings()
    {
        float bgm = NormalizeVolume(_settings.BGMVolume);
        float sfx = NormalizeVolume(_settings.SFXVolume);

        HasUnsavedChanges |=
            bgm != _settings.BGMVolume ||
            sfx != _settings.SFXVolume;

        _settings.BGMVolume = bgm;
        _settings.SFXVolume = sfx;

        // 전체 화면에서는 저장된 창 해상도가 현재 목록에 없어도 문제없다.
        if (_settings.Fullscreen)
            return;

        if (FindResolution(
                _settings.ResolutionWidth,
                _settings.ResolutionHeight) >= 0)
        {
            return;
        }

        ResolutionOption fallback = GetDefaultWindowedResolution();

        _settings.ResolutionWidth = fallback.Width;
        _settings.ResolutionHeight = fallback.Height;

        HasUnsavedChanges = true;
    }

    // ============================================================
    // Volume
    // ============================================================

    /// <summary>
    /// BGM 음량을 0~1 범위로 적용한다.
    /// 파일 저장은 조작 완료 시 별도로 요청한다.
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        RequireInitialized();

        volume = NormalizeVolume(volume);

        if (_settings.BGMVolume == volume)
            return;

        _settings.BGMVolume = volume;

        AudioVolumeSettings.SetBGMVolume(
            BGMMuted ? 0f : volume);

        MarkChanged();
    }

    /// <summary>
    /// SFX 음량을 0~1 범위로 적용한다.
    /// BGM 음량은 변경하지 않는다.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        RequireInitialized();

        volume = NormalizeVolume(volume);

        if (_settings.SFXVolume == volume)
            return;

        _settings.SFXVolume = volume;

        AudioVolumeSettings.SetSFXVolume(
            SFXMuted ? 0f : volume);

        MarkChanged();
    }

    /// <summary>
    /// BGM 음소거를 즉시 적용한다.
    /// 슬라이더에서 지정한 음량은 유지한다.
    /// </summary>
    public void SetBGMMuted(bool muted)
    {
        RequireInitialized();

        if (_settings.BGMMuted == muted)
            return;

        _settings.BGMMuted = muted;

        AudioVolumeSettings.SetBGMVolume(
            muted ? 0f : BGMVolume);

        MarkChanged();
    }

    /// <summary>
    /// SFX 음소거를 즉시 적용한다.
    /// 해제하면 현재 슬라이더 음량으로 복원한다.
    /// </summary>
    public void SetSFXMuted(bool muted)
    {
        RequireInitialized();

        if (_settings.SFXMuted == muted)
            return;

        _settings.SFXMuted = muted;

        AudioVolumeSettings.SetSFXVolume(
            muted ? 0f : SFXVolume);

        MarkChanged();
    }

    // ============================================================
    // Display
    // ============================================================

    /// <summary>
    /// 현재 디스플레이에서 사용할 수 있는 가장 큰 16:9 해상도로
    /// 전체 화면을 적용한다.
    /// </summary>
    public void SetFullscreen()
    {
        RequireInitialized();

        bool changed = !_settings.Fullscreen;

        _settings.Fullscreen = true;

        ApplyDisplaySettings();

        if (changed)
            MarkChanged();
    }

    /// <summary>
    /// AvailableResolutions의 인덱스로 창 모드 해상도를 변경한다.
    /// </summary>
    public void SetWindowedResolutionIndex(int index)
    {
        RequireInitialized();

        if (index < 0 || index >= _resolutions.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        ResolutionOption option = _resolutions[index];

        SetWindowedResolution(
            option.Width,
            option.Height);
    }

    /// <summary>
    /// 지원 목록에 있는 16:9 해상도로 창 모드를 적용한다.
    /// </summary>
    public void SetWindowedResolution(int width, int height)
    {
        RequireInitialized();

        if (FindResolution(width, height) < 0)
        {
            throw new ArgumentException(
                "AvailableResolutions의 16:9 창 모드 해상도만 사용할 수 있습니다.");
        }

        bool changed =
            _settings.Fullscreen ||
            _settings.ResolutionWidth != width ||
            _settings.ResolutionHeight != height;

        if (!changed)
            return;

        _settings.Fullscreen = false;
        _settings.ResolutionWidth = width;
        _settings.ResolutionHeight = height;

        ApplyDisplaySettings();

        MarkChanged();
    }

    // ============================================================
    // Apply / Save
    // ============================================================

    /// <summary>
    /// 현재 화면 설정과 음량을 적용한다.
    /// 저장 파일은 변경하지 않는다.
    /// </summary>
    public void ApplySettings()
    {
        RequireInitialized();

        AudioVolumeSettings.SetBGMVolume(
            BGMMuted ? 0f : BGMVolume);

        AudioVolumeSettings.SetSFXVolume(
            SFXMuted ? 0f : SFXVolume);

        ApplyDisplaySettings();
    }

    /// <summary>
    /// UI의 저장/닫기 버튼이나 슬라이더 조작 완료 시 호출하며,
    /// 저장 실패는 로그로 남긴다.
    /// </summary>
    public void SaveSettings()
    {
        if (!TrySave(out string error))
        {
            Debug.LogError(
                "[GameSettingsManager] " + error,
                this);
        }
    }

    /// <summary>
    /// 현재 설정을 파일에 저장한다.
    /// 실패하면 변경 상태를 유지하고 오류를 반환한다.
    /// </summary>
    public bool TrySave(out string error)
    {
        RequireInitialized();

        error = null;

        if (_backupBeforeSave)
        {
            if (!_saveManager.TryBackup(
                    SaveKey,
                    out _,
                    out error))
            {
                LastError = error;
                return false;
            }

            _backupBeforeSave = false;
        }

        if (!_saveManager.TrySave(
                SaveKey,
                _settings,
                out error))
        {
            LastError = error;
            return false;
        }

        LastError = null;
        HasUnsavedChanges = false;

        return true;
    }

    // ============================================================
    // Display Apply
    // ============================================================

    /// <summary>
    /// 전체 화면이면 현재 디스플레이에서 가장 적합한 16:9 해상도를 사용하고,
    /// 창 모드이면 저장된 해상도를 사용한다.
    /// </summary>
    private void ApplyDisplaySettings()
    {
        if (_settings.Fullscreen)
        {
            ResolutionOption resolution =
                GetBestFullscreenResolution();

            Screen.SetResolution(
                resolution.Width,
                resolution.Height,
                FullScreenMode.FullScreenWindow);

            return;
        }

        Screen.SetResolution(
            ResolutionWidth,
            ResolutionHeight,
            FullScreenMode.Windowed);
    }

    // ============================================================
    // Resolution Helpers
    // ============================================================

    /// <summary>
    /// 현재 디스플레이에서 사용할 수 있는 가장 큰 16:9 해상도를 찾는다.
    /// Screen.resolutions에 적합한 항목이 없으면 현재 화면 높이를 기준으로
    /// 16:9 크기를 계산한다.
    /// </summary>
    private ResolutionOption GetBestFullscreenResolution()
    {
        int maxWidth = Screen.currentResolution.width;
        int maxHeight = Screen.currentResolution.height;

        ResolutionOption best = default;
        long bestPixels = 0;

        foreach (Resolution resolution in Screen.resolutions)
        {
            int width = resolution.width;
            int height = resolution.height;

            if (!IsSixteenByNine(width, height))
                continue;

            if (width > maxWidth || height > maxHeight)
                continue;

            long pixels = (long)width * height;

            if (pixels <= bestPixels)
                continue;

            best = new ResolutionOption(width, height);
            bestPixels = pixels;
        }

        // 실제 지원 목록에서 16:9를 찾지 못한 경우
        // 현재 디스플레이 안에 들어가는 가장 큰 16:9 영역을 계산한다.
        if (bestPixels == 0)
        {
            float currentAspect =
                (float)maxWidth / maxHeight;

            float targetAspect = 16f / 9f;

            int width;
            int height;

            if (currentAspect >= targetAspect)
            {
                height = maxHeight;
                width = Mathf.FloorToInt(height * targetAspect);
            }
            else
            {
                width = maxWidth;
                height = Mathf.FloorToInt(width / targetAspect);
            }

            // 홀수 해상도로 인한 불필요한 오차를 줄인다.
            width -= width % 2;
            height -= height % 2;

            best = new ResolutionOption(width, height);
        }

        return best;
    }

    /// <summary>
    /// 창 모드에서 선택 가능한 16:9 해상도를 구성한다.
    /// 현재 디스플레이 전체 크기와 동일한 해상도는 제외하여
    /// 제목 표시줄이나 창 테두리로 화면이 잘리는 상황을 방지한다.
    /// </summary>
    private void BuildResolutionOptions()
    {
        _resolutions.Clear();

        int displayWidth = Display.main.systemWidth;
        int displayHeight = Display.main.systemHeight;

        foreach (ResolutionOption option in StandardResolutions)
        {
            // 현재 모니터보다 큰 해상도는 제외한다.
            if (option.Width > displayWidth ||
                option.Height > displayHeight)
            {
                continue;
            }

            // 모니터와 동일한 크기는 전체 화면용이므로
            // 창 모드 목록에서는 제외한다.
            if (option.Width == displayWidth &&
                option.Height == displayHeight)
            {
                continue;
            }

            AddResolution(
                option.Width,
                option.Height);
        }

        if (_resolutions.Count == 0)
        {
            AddResolution(960, 540);
        }

        _resolutions.Sort(
            (a, b) => a.Width.CompareTo(b.Width));
    }

    /// <summary>
    /// 16:9 비율이면서 아직 등록되지 않은 해상도만 추가한다.
    /// </summary>
    private void AddResolution(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        if (!IsSixteenByNine(width, height))
            return;

        if (FindResolution(width, height) >= 0)
            return;

        _resolutions.Add(
            new ResolutionOption(width, height));
    }

    /// <summary>
    /// 너비와 높이가 같은 항목의 인덱스를 반환하며,
    /// 목록에 없으면 -1을 반환한다.
    /// </summary>
    private int FindResolution(int width, int height)
    {
        for (int i = 0; i < _resolutions.Count; i++)
        {
            if (_resolutions[i].Width == width &&
                _resolutions[i].Height == height)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 저장된 창 해상도를 사용할 수 없을 때 사용할 기본값을 반환한다.
    /// 가능한 항목 중 1920x1080 이하의 가장 큰 해상도를 우선한다.
    /// </summary>
    private ResolutionOption GetDefaultWindowedResolution()
    {
        if (_resolutions.Count == 0)
            return new ResolutionOption(1280, 720);

        ResolutionOption result =
            _resolutions[0];

        for (int i = 0; i < _resolutions.Count; i++)
        {
            ResolutionOption option =
                _resolutions[i];

            if (option.Width > 1920 ||
                option.Height > 1080)
            {
                break;
            }

            result = option;
        }

        return result;
    }

    private static bool IsSixteenByNine(
        int width,
        int height)
    {
        return width > 0 &&
               height > 0 &&
               (long)width * 9 ==
               (long)height * 16;
    }

    // ============================================================
    // Validation / Change Notification
    // ============================================================

    /// <summary>
    /// 잘못된 수치는 기본 음량으로,
    /// 범위를 벗어난 값은 0~1로 보정한다.
    /// </summary>
    private static float NormalizeVolume(float value)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value))
        {
            return 1f;
        }

        return Mathf.Clamp01(value);
    }

    /// <summary>
    /// UI에서 Bootstrap 초기화 전에 설정을 변경하지 못하게 한다.
    /// </summary>
    private void RequireInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "Bootstrap에서 Initialize를 먼저 호출하세요.");
        }
    }

    /// <summary>
    /// 적용된 값과 저장된 값이 달라졌음을 기록하고
    /// UI에 갱신을 알린다.
    /// </summary>
    private void MarkChanged()
    {
        HasUnsavedChanges = true;

        Changed?.Invoke();
    }
}