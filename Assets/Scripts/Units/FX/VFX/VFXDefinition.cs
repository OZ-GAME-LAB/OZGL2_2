using System;
using Units.Skills;
using UnityEngine;

namespace Units.FX
{
    [Serializable]
    public sealed class VFXDefinition
    {
        [SerializeField, Tooltip("카탈로그 분류용 권장 동작. 실제 실행 시점은 공격의 FX 설정을 따릅니다.")]
        private SkillFXOperation _operation;

        [SerializeField] private FXCatalogCategory _category;
        public FXCatalogCategory Category => _category;

        public SkillFXOperation Operation => _operation;

        [Header("Identity")]
        [SerializeField]
        private string _key;

        [SerializeField]
        private GameObject _prefab;

        [Header("Transform")]
        [SerializeField]
        private Vector2 _offset;

        [SerializeField]
        private Vector2 _scale = Vector2.one;

        [SerializeField]
        private float _rotationOffset;

        [Header("Direction")]
        [SerializeField]
        private VFXDirectionMode _directionMode;

        [SerializeField]
        private float _nativeAngle;

        [SerializeField]
        private bool _spriteFacesRight = true;

        [Header("Lifetime")]
        [SerializeField, Min(0.1f)]
        private float _maxLifetime = 0.3f;

        [Header("Rendering")]
        [SerializeField, Tooltip(
            "부착 대상 기준 렌더링 순서. " +
            "양수는 전체보다 앞, 음수는 전체보다 뒤, 0은 원본 유지.")]
        private int _sortingOrderOffset;

        [Header("Particle Playback")]
        [SerializeField, Tooltip(
            "ParticleSystem의 재생 시작 방식. " +
            "Original은 프리팹 설정을 그대로 사용하고, " +
            "EmitImmediately는 재생 직후 파티클을 추가로 즉시 방출합니다.")]
        private VFXParticleStartMode _particleStartMode =
            VFXParticleStartMode.Original;

        [SerializeField, Min(1), Tooltip(
            "EmitImmediately 사용 시 각 ParticleSystem에서 " +
            "즉시 방출할 파티클 수")]
        private int _immediateEmitCount = 1;

        [SerializeField, Tooltip(
            "ParticleSystem의 Scaling Mode 보정 방식. " +
            "Original은 프리팹 설정을 유지하고, " +
            "Hierarchy는 부모를 포함한 전체 Transform Scale을 반영합니다.")]
        private VFXParticleScalingMode _particleScalingMode =
            VFXParticleScalingMode.Original;

        [SerializeField, Tooltip(
            "ParticleSystem의 Simulation Space 보정 방식. " +
            "Original은 프리팹 설정을 유지하고, " +
            "Local은 VFX Transform의 이동과 회전을 파티클에 반영합니다.")]
        private VFXParticleSimulationMode _particleSimulationMode =
            VFXParticleSimulationMode.Original;

        public string Key => _key;
        public GameObject Prefab => _prefab;

        [SerializeField, Min(0.01f), Tooltip("카탈로그 Scale 적용 후 이펙트의 기준 반경(월드 단위). 범위 연동 요청은 실제 반경 / 기준 반경 배율을 사용합니다.")]
        private float _referenceRadius = 1f;
        public float ReferenceRadius => Mathf.Max(0.01f, _referenceRadius);

        public Vector2 Offset => _offset;
        public Vector2 Scale => _scale;
        public float RotationOffset => _rotationOffset;

        public VFXDirectionMode DirectionMode => _directionMode;
        public float NativeAngle => _nativeAngle;
        public bool SpriteFacesRight => _spriteFacesRight;

        public float MaxLifetime => _maxLifetime;

        public int SortingOrderOffset => _sortingOrderOffset;

        public VFXParticleStartMode ParticleStartMode =>
            _particleStartMode;

        public int ImmediateEmitCount =>
            Mathf.Max(1, _immediateEmitCount);

        public VFXParticleScalingMode ParticleScalingMode =>
            _particleScalingMode;

        public VFXParticleSimulationMode ParticleSimulationMode =>
            _particleSimulationMode;
    }
}