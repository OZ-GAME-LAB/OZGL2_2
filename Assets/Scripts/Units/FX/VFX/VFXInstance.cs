using System.Collections.Generic;
using UnityEngine;
using Units.Skills;

namespace Units.FX
{
    // 풀의 독립 루트에 배치하며 프리팹 원본 회전·크기를 매 대여마다 복구한다.
    public sealed class VFXInstance : MonoBehaviour
    {
        private ParticleSystem[] _particles;
        private AudioSource[] _audioSources;

        private ParticleSystemScalingMode[] _baseParticleScalingModes;
        private ParticleSystemSimulationSpace[] _baseParticleSimulationSpaces;

        private ParticleSystemRenderer[] _particleRenderers;
        private ParticleSystemRenderSpace[] _baseParticleAlignments;

        private Animator[] _animators;
        private TrailRenderer[] _trails;

        private Renderer[] _renderers;
        private int[] _baseSortingOrders;

        private Quaternion _baseRotation;
        private Vector3 _baseScale;

        private VFXDefinition _definition;
        private FXPlayback _playback;

        private float _age;
        private float _stopAge;

        private bool _stopping;
        private bool _finished;
        private bool _facesRight;


        internal long LastUsedGeneration { get; set; }

        internal GameObject Prefab { get; private set; }

        internal SkillFXRequest Request => _playback.Request;

        // ============================================================
        // Material
        // ============================================================

        // ============================================================
        // Configure
        // ============================================================

        internal void Configure(GameObject prefab)
        {
            Prefab = prefab;

            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;

            _particles =
                GetComponentsInChildren<ParticleSystem>(true);

            _animators =
                GetComponentsInChildren<Animator>(true);

            _trails =
                GetComponentsInChildren<TrailRenderer>(true);

            _audioSources = GetComponentsInChildren<AudioSource>(true);
            SilenceEmbeddedAudio();

            CacheParticleSettings();
            CacheSortingOrders();
        }

        // VFX 프리팹에 포함된 소리는 SFXManager와 중복 재생하지 않는다.
        private void SilenceEmbeddedAudio()
        {
            foreach (var source in _audioSources)
            {
                if (source == null) continue;
                source.playOnAwake = false;
                source.mute = true;
                source.Stop();
                source.enabled = false;
            }
        }

        private void CacheParticleSettings()
        {
            _baseParticleScalingModes =
                new ParticleSystemScalingMode[_particles.Length];

            _baseParticleSimulationSpaces =
                new ParticleSystemSimulationSpace[_particles.Length];

            _particleRenderers = new ParticleSystemRenderer[_particles.Length];
            _baseParticleAlignments = new ParticleSystemRenderSpace[_particles.Length];

            for (int i = 0; i < _particles.Length; i++)
            {
                var particle = _particles[i];

                if (particle == null)
                    continue;

                var main = particle.main;

                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                _particleRenderers[i] = renderer;
                if (renderer != null)
                    _baseParticleAlignments[i] = renderer.alignment;

                // 외부 에셋의 원래 Scaling Mode를 보관한다.
                _baseParticleScalingModes[i] =
                    main.scalingMode;

                // 외부 에셋의 원래 Simulation Space를 보관한다.
                _baseParticleSimulationSpaces[i] =
                    main.simulationSpace;

                // 프리팹의 자동 Destroy/Disable 대신
                // VFXManager가 반환 시점을 소유한다.
                main.stopAction =
                    ParticleSystemStopAction.None;
            }
        }

        private void CacheSortingOrders()
        {
            // VFX 내부 Renderer의 원본 Sorting Order를 저장한다.
            // 풀 재사용 시에도 항상 이 값을 기준으로 재계산한다.
            _renderers =
                GetComponentsInChildren<Renderer>(true);

            _baseSortingOrders =
                new int[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];

                if (renderer == null)
                    continue;

                _baseSortingOrders[i] =
                    renderer.sortingOrder;
            }
        }

        // ============================================================
        // Playback
        // ============================================================

        internal void Play(
            VFXDefinition definition,
            SkillFXRequest request)
        {
            _definition = definition;
            _playback = new FXPlayback(request);

            _age = 0f;
            _stopAge = 0f;

            _stopping = false;
            _finished = false;

            _facesRight =
                request.FacingDirection.x >= 0f;

            // --------------------------------------------------------
            // 기본 Transform
            // --------------------------------------------------------

            transform.SetPositionAndRotation(
                request.Position,
                _baseRotation);

            // 전체 크기는 VFX 루트에서만 조절한다.
            // 자식 Transform에 다시 Scale을 적용하지 않는다.
            transform.localScale =
                Vector3.Scale(
                    _baseScale,
                    Vector2.Scale(definition.Scale, request.Entry.ResolveScale(request.AttackRadius, definition.ReferenceRadius)));

            // --------------------------------------------------------
            // Particle 설정
            // --------------------------------------------------------

            // 외부 에셋의 설정을 현재 VFXDefinition에 맞게
            // 런타임에서만 보정한다.
            ApplyParticleSettings();

            // --------------------------------------------------------
            // 최초 위치 / 방향 / 회전 확정
            // --------------------------------------------------------

            // Particle을 생성하기 전에 실제 Follow 대상의
            // 현재 위치와 방향을 먼저 가져온다.
            _playback.Tick();

            // RotateToDirection을 포함한 최종 Transform을
            // Particle 방출 전에 확정한다.
            ApplyPose();

            // 최종 부착 대상을 기준으로 Sorting을 적용한다.
            ApplySortingOrder();

            // --------------------------------------------------------
            // 활성화
            // --------------------------------------------------------

            SilenceEmbeddedAudio();
            gameObject.SetActive(true);
            SilenceEmbeddedAudio();

            // --------------------------------------------------------
            // Animator / Trail
            // --------------------------------------------------------

            foreach (var animator in _animators)
            {
                if (animator == null)
                    continue;

                animator.Rebind();
                animator.Update(0f);
            }

            foreach (var trail in _trails)
            {
                if (trail == null)
                    continue;

                trail.Clear();
                trail.emitting = true;
            }

            // --------------------------------------------------------
            // Particle
            // --------------------------------------------------------

            foreach (var particle in _particles)
            {
                if (particle == null)
                    continue;

                // 위치·회전·Scale·Simulation Space가 모두
                // 확정된 이후에 Particle 재생을 시작한다.
                particle.Play(false);

                // 외부 에셋의 Emission 값을 변경하지 않고
                // 필요한 VFX만 즉시 추가 방출한다.
                if (definition.ParticleStartMode
                    == VFXParticleStartMode.EmitImmediately)
                {
                    particle.Emit(
                        definition.ImmediateEmitCount);
                }
            }
        }

        // ============================================================
        // Particle Settings
        // ============================================================

        private void ApplyParticleSettings()
        {
            if (_particles == null)
                return;

            int count = _particles.Length;

            for (int i = 0; i < count; i++)
            {
                var particle = _particles[i];

                if (particle == null)
                    continue;

                var main = particle.main;

                // Scaling Mode
                switch (_definition.ParticleScalingMode)
                {
                    case VFXParticleScalingMode.Hierarchy:
                        main.scalingMode =
                            ParticleSystemScalingMode.Hierarchy;
                        break;

                    case VFXParticleScalingMode.Original:
                    default:
                        if (_baseParticleScalingModes != null
                            && i < _baseParticleScalingModes.Length)
                        {
                            main.scalingMode =
                                _baseParticleScalingModes[i];
                        }

                        break;
                }

                // View/World 정렬은 루트가 회전해도 입자의 표시 방향을 유지한다.
                // 방향 보정이 필요한 Billboard/Mesh만 로컬 정렬로 그린다.
                var renderer = _particleRenderers[i];
                if (renderer != null)
                {
                    var original = _baseParticleAlignments[i];
                    bool rotates = _definition.DirectionMode == VFXDirectionMode.RotateToDirection
                        || !Mathf.Approximately(_definition.RotationOffset, 0f);
                    bool supportsAlignment = renderer.renderMode == ParticleSystemRenderMode.Billboard
                        || renderer.renderMode == ParticleSystemRenderMode.Mesh;
                    renderer.alignment = rotates && supportsAlignment
                        && (original == ParticleSystemRenderSpace.View
                            || original == ParticleSystemRenderSpace.World)
                        ? ParticleSystemRenderSpace.Local
                        : original;
                }

                // Simulation Space
                switch (_definition.ParticleSimulationMode)
                {
                    case VFXParticleSimulationMode.Local:
                        main.simulationSpace =
                            ParticleSystemSimulationSpace.Local;
                        break;

                    case VFXParticleSimulationMode.Original:
                    default:
                        if (_baseParticleSimulationSpaces != null
                            && i < _baseParticleSimulationSpaces.Length)
                        {
                            main.simulationSpace =
                                _baseParticleSimulationSpaces[i];
                        }

                        break;
                }
            }
        }

        private void RestoreParticleSettings()
        {
            if (_particles == null)
                return;

            for (int i = 0; i < _particles.Length; i++)
            {
                var particle = _particles[i];

                if (particle == null)
                    continue;

                var main = particle.main;

                var renderer = _particleRenderers[i];
                if (renderer != null)
                    renderer.alignment = _baseParticleAlignments[i];

                if (_baseParticleScalingModes != null
                    && i < _baseParticleScalingModes.Length)
                {
                    main.scalingMode =
                        _baseParticleScalingModes[i];
                }

                if (_baseParticleSimulationSpaces != null
                    && i < _baseParticleSimulationSpaces.Length)
                {
                    main.simulationSpace =
                        _baseParticleSimulationSpaces[i];
                }
            }
        }

        // ============================================================
        // Sorting
        // ============================================================

        private void ApplySortingOrder()
        {
            RestoreBaseSortingOrders();

            if (_definition == null)
                return;

            int offset =
                _definition.SortingOrderOffset;

            // 0은 VFX 프리팹의 원본 Sorting Order를 그대로 사용한다.
            if (offset == 0)
                return;

            if (_playback == null)
                return;

            var followTarget =
                _playback.FollowTarget;

            // World FX 등 기준이 되는 대상이 없다면
            // 원본 Order를 사용한다.
            if (followTarget == null)
                return;

            var targetRenderers =
                followTarget.GetComponentsInChildren<SpriteRenderer>(true);

            if (targetRenderers == null
                || targetRenderers.Length == 0)
            {
                return;
            }

            if (_renderers == null
                || _renderers.Length == 0)
            {
                return;
            }

            int targetMin = int.MaxValue;
            int targetMax = int.MinValue;

            foreach (var renderer in targetRenderers)
            {
                if (renderer == null)
                    continue;

                targetMin =
                    Mathf.Min(
                        targetMin,
                        renderer.sortingOrder);

                targetMax =
                    Mathf.Max(
                        targetMax,
                        renderer.sortingOrder);
            }

            if (targetMin == int.MaxValue
                || targetMax == int.MinValue)
            {
                return;
            }

            int vfxMin = int.MaxValue;
            int vfxMax = int.MinValue;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;

                int order =
                    _baseSortingOrders[i];

                vfxMin =
                    Mathf.Min(
                        vfxMin,
                        order);

                vfxMax =
                    Mathf.Max(
                        vfxMax,
                        order);
            }

            if (vfxMin == int.MaxValue
                || vfxMax == int.MinValue)
            {
                return;
            }

            int shift;

            if (offset > 0)
            {
                // VFX 전체가 대상 전체보다 앞에 위치한다.
                //
                // Target Max = 25
                // VFX Min = -2
                // Offset = +1
                //
                // Shift = 25 + 1 - (-2)
                //       = 28
                //
                // VFX -2, 0, 3
                //   → 26, 28, 31
                shift =
                    targetMax
                    + offset
                    - vfxMin;
            }
            else
            {
                // VFX 전체가 대상 전체보다 뒤에 위치한다.
                //
                // Target Min = -100
                // VFX Max = 3
                // Offset = -1
                //
                // Shift = -100 - 1 - 3
                //       = -104
                //
                // VFX -2, 0, 3
                //   → -106, -104, -101
                shift =
                    targetMin
                    + offset
                    - vfxMax;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];

                if (renderer == null)
                    continue;

                renderer.sortingOrder =
                    _baseSortingOrders[i]
                    + shift;
            }
        }

        private void RestoreBaseSortingOrders()
        {
            if (_renderers == null
                || _baseSortingOrders == null)
            {
                return;
            }

            int count =
                Mathf.Min(
                    _renderers.Length,
                    _baseSortingOrders.Length);

            for (int i = 0; i < count; i++)
            {
                var renderer = _renderers[i];

                if (renderer == null)
                    continue;

                renderer.sortingOrder =
                    _baseSortingOrders[i];
            }
        }

        // ============================================================
        // Pose
        // ============================================================

        private void ApplyPose()
        {
            var direction =
                _playback.Direction;

            var rotation =
                _baseRotation
                * Quaternion.Euler(
                    0f,
                    0f,
                    _definition.RotationOffset);

            var scale =
                Vector3.Scale(
                    _baseScale,
                    Vector2.Scale(_definition.Scale, Request.Entry.ResolveScale(Request.AttackRadius, _definition.ReferenceRadius)));

            if (_definition.DirectionMode
                == VFXDirectionMode.RotateToDirection)
            {
                rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Atan2(
                            direction.y,
                            direction.x)
                        * Mathf.Rad2Deg
                        - _definition.NativeAngle
                        + _definition.RotationOffset)
                    * _baseRotation;
            }
            else if (_definition.DirectionMode
                     == VFXDirectionMode.FlipHorizontal)
            {
                if (Mathf.Abs(direction.x) > 0.001f)
                {
                    _facesRight =
                        direction.x > 0f;
                }

                if (_facesRight
                    != _definition.SpriteFacesRight)
                {
                    scale.x = -scale.x;
                }
            }

            transform.localScale = scale;
            transform.rotation = rotation;

            var offset =
                _definition.Offset;

            if (_definition.DirectionMode
                    == VFXDirectionMode.FlipHorizontal
                && _facesRight
                != _definition.SpriteFacesRight)
            {
                offset.x = -offset.x;
            }

            transform.position =
                (Vector3)_playback.Position
                + rotation * (Vector3)offset;
        }

        // ============================================================
        // Runtime
        // ============================================================

        internal bool Tick(float delta)
        {
            _age += delta;

            if (!_playback.Tick())
            {
                if (Request.Entry.EndPolicy == SkillFXEndPolicy.ClearImmediately)
                    return true;
                if (Request.Entry.EndPolicy
                    == SkillFXEndPolicy.Independent)
                {
                    _playback.Detach();
                }
                else
                {
                    Stop();
                }
            }

            ApplyPose();

            if (_stopping)
                _stopAge += delta;

            bool particlesAlive = false;

            foreach (var particle in _particles)
            {
                particlesAlive |=
                    particle != null
                    && particle.IsAlive(false);
            }

            bool animatorAlive = false;

            foreach (var animator in _animators)
            {
                if (animator == null
                    || !animator.isActiveAndEnabled
                    || animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                for (int layer = 0;
                     layer < animator.layerCount;
                     layer++)
                {
                    var state =
                        animator.GetCurrentAnimatorStateInfo(layer);

                    animatorAlive |=
                        state.loop
                        || state.normalizedTime < 1f
                        || animator.IsInTransition(layer);
                }
            }

            if (_finished)
                return true;

            float trailTime = 0f;

            foreach (var trail in _trails)
            {
                if (trail != null)
                {
                    trailTime =
                        Mathf.Max(
                            trailTime,
                            trail.time);
                }
            }

            if (_stopping)
            {
                return (!particlesAlive
                        && !animatorAlive
                        && _stopAge >= trailTime)
                       || _stopAge
                       >= _definition.MaxLifetime;
            }

            if (Request.Entry.EndPolicy
                == SkillFXEndPolicy.KeepActive)
            {
                return false;
            }

            return _age >= _definition.MaxLifetime
                   || (_age > 0.01f
                       && (_particles.Length
                           + _animators.Length) > 0
                       && !particlesAlive
                       && !animatorAlive);
        }

        // 애니메이션 이벤트에서도 명시적으로 완료를 알릴 수 있다.
        public void CompletePlayback()
        {
            _finished = true;
        }

        internal void Detach()
        {
            _playback.Detach();
        }

        internal void Stop()
        {
            if (_stopping)
                return;

            _stopping = true;
            _playback.Detach();

            foreach (var trail in _trails)
            {
                if (trail != null)
                    trail.emitting = false;
            }

            foreach (var particle in _particles)
            {
                if (particle == null)
                    continue;

                particle.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // ============================================================
        // Pool Reset
        // ============================================================

        internal void ResetPlayback()
        {
            SilenceEmbeddedAudio();
            foreach (var particle in _particles)
            {
                if (particle == null)
                    continue;

                particle.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (var trail in _trails)
            {
                if (trail != null)
                    trail.Clear();
            }

            // 런타임에서 변경했던 외부 ParticleSystem 설정을
            // 프리팹 원본 값으로 되돌린다.
            RestoreParticleSettings();

            // 다음 대여 시 이전 FX의 Sorting 설정이 남지 않도록
            // 프리팹 원본 값으로 복구한다.
            RestoreBaseSortingOrders();

            gameObject.SetActive(false);

            transform.localScale =
                _baseScale;

            transform.localRotation =
                _baseRotation;

            _playback = null;
            _definition = null;
        }
    }
}