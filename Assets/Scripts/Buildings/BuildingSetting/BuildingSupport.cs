// Current date KDH 2026-10-08
// 지원 건물이 서 있는 동안만 해당 계열 유닛 스탯을 올립니다.
// Update에서 매 프레임 찾지 않습니다. 지을 때 한 번 등록하고, 사라질 때 한 번 뺍니다.
using UnityEngine;
using Units;

namespace OZGL.KDH
{
    public class BuildingSupport : MonoBehaviour, IBuildingModule
    {
        private Building _owner;
        private UnitStatModifierManager _modifierManager;
        private bool _applied;

        public void Setup(Building owner)
        {
            Teardown();

            _owner = owner;
            if (_owner == null || _owner.Data == null)
            {
                Debug.LogWarning("[BuildingSupport] Setup에 BuildingData가 없습니다.", this);
                return;
            }

            if (!_owner.Data.HasSupport)
                return;

            BuildingSupportSettings settings = _owner.Data.Support;
            bool hasClass = settings.targetClass != AllyUnitClass.Default && HasAnyEffect(settings.effects);
            bool hasUnique = settings.targetUnitType != AllyUnitType.Default && settings.uniquePassive != null;
            if (settings.uniquePassive != null && settings.targetUnitType == AllyUnitType.Default)
            {
                Debug.LogWarning("[BuildingSupport] targetUnitType이 Default라 전용 패시브를 적용하지 않습니다.", this);
            }

            if (!hasClass && !hasUnique)
            {
                Debug.LogWarning("[BuildingSupport] 적용할 계열 효과와 전용 패시브가 없습니다.", this);
                return;
            }

            // Find는 건설 때 한 번만 합니다. 캐시해 두면 파괴 때 다시 찾지 않습니다.
            _modifierManager = FindFirstObjectByType<UnitStatModifierManager>();
            if (_modifierManager == null)
            {
                Debug.LogWarning("[BuildingSupport] UnitStatModifierManager가 없어 강화할 수 없습니다.", this);
                return;
            }

            // Current date KDH 2026-10-14
            // 계열 효과는 그 클래스 전체의 수치 버프입니다.
            // 전용 패시브는 targetUnitType 하나만 받습니다.
            // Source는 이 컴포넌트 하나라 건물이 사라지면 수치와 패시브가 같이 빠집니다.
            // struct라 new를 해도 힙 할당이 없고, 이 등록은 건설 때 한 번만 돕니다.
            if (hasClass)
            {
                AddEffects(settings.effects, UnitModifierApplyType.Class, settings.targetClass, AllyUnitType.Default);
            }

            if (hasUnique)
            {
                _modifierManager.AddAllyPassiveSkill(new AllyPassiveSkillModifier(
                    this,
                    settings.uniquePassive,
                    UnitModifierApplyType.Type,
                    AllyUnitClass.Default,
                    settings.targetUnitType));
                _applied = true;
            }
        }

        // Current date KDH 2026-10-14
        // 0이거나 유한하지 않은 수치는 보정치로 넣지 않습니다.
        private static bool HasAnyEffect(BuildingSupportEffect[] effects)
        {
            if (effects == null)
                return false;

            for (int i = 0; i < effects.Length; i++)
            {
                float value = effects[i].value;
                if (!float.IsNaN(value) && !float.IsInfinity(value) && value != 0f)
                    return true;
            }

            return false;
        }

        private void AddEffects(BuildingSupportEffect[] effects, UnitModifierApplyType applyType,
            AllyUnitClass targetClass, AllyUnitType targetUnitType)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                BuildingSupportEffect effect = effects[i];
                if (float.IsNaN(effect.value) || float.IsInfinity(effect.value) || effect.value == 0f)
                {
                    Debug.LogWarning($"[BuildingSupport] 효과[{i}] 수치가 0이거나 유한하지 않아 건너뜁니다.", this);
                    continue;
                }

                _modifierManager.AddAllyModifier(new AllyStatModifier(
                    this,
                    applyType,
                    targetClass,
                    targetUnitType,
                    effect.statType,
                    effect.modifierType,
                    effect.value));
                _applied = true;
            }
        }

        public void Teardown()
        {
            if (_applied && _modifierManager != null)
                _modifierManager.RemoveModifiersBySource(this);

            _applied = false;
            _modifierManager = null;
            _owner = null;
        }

        private void OnDestroy()
        {
            Teardown();
        }
    }
}
