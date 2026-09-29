using System;
using UnityEngine;

// 패시브 추가 공격의 공통 Attack 설정과 트리거 위치 사용 여부를 보관한다.
namespace Units.Skills
{
    [Serializable]
    public sealed class PassiveAdditionalAttackActionData : PassiveSkillActionData
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillAttackActionData _attack = new();

        [SerializeField]
        private bool _useTriggerPosition;

        // ============================================================
        // Properties
        // ============================================================

        public SkillAttackActionData Attack => _attack;

        public bool UseTriggerPosition => _useTriggerPosition;
    }
}
