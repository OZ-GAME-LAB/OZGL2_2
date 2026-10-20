using UnityEngine;

namespace Game.Cameras
{
    internal static class CameraBoundsUtility
    {
        // ============================================================
        // Bounds / Zoom
        // ============================================================

        public static float MaximumSize(Rect bounds, float aspect) => Mathf.Max(0.01f, Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect)));

        public static float ManualSize(CameraAreaState area, Rect bounds, float aspect) => Mathf.Min(area.DefaultSize / Mathf.Clamp(area.Zoom, 0.5f, 2f), MaximumSize(bounds, aspect));

        public static float OverviewSize(Rect bounds, float aspect, float padding) => Mathf.Max(bounds.height * 0.5f, bounds.width / (2f * aspect)) + Mathf.Max(0f, padding);

        public static Vector3 ClampPosition(Vector3 position, Rect bounds, float size, float aspect)
        {
            float halfWidth = size * aspect;
            position.x = bounds.width <= halfWidth * 2f ? bounds.center.x : Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            position.y = bounds.height <= size * 2f ? bounds.center.y : Mathf.Clamp(position.y, bounds.yMin + size, bounds.yMax - size);
            return position;
        }

    }
}
