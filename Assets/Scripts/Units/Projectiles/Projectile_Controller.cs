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


        private Vector2 _direction;


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

            _impactResolver =
                new ProjectileImpactResolver(
                    request.Attacker
                );

            transform.position =
                request.Origin;


            Vector2 offset =
                (Vector2)request.Target.Transform.position - request.Origin;

            _direction =
                offset.sqrMagnitude > 0f
                    ? offset.normalized
                    : Vector2.right;


            _isFlying = true;
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void FixedUpdate()
        {
            if (!_isFlying)
                return;


            _remainingLifetime -=
                Time.fixedDeltaTime;


            // 이미 발사된 투사체는 Target과 공격자의 상태에 관계없이 유지되며,
            // 충돌하거나 수명이 종료될 때까지 발사 시점의 방향으로 이동한다.
            if (_remainingLifetime <= 0f)
            {
                Release();

                return;
            }


            Vector2 origin =
                transform.position;


            float distance =
                _request.ProjectileSpeed
                * Time.fixedDeltaTime;


            var filter =
                ContactFilter2D.noFilter;


            int count;


            // 고속 투사체가 Collider를 건너뛰지 않도록 이번 틱의 이동 구간 전체를 검사한다.
            do
            {
                count = Physics2D.CircleCast(
                    origin,
                    _radius,
                    _direction,
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


            ICombatTarget impactTarget =
                null;


            float nearestDistance =
                float.MaxValue;


            for (int i = 0;
                i < count;
                i++)
            {
                var collider =
                    _hits[i].collider;


                if (collider == null)
                    continue;


                var candidate =
                    collider.GetComponentInParent<ICombatTarget>();


                // 발사 시점에 저장한 Target Team의 유닛에만 충돌하며,
                // 다른 팀과 환경 Collider는 통과한다.
                if (!CombatTargetUtility.IsValid(candidate)
                    || candidate.Team != _request.TargetTeam
                    || (_request.TargetFilter != null && !_request.TargetFilter(candidate))
                    || _hits[i].distance >= nearestDistance)
                {
                    continue;
                }


                impactTarget =
                    candidate;


                nearestDistance =
                    _hits[i].distance;
            }


            if (impactTarget != null)
            {
                Impact(
                    impactTarget,
                    origin
                    + _direction
                    * nearestDistance,
                    _direction
                );


                return;
            }


            transform.position =
                origin
                + _direction
                * distance;
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


            transform.position =
                position;


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