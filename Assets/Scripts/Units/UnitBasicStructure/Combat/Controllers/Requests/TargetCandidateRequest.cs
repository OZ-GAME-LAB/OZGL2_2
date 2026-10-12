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

        // null: 위치 중심 검색, 값 있음: 유닛 사거리 보정 검색.
        public float? SourceBodyRadius { get; }

        public UnitTeam TargetTeam { get; }

        public Predicate<ICombatTarget> TargetFilter { get; }


        public TargetCandidateRequest(
            Vector2 origin,
            float range,
            UnitTeam targetTeam,
            Predicate<ICombatTarget> targetFilter = null,
            float? sourceBodyRadius = null)
        {
            Origin = origin;
            SourceBodyRadius = sourceBodyRadius;

            Range = Mathf.Max(
                0f,
                range
            );

            TargetTeam = targetTeam;
            TargetFilter = targetFilter;
        }
    }
}