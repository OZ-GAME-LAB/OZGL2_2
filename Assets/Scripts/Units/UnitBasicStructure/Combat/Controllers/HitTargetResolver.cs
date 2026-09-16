using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    public class HitTargetResolver
    {
        private const int DefaultBufferSize =
            32;


        // ============================================================
        // Reference
        // ============================================================

        private readonly Unit_Core _core;


        // ============================================================
        // Physics
        // ============================================================

        private readonly ContactFilter2D _contactFilter;


        // ============================================================
        // Runtime Buffer
        // ============================================================

        private Collider2D[] _colliderBuffer;

        private readonly List<ICombatTarget> _targets;


        // ============================================================
        // Constructor
        // ============================================================

        public HitTargetResolver(
            Unit_Core core,
            int bufferSize = DefaultBufferSize)
        {
            _core =
                core;


            int finalBufferSize =
                Mathf.Max(
                    1,
                    bufferSize
                );


            _colliderBuffer =
                new Collider2D[
                    finalBufferSize
                ];


            _targets =
                new List<ICombatTarget>(
                    finalBufferSize
                );


            _contactFilter = ContactFilter2D.noFilter;
        }


        // ============================================================
        // Resolve
        // ============================================================

        public IReadOnlyList<ICombatTarget> Resolve(
            TargetHitRequest request)
        {
            _targets.Clear();


            if (_core == null)
                return _targets;

            if (request.MaxTargetCount <= 0)
                return _targets;

            if (request.Radius <= 0f)
                return _targets;


            switch (request.AreaType)
            {
                case HitAreaType.Circle:

                    ResolveCircle(
                        request
                    );

                    break;


                case HitAreaType.Cone:

                    ResolveCone(
                        request
                    );

                    break;
            }


            SortByDistance(
                request.Origin
            );


            TrimTargetCount(
                request.MaxTargetCount
            );


            return _targets;
        }


        // ============================================================
        // Projectile Target Selection
        // ============================================================

        public IReadOnlyList<ICombatTarget> ResolveAttackTargets(
            Vector2 origin,
            float range,
            int maxTargetCount,
            ICombatTarget preferredTarget)
        {
            Resolve(
                new TargetHitRequest(
                    origin,
                    Vector2.zero,
                    range,
                    0f,
                    int.MaxValue,
                    HitAreaType.Circle
                )
            );

            if (_core == null || range < 0f || maxTargetCount <= 0)
            {
                _targets.Clear();
                return _targets;
            }

            float rangeSquared = range * range;

            // Collider 일부만 사거리에 걸친 대상은 위치 기준으로 다시 검사한다.
            _targets.RemoveAll(
                target => !IsValidTarget(target)
                    || ((Vector2)target.Transform.position - origin).sqrMagnitude > rangeSquared
            );

            // 현재 목표도 동일한 사거리 검사를 통과해야 우선 선택된다.
            if (IsValidTarget(preferredTarget)
                && ((Vector2)preferredTarget.Transform.position - origin).sqrMagnitude <= rangeSquared)
            {
                _targets.Remove(preferredTarget);
                _targets.Insert(0, preferredTarget);
            }

            TrimTargetCount(maxTargetCount);

            return _targets;
        }


        // ============================================================
        // Physics Buffer
        // ============================================================

        private int CollectColliders(
            Vector2 origin,
            float radius)
        {
            while (true)
            {
                int count = Physics2D.OverlapCircle(
                    origin,
                    radius,
                    _contactFilter,
                    _colliderBuffer
                );

                if (count < _colliderBuffer.Length)
                    return count;

                // 밀집된 전장에서도 고정 버퍼 크기로 후보가 누락되지 않도록 확장한다.
                System.Array.Resize(
                    ref _colliderBuffer,
                    _colliderBuffer.Length * 2
                );
            }
        }

        // ============================================================
        // Circle
        // ============================================================

        private void ResolveCircle(
            TargetHitRequest request)
        {
            int hitCount =
                CollectColliders(
                    request.Origin,
                    request.Radius
                );


            for (int i = 0;
                i < hitCount;
                i++)
            {
                Collider2D hit =
                    _colliderBuffer[i];


                TryAddTarget(
                    hit
                );
            }
        }


        // ============================================================
        // Cone
        // ============================================================

        private void ResolveCone(
            TargetHitRequest request)
        {
            if (request.Direction.sqrMagnitude <= 0f)
                return;


            int hitCount =
                CollectColliders(
                    request.Origin,
                    request.Radius
                );


            Vector2 direction =
                request.Direction.normalized;


            float halfAngle =
                request.Angle
                * 0.5f;


            for (int i = 0;
                i < hitCount;
                i++)
            {
                Collider2D hit =
                    _colliderBuffer[i];


                if (hit == null)
                    continue;


                ICombatTarget target =
                    GetCombatTarget(
                        hit
                    );


                if (!IsValidTarget(
                    target))
                {
                    continue;
                }


                Vector2 targetDirection =
                    (Vector2)target.Transform.position
                    - request.Origin;


                if (targetDirection.sqrMagnitude <= 0f)
                    continue;


                float angle =
                    Vector2.Angle(
                        direction,
                        targetDirection.normalized
                    );


                if (angle > halfAngle)
                    continue;


                AddTarget(
                    target
                );
            }
        }


        // ============================================================
        // Target Resolve
        // ============================================================

        private void TryAddTarget(
            Collider2D hit)
        {
            if (hit == null)
                return;


            ICombatTarget target =
                GetCombatTarget(
                    hit
                );


            if (!IsValidTarget(
                target))
            {
                return;
            }


            AddTarget(
                target
            );
        }


        private ICombatTarget GetCombatTarget(
            Collider2D hit)
        {
            if (hit == null)
                return null;


            return hit.GetComponentInParent<ICombatTarget>();
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool IsValidTarget(
            ICombatTarget target)
        {
            if (!CombatTargetUtility.IsValid(target))
                return false;


            if (!target.IsTargetable)
                return false;


            if (target.Transform == null)
                return false;


            if (target.Team == _core.Team)
                return false;


            return true;
        }


        // ============================================================
        // Target List
        // ============================================================

        private void AddTarget(
            ICombatTarget target)
        {
            if (_targets.Contains(
                target))
            {
                return;
            }


            _targets.Add(
                target
            );
        }


        private void SortByDistance(
            Vector2 origin)
        {
            _targets.Sort(
                (a, b) =>
                {
                    float distanceA =
                        (
                            (Vector2)a.Transform.position
                            - origin
                        ).sqrMagnitude;


                    float distanceB =
                        (
                            (Vector2)b.Transform.position
                            - origin
                        ).sqrMagnitude;


                    return distanceA.CompareTo(
                        distanceB
                    );
                }
            );
        }


        private void TrimTargetCount(
            int maxTargetCount)
        {
            if (_targets.Count <= maxTargetCount)
                return;


            _targets.RemoveRange(
                maxTargetCount,
                _targets.Count
                - maxTargetCount
            );
        }
    }
}