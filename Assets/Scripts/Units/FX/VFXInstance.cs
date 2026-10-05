using UnityEngine;
using Units.Skills;

namespace Units.FX
{
    // 풀의 독립 루트에 배치하며 프리팹 원본 회전·크기를 매 대여마다 복구한다.
    public sealed class VFXInstance : MonoBehaviour
    {
        private ParticleSystem[] _particles;
        private Animator[] _animators;
        private TrailRenderer[] _trails;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private VFXDefinition _definition;
        private FXPlayback _playback;
        private float _age, _stopAge;
        private bool _stopping, _finished;
        private bool _facesRight;

        internal GameObject Prefab { get; private set; }
        internal SkillFXRequest Request => _playback.Request;
        internal void Configure(GameObject prefab)
        {
            Prefab = prefab;
            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _animators = GetComponentsInChildren<Animator>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            foreach (var particle in _particles)
            {
                // 프리팹의 자동 Destroy/Disable 대신 매니저가 반환 시점을 소유한다.
                var main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        internal void Play(VFXDefinition definition, SkillFXRequest request)
        {
            _definition = definition;
            _playback = new FXPlayback(request);
            _age = _stopAge = 0f;
            _stopping = _finished = false;
            _facesRight = request.FacingDirection.x >= 0f;
            transform.SetPositionAndRotation(request.Position, _baseRotation);
            transform.localScale = Vector3.Scale(_baseScale, definition.Scale);
            gameObject.SetActive(true);
            foreach (var animator in _animators) { animator.Rebind(); animator.Update(0f); }
            foreach (var trail in _trails) { trail.Clear(); trail.emitting = true; }
            foreach (var particle in _particles) particle.Play(false);
            _playback.Tick();
            ApplyPose();
        }

        private void ApplyPose()
        {
            var direction = _playback.Direction;
            var rotation = _baseRotation * Quaternion.Euler(0f, 0f, _definition.RotationOffset);
            var scale = Vector3.Scale(_baseScale, _definition.Scale);
            if (_definition.DirectionMode == VFXDirectionMode.RotateToDirection)
                rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                    - _definition.NativeAngle + _definition.RotationOffset) * _baseRotation;
            else if (_definition.DirectionMode == VFXDirectionMode.FlipHorizontal)
            {
                if (Mathf.Abs(direction.x) > 0.001f) _facesRight = direction.x > 0f;
                if (_facesRight != _definition.SpriteFacesRight) scale.x = -scale.x;
            }
            transform.localScale = scale;
            transform.rotation = rotation;
            var offset = _definition.Offset;
            if (_definition.DirectionMode == VFXDirectionMode.FlipHorizontal && _facesRight != _definition.SpriteFacesRight)
                offset.x = -offset.x;
            transform.position = (Vector3)_playback.Position + rotation * (Vector3)offset;
        }

        internal bool Tick(float delta)
        {
            _age += delta;
            if (!_playback.Tick())
            {
                if (Request.Entry.EndPolicy == SkillFXEndPolicy.Independent) _playback.Detach();
                else Stop();
            }
            ApplyPose();
            if (_stopping) _stopAge += delta;
            bool particlesAlive = false;
            foreach (var particle in _particles) particlesAlive |= particle != null && particle.IsAlive(false);
            bool animatorAlive = false;
            foreach (var animator in _animators)
            {
                if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) continue;
                for (int layer = 0; layer < animator.layerCount; layer++)
                {
                    var state = animator.GetCurrentAnimatorStateInfo(layer);
                    animatorAlive |= state.loop || state.normalizedTime < 1f || animator.IsInTransition(layer);
                }
            }
            if (_finished) return true;
            float trailTime = 0f;
            foreach (var trail in _trails) if (trail != null) trailTime = Mathf.Max(trailTime, trail.time);
            if (_stopping) return (!particlesAlive && !animatorAlive && _stopAge >= trailTime) || _stopAge >= _definition.MaxLifetime;
            if (Request.Entry.EndPolicy == SkillFXEndPolicy.KeepActive) return false;
            return _age >= _definition.MaxLifetime || (_age > 0.01f && (_particles.Length + _animators.Length) > 0 && !particlesAlive && !animatorAlive);
        }

        // 애니메이션 이벤트에서도 명시적으로 완료를 알릴 수 있다.
        public void CompletePlayback() => _finished = true;
        internal void Detach() => _playback.Detach();
        internal void Stop()
        {
            if (_stopping) return;
            _stopping = true;
            _playback.Detach();
            foreach (var trail in _trails) trail.emitting = false;
            foreach (var particle in _particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        internal void ResetPlayback()
        {
            foreach (var particle in _particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var trail in _trails) trail.Clear();
            gameObject.SetActive(false);
            transform.localScale = _baseScale;
            transform.localRotation = _baseRotation;
            _playback = null;
            _definition = null;
        }
    }
}
