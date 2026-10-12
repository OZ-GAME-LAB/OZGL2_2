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
            if (settings.targetClass == AllyUnitClass.Default)
            {
                Debug.LogWarning("[BuildingSupport] targetClass가 Default라 강화하지 않습니다.", this);
                return;
            }

            if (float.IsNaN(settings.value) || float.IsInfinity(settings.value) || settings.value == 0f)
            {
                Debug.LogWarning("[BuildingSupport] 강화 수치가 0이거나 유한하지 않아 적용하지 않습니다.", this);
                return;
            }

            // Find는 건설 때 한 번만 합니다. 캐시해 두면 파괴 때 다시 찾지 않습니다.
            _modifierManager = FindFirstObjectByType<UnitStatModifierManager>();
            if (_modifierManager == null)
            {
                Debug.LogWarning("[BuildingSupport] UnitStatModifierManager가 없어 강화할 수 없습니다.", this);
                return;
            }

            // struct라 new를 해도 힙 할당이 없습니다. Source는 이 컴포넌트라 이 건물만 뺄 수 있습니다.
            _modifierManager.AddAllyModifier(new AllyStatModifier(
                this,
                UnitModifierApplyType.Class,
                settings.targetClass,
                AllyUnitType.Default,
                settings.statType,
                settings.modifierType,
                settings.value));
            _applied = true;
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
