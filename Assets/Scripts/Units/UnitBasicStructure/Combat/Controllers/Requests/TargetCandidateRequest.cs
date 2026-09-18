using System;
using UnityEngine;

namespace Units
{
    /// <summary>
    /// 타겟 후보 탐색 요청 데이터
    /// </summary>
    public readonly struct TargetCandidateRequest
    {
        public Vector2 Origin { get; }

        public float Range { get; }

        public UnitTeam TargetTeam { get; }

        public Predicate<ICombatTarget> TargetFilter { get; }


        public TargetCandidateRequest(
            Vector2 origin,
            float range,
            UnitTeam targetTeam,
            Predicate<ICombatTarget> targetFilter = null)
        {
            Origin = origin;

            Range = Mathf.Max(
                0f,
                range
            );

            TargetTeam = targetTeam;
            TargetFilter = targetFilter;
        }
    }
}