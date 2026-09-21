using System;
using UnityEngine;



namespace Units.Skills
{
    [Serializable]
    public abstract class PassiveDamageModifierActionData
        : PassiveSkillActionData
    {
        // ============================================================
        // Owner
        // ============================================================

        [Header("Owner")]
        [SerializeField]
        private PassiveDamageOwnerType _ownerType =
            PassiveDamageOwnerType.Attacker;


        // ============================================================
        // Properties
        // ============================================================

        public PassiveDamageOwnerType OwnerType =>
            _ownerType;

        // 데미지 계산 중 이 Action이 적용되는 단계를 반환한다.
        public abstract PassiveDamageCalculationType CalculationType
        {
            get;
        }
    }
}