using UnityEngine;

namespace Game.Cameras
{
    [DisallowMultipleComponent]
    public sealed class CameraArea : MonoBehaviour
    {
        // ============================================================
        // Movement Area
        // ============================================================

        [Header("Movement Area")]

        [Tooltip("카메라의 기본 영역 크기입니다. Transform의 XY Scale도 반영됩니다.")]
        [SerializeField]
        private Vector2 _size = new Vector2(80f, 40f);

        [Tooltip("카메라의 기본 진입 위치입니다. 미지정 시 CameraArea의 중심을 사용합니다.")]
        [SerializeField]
        private Transform _entryPoint;

        [Tooltip("기본 Orthographic Size입니다. 0이면 Cinemachine Camera의 기존 값을 사용합니다.")]
        [SerializeField, Min(0f)]
        private float _defaultOrthographicSize;

        // ============================================================
        // Overview Area
        // ============================================================

        [Header("Overview Area")]

        [Tooltip("전투 준비 연출에서 보여줄 Overview 영역의 중심 오프셋입니다.")]
        [SerializeField]
        private Vector2 _overviewOffset = Vector2.zero;

        [Tooltip("전투 준비 연출에서 화면에 담을 Overview 영역의 크기입니다.")]
        [SerializeField]
        private Vector2 _overviewSize = new Vector2(80f, 40f);

        [Tooltip("Overview 화면에 추가할 여백입니다.")]
        [SerializeField, Min(0f)]
        private float _overviewPadding = 1f;

        // ============================================================
        // Properties
        // ============================================================

        public Vector3 EntryPosition =>
            _entryPoint != null
                ? _entryPoint.position
                : transform.position;

        public float DefaultOrthographicSize =>
            Mathf.Max(
                0f,
                _defaultOrthographicSize);

        public float OverviewPadding =>
            Mathf.Max(
                0f,
                _overviewPadding);

        /// <summary>
        /// 기본 카메라 영역.
        ///
        /// Base:
        /// 건설 단계에서 화면 전체에 표시할 캠프 영역.
        ///
        /// Battlefield:
        /// 전투 중 카메라가 이동할 수 있는 영역.
        /// </summary>
        public Rect Bounds
        {
            get
            {
                Vector3 scale =
                    transform.lossyScale;

                Vector2 size =
                    new Vector2(
                        Mathf.Max(
                            0.01f,
                            Mathf.Abs(_size.x * scale.x)),
                        Mathf.Max(
                            0.01f,
                            Mathf.Abs(_size.y * scale.y)));

                return new Rect(
                    (Vector2)transform.position - size * 0.5f,
                    size);
            }
        }

        /// <summary>
        /// 전투 준비 연출에서 화면에 담을 별도의 영역.
        /// Battlefield CameraArea에서 주로 사용한다.
        /// </summary>
        public Rect OverviewBounds
        {
            get
            {
                Vector3 scale =
                    transform.lossyScale;

                Vector2 size =
                    new Vector2(
                        Mathf.Max(
                            0.01f,
                            Mathf.Abs(_overviewSize.x * scale.x)),
                        Mathf.Max(
                            0.01f,
                            Mathf.Abs(_overviewSize.y * scale.y)));

                Vector2 offset =
                    new Vector2(
                        _overviewOffset.x * scale.x,
                        _overviewOffset.y * scale.y);

                Vector2 center =
                    (Vector2)transform.position + offset;

                return new Rect(
                    center - size * 0.5f,
                    size);
            }
        }

        // ============================================================
        // Validation
        // ============================================================

        private void OnValidate()
        {
            _size.x =
                Mathf.Max(
                    0.01f,
                    _size.x);

            _size.y =
                Mathf.Max(
                    0.01f,
                    _size.y);

            _overviewSize.x =
                Mathf.Max(
                    0.01f,
                    _overviewSize.x);

            _overviewSize.y =
                Mathf.Max(
                    0.01f,
                    _overviewSize.y);

            _overviewPadding =
                Mathf.Max(
                    0f,
                    _overviewPadding);
        }

        // ============================================================
        // Editor Preview
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            DrawMovementArea();
            DrawOverviewArea();
            DrawEntryPoint();
        }

        private void DrawMovementArea()
        {
            Rect bounds =
                Bounds;

            Gizmos.color =
                Color.cyan;

            Gizmos.DrawWireCube(
                new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    transform.position.z),
                new Vector3(
                    bounds.width,
                    bounds.height,
                    0f));
        }

        private void DrawOverviewArea()
        {
            Rect bounds =
                OverviewBounds;

            Gizmos.color =
                Color.green;

            Gizmos.DrawWireCube(
                new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    transform.position.z),
                new Vector3(
                    bounds.width,
                    bounds.height,
                    0f));
        }

        private void DrawEntryPoint()
        {
            Gizmos.color =
                Color.yellow;

            Gizmos.DrawWireSphere(
                EntryPosition,
                0.5f);
        }
    }
}