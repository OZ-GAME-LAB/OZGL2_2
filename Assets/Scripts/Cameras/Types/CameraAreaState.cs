using Unity.Cinemachine;
using UnityEngine;

namespace Game.Cameras
{
    internal sealed class CameraAreaState
    {
        // ============================================================
        // Properties / Runtime State
        // ============================================================

        public CinemachineCamera Camera { get; }
        public Transform OriginalFollow { get; }
        public float DefaultSize { get; }
        public Vector3 EntryPosition { get; }
        public float Zoom = 1f;

        public CameraArea Definition { get; }

        // ============================================================
        // Initialize
        // ============================================================

        public CameraAreaState(CinemachineCamera camera, CameraArea definition)
        {
            Definition = definition;
            Camera = camera;
            OriginalFollow = camera.Follow;
            DefaultSize = definition.DefaultOrthographicSize > 0f
                ? definition.DefaultOrthographicSize : Mathf.Max(0.01f, camera.Lens.OrthographicSize);
            Vector3 entry = definition.EntryPosition;
            entry.z = camera.transform.position.z;
            EntryPosition = entry;
        }
    }
}
