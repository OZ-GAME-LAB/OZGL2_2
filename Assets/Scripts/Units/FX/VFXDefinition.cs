using System;
using UnityEngine;

namespace Units.FX
{
    [Serializable]
    public sealed class VFXDefinition
    {
        [SerializeField] private string _key;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector2 _offset;
        [SerializeField] private Vector3 _scale = Vector3.one;
        [SerializeField] private VFXDirectionMode _directionMode;
        [SerializeField, Tooltip("프리팹 원본이 향하는 각도. 오른쪽 0, 위 90, 왼쪽 180도.")]
        private float _nativeAngle;
        [SerializeField] private float _rotationOffset;
        [SerializeField] private bool _spriteFacesRight = true;
        [SerializeField, Min(0.05f), Tooltip("완료 신호가 없는 연출의 회수 시간과 Independent 최대 수명.")]
        private float _maxLifetime = 5f;

        public string Key => _key;
        public GameObject Prefab => _prefab;
        public Vector2 Offset => _offset;
        public Vector3 Scale => _scale;
        public VFXDirectionMode DirectionMode => _directionMode;
        public float NativeAngle => _nativeAngle;
        public float RotationOffset => _rotationOffset;
        public bool SpriteFacesRight => _spriteFacesRight;
        public float MaxLifetime => Mathf.Max(0.05f, _maxLifetime);
    }
}
