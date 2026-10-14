// Current date KDH 2026-10-08
// 지원 건물이 이번 판 동안 아군 계열에 주는 스탯 보정.
// 플레이 중 누적값은 여기에 두지 않습니다. SO를 여러 건물이 공유하기 때문입니다.
using System;
using UnityEngine;
using Units;
using Units.Skills;

namespace OZGL.KDH
{
    [Serializable]
    public struct BuildingSupportEffect
    {
        public UnitStatType statType;

        [Tooltip("Percent의 0.2는 +20%입니다. Flat은 고정 수치입니다.")]
        public UnitStatModifierType modifierType;

        public float value;
    }

    [Serializable]
    public class BuildingSupportSettings
    {
        public bool enabled;

        [Tooltip("이 클래스 아군만 강화합니다. Default면 계열 효과는 적용하지 않습니다.")]
        public AllyUnitClass targetClass;

        // Current date KDH 2026-10-14
        // 한 건물이 공격력, 체력처럼 여러 스탯을 같이 올립니다. 계열은 위 targetClass 하나를 공유합니다.
        public BuildingSupportEffect[] effects;

        // Current date KDH 2026-10-14
        // T3 지원 건물만 채웁니다. 이 유닛 타입만 전용 패시브를 받습니다. Default면 전용 패시브는 적용하지 않습니다.
        public AllyUnitType targetUnitType;

        [Tooltip("이 유닛에게만 붙는 패시브입니다. 비우면 계열 수치 버프만 적용합니다.")]
        public PassiveSkillData uniquePassive;
    }
}
