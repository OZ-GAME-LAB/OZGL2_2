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

        private readonly Collider2D[] _colliderBuffer;

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
        // Circle
        // ============================================================

        private void ResolveCircle(
            TargetHitRequest request)
        {
            int hitCount =
                Physics2D.OverlapCircle(
                    request.Origin,
                    request.Radius,
                    _contactFilter,
                    _colliderBuffer
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
                Physics2D.OverlapCircle(
                    request.Origin,
                    request.Radius,
                    _contactFilter,
                    _colliderBuffer
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
            if (target == null)
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