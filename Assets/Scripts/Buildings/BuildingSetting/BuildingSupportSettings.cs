// Current date KDH 2026-10-08
// 지원 건물이 이번 판 동안 아군 계열에 주는 스탯 보정.
// 플레이 중 누적값은 여기에 두지 않습니다. SO를 여러 건물이 공유하기 때문입니다.
using System;
using UnityEngine;
using Units;

namespace OZGL.KDH
{
    [Serializable]
    public class BuildingSupportSettings
    {
        public bool enabled;

        [Tooltip("이 클래스 아군만 강화합니다. Default면 적용하지 않습니다.")]
        public AllyUnitClass targetClass = AllyUnitClass.Warrior;

        public UnitStatType statType = UnitStatType.AttackPower;

        [Tooltip("Percent의 0.2는 +20%입니다. Flat은 고정 수치입니다.")]
        public UnitStatModifierType modifierType = UnitStatModifierType.Percent;

        public float value = 0.2f;
    }
}
