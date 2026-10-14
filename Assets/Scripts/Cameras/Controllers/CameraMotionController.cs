using UnityEngine;

namespace Game.Cameras
{
    internal sealed class CameraMotionController
    {
        // ============================================================
        // Runtime State
        // ============================================================

        private float _elapsed;

        private Vector3 _startPosition;
        private float _startSize;

        // ============================================================
        // Automatic Movement
        // ============================================================

        /// <summary>
        /// 현재 CameraArea의 View를 시작점으로 저장한다.
        /// </summary>
        public void BeginTransition(
            CameraAreaState area)
        {
            _elapsed = 0f;

            _startPosition =
                area.Camera.transform.position;

            _startSize =
                area.Camera.Lens.OrthographicSize;
        }

        /// <summary>
        /// 저장된 시작 View에서 목표 View까지 부드럽게 이동한다.
        /// 위치와 Orthographic Size를 함께 보간한다.
        /// </summary>
        public bool UpdateTransition(
            CameraAreaState area,
            Vector3 destination,
            float size,
            float duration,
            float deltaTime)
        {
            _elapsed += deltaTime;

            float t =
                duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        _elapsed / duration);

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            Vector3 position =
                Vector3.Lerp(
                    _startPosition,
                    destination,
                    eased);

            float orthographicSize =
                Mathf.Lerp(
                    _startSize,
                    size,
                    eased);

            SetView(
                area,
                position,
                orthographicSize);

            return t >= 1f;
        }

        /// <summary>
        /// CameraArea의 위치와 Orthographic Size를 즉시 적용한다.
        /// </summary>
        public void SetView(
            CameraAreaState area,
            Vector3 position,
            float size)
        {
            area.Camera.transform.position =
                position;

            area.Camera.Lens.OrthographicSize =
                size;
        }

        // ============================================================
        // Manual Movement
        // ============================================================

        /// <summary>
        /// 전투 단계에서 사용하는 수동 카메라 이동/줌.
        /// </summary>
        public void Move(
            CameraAreaState area,
            Rect bounds,
            float aspect,
            Vector2 direction,
            float zoomDelta,
            float moveSpeed,
            float zoomSensitivity,
            float zoomResponse,
            float deltaTime)
        {
            if (zoomDelta != 0f)
            {
                area.Zoom =
                    Mathf.Clamp(
                        area.Zoom *
                        Mathf.Exp(
                            zoomDelta *
                            zoomSensitivity),
                        0.5f,
                        2f);
            }

            float size =
                Mathf.Lerp(
                    area.Camera.Lens.OrthographicSize,
                    CameraBoundsUtility.ManualSize(
                        area,
                        bounds,
                        aspect),
                    1f -
                    Mathf.Exp(
                        -Mathf.Max(
                            0.01f,
                            zoomResponse) *
                        deltaTime));

            size =
                Mathf.Min(
                    size,
                    CameraBoundsUtility.MaximumSize(
                        bounds,
                        aspect));

            Vector3 movement =
                (Vector3)Vector2.ClampMagnitude(
                    direction,
                    1f)
                *
                (
                    moveSpeed *
                    size /
                    area.DefaultSize *
                    deltaTime
                );

            Vector3 position =
                CameraBoundsUtility.ClampPosition(
                    area.Camera.transform.position + movement,
                    bounds,
                    size,
                    aspect);

            SetView(
                area,
                position,
                size);
        }
    }
}