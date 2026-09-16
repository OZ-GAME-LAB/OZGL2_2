using System;
using UnityEngine;

namespace Units
{
    public class Projectile_Controller : MonoBehaviour
    {
        // ============================================================
        // Runtime State
        // ============================================================

        private ProjectileManager _manager;


        private ProjectileRequest _request;


        private ProjectileImpactResolver _impactResolver;


        private RaycastHit2D[] _hits = new RaycastHit2D[16];


        private float _radius;


        private float _remainingLifetime;


        private bool _isFlying;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsFlying => _isFlying;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            ProjectileManager manager,
            ProjectileRequest request,
            float collisionRadius,
            float maxLifetime)
        {
            _manager = manager;

            _request = request;

            _radius = Mathf.Max(
                0.01f,
                collisionRadius
            );

            _remainingLifetime = Mathf.Max(
                0.1f,
                maxLifetime
            );

            _impactResolver = new ProjectileImpactResolver(request.Attacker);

            transform.position = request.Origin;

            _isFlying = true;
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void FixedUpdate()
        {
            if (!_isFlying)
                return;

            _remainingLifetime -= Time.fixedDeltaTime;

            // Target이 사망하거나 사라지면 다른 대상을 찾지 않고 투사체를 제거한다.
            // 공격자가 사망해도 Object가 남아 있으면 이미 발사한 투사체는 유지한다.
            if (_remainingLifetime <= 0f
                || _request.Attacker == null
                || !CombatTargetUtility.IsValid(_request.Target))
            {
                Release();

                return;
            }

            Vector2 origin = transform.position;

            Vector2 offset = (Vector2)_request.Target.Transform.position - origin;

            Vector2 direction = offset.sqrMagnitude > 0f ? offset.normalized : Vector2.right;

            float distance = Mathf.Min(
                offset.magnitude,
                _request.ProjectileSpeed * Time.fixedDeltaTime
            );

            var filter = ContactFilter2D.noFilter;

            int count;

            // 고속 투사체가 Collider를 건너뛰지 않도록 이번 틱의 이동 구간 전체를 검사한다.
            do
            {
                count = Physics2D.CircleCast(
                    origin,
                    _radius,
                    direction,
                    filter,
                    _hits,
                    distance
                );

                if (count < _hits.Length)
                    break;

                Array.Resize(
                    ref _hits,
                    _hits.Length * 2
                );
            }
            while (true);

            ICombatTarget impactTarget = null;

            float nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var collider = _hits[i].collider;

                if (collider == null)
                    continue;

                var candidate = collider.GetComponentInParent<ICombatTarget>();

                // 발사자 기준 상대 팀에만 충돌하고, 같은 팀과 환경 Collider는 통과한다.
                if (!CombatTargetUtility.IsValid(candidate)
                    || candidate.Team == _request.Attacker.Team
                    || _hits[i].distance >= nearestDistance)
                    continue;

                impactTarget = candidate;

                nearestDistance = _hits[i].distance;
            }

            if (impactTarget != null)
            {
                Impact(
                    impactTarget,
                    origin + direction * nearestDistance,
                    direction
                );

                return;
            }

            transform.position = origin + direction * distance;

            // Collider가 없는 ICombatTarget은 목표 위치 도달 시 명중 처리한다.
            if (offset.magnitude <= distance + 0.001f)
                Impact(
                    _request.Target,
                    transform.position,
                    direction
                );
        }


        // ============================================================
        // Impact
        // ============================================================

        private void Impact(
            ICombatTarget target,
            Vector2 position,
            Vector2 direction)
        {
            if (!_isFlying)
                return;

            _isFlying = false;

            transform.position = position;

            try
            {
                _impactResolver.Resolve(
                    _request,
                    target,
                    position,
                    direction
                );
            }
            finally
            {
                Release();
            }
        }


        // ============================================================
        // Projectile Return
        // ============================================================

        private void Release()
        {
            _isFlying = false;

            if (_manager != null)
                _manager.Release(this);
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnDisable()
        {
            _isFlying = false;
        }
    }
}
