using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Cameras
{
    internal sealed class CameraInputReader
    {
        // ============================================================
        // Input
        // ============================================================

        public void Read(Camera worldCamera, bool edgeScrollEnabled, float edgeWidth, float edgeScreenRatio,
            out Vector2 movement, out float zoom)
        {
            Vector2 direction = Vector2.zero;
            float zoomDelta = 0f;
            Keyboard keyboard = Keyboard.current;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool typing = selected != null && (selected.GetComponentInParent<TMP_InputField>() != null
                || selected.GetComponentInParent<InputField>() != null);
            if (keyboard != null && !typing)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y += 1f;
            }

            Mouse mouse = Mouse.current;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            // 일반 UI 위에서도 가장자리 이동을 허용한다. 카메라 상태 잠금은 상위 컨트롤러가 담당한다.
            if (mouse != null && !typing)
            {
                Vector2 p = mouse.position.ReadValue();
                Rect viewport = worldCamera != null ? worldCamera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
                if (viewport.Contains(p))
                {
                    if (edgeScrollEnabled)
                    {
                        // 해상도가 높아도 감지 영역이 지나치게 좁아지지 않도록 비율 기준을 함께 사용한다.
                        float shortSide = Mathf.Min(viewport.width, viewport.height);
                        float edge = Mathf.Clamp(Mathf.Max(edgeWidth, shortSide * edgeScreenRatio), 0f, shortSide * 0.25f);
                        if (p.x < viewport.xMin + edge) direction.x -= 1f;
                        if (p.x > viewport.xMax - edge) direction.x += 1f;
                        if (p.y < viewport.yMin + edge) direction.y -= 1f;
                        if (p.y > viewport.yMax - edge) direction.y += 1f;
                    }
                    float scroll = mouse.scroll.ReadValue().y;
                    // UI 스크롤과 카메라 줌이 동시에 실행되지 않도록 휠만 UI 위에서 차단한다.
                    // 휠 장치별 단위 차이는 방향으로 정규화한다.
                    if (!overUI && Mathf.Abs(scroll) > 0.01f)
                        zoomDelta = Mathf.Sign(scroll);
                }
            }

            movement = Vector2.ClampMagnitude(direction, 1f);
            zoom = zoomDelta;
        }
    }
}
