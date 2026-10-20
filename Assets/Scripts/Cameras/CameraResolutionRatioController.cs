using UnityEngine;

/// <summary>
/// 현재 화면 해상도와 관계없이 Camera의 출력 영역을 16:9로 유지한다.
/// <br/>16:9보다 넓은 화면은 좌우 여백을,
/// 좁은 화면은 상하 여백을 사용한다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class CameraResolutionRatioController : MonoBehaviour
{
    // ============================================================
    // Constants
    // ============================================================

    private const float TargetAspectRatio = 16f / 9f;

    // ============================================================
    // Runtime State
    // ============================================================

    private Camera _camera;

    private int _lastScreenWidth;
    private int _lastScreenHeight;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        ApplyAspectRatio();
    }

    private void OnEnable()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();

        ApplyAspectRatio();
    }

    private void Update()
    {
        // 창 모드에서 사용자가 창 크기를 변경하거나
        // 설정에서 해상도가 변경된 경우에만 다시 계산한다.
        if (_lastScreenWidth == Screen.width &&
            _lastScreenHeight == Screen.height)
        {
            return;
        }

        ApplyAspectRatio();
    }

    private void OnDisable()
    {
        // 컴포넌트가 비활성화되면 Camera의 전체 영역을 복원한다.
        if (_camera != null)
        {
            _camera.rect =
                new Rect(0f, 0f, 1f, 1f);
        }
    }

    // ============================================================
    // Aspect Ratio
    // ============================================================

    /// <summary>
    /// 현재 화면 비율을 기준으로 Camera viewport를 조정하여
    /// 실제 게임 출력 영역을 항상 16:9로 유지한다.
    /// </summary>
    private void ApplyAspectRatio()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        if (_camera == null ||
            _lastScreenWidth <= 0 ||
            _lastScreenHeight <= 0)
        {
            return;
        }

        float currentAspectRatio =
            (float)_lastScreenWidth /
            _lastScreenHeight;

        float scale =
            currentAspectRatio /
            TargetAspectRatio;

        // --------------------------------------------------------
        // 16:9보다 좁은 화면
        // --------------------------------------------------------

        if (scale < 1f)
        {
            // 화면의 너비를 기준으로 16:9를 맞추기 때문에
            // 위와 아래에 Letterbox 영역이 생긴다.
            float height = scale;
            float y = (1f - height) * 0.5f;

            _camera.rect =
                new Rect(
                    0f,
                    y,
                    1f,
                    height);

            return;
        }

        // --------------------------------------------------------
        // 16:9보다 넓은 화면
        // --------------------------------------------------------

        // 화면의 높이를 기준으로 16:9를 맞추기 때문에
        // 왼쪽과 오른쪽에 Pillarbox 영역이 생긴다.
        float width = 1f / scale;
        float x = (1f - width) * 0.5f;

        _camera.rect =
            new Rect(
                x,
                0f,
                width,
                1f);
    }
}