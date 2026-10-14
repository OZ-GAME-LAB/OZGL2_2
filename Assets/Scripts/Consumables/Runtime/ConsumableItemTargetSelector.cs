using System.Collections.Generic;
using Units;
using UnityEngine;

// 현재 전투에 등록된 유닛 중 아이템 설정에 맞는 대상만 선택합니다.
public class ConsumableItemTargetSelector
{
    private readonly RuntimeUnitManager _unitManager;

    public ConsumableItemTargetSelector(RuntimeUnitManager unitManager)
    {
        _unitManager = unitManager;
    }

    // Area는 selectedPosition이 필요합니다.
    // All은 추가 입력 없이 조회합니다. 잘못된 입력이나 대상 없음은 빈 목록을 반환합니다.
    public List<ICombatTarget> SelectTargets(
        ConsumableItemData item,
        Vector2? selectedPosition = null)
    {
        var results = new List<ICombatTarget>();
        if (_unitManager == null || item == null)
            return results;

        switch (item.TargetMode)
        {
            case ConsumableTargetMode.Area:
                if (!selectedPosition.HasValue || !IsFinite(selectedPosition.Value.x) ||
                    !IsFinite(selectedPosition.Value.y) || !IsFinite(item.Radius) || item.Radius < 0f)
                    return results;
                break;
            case ConsumableTargetMode.All:
                break;
            default:
                return results;
        }

        var seen = new HashSet<ICombatTarget>();
        Collect(_unitManager.AllyUnits, item, selectedPosition, seen, results);
        Collect(_unitManager.EnemyUnits, item, selectedPosition, seen, results);
        return results;
    }

    private static void Collect(
        IReadOnlyList<Unit_Gateway> units,
        ConsumableItemData item,
        Vector2? selectedPosition,
        HashSet<ICombatTarget> seen,
        List<ICombatTarget> results)
    {
        for (int i = 0; i < units.Count; i++)
        {
            ICombatTarget target = units[i];
            if (!CombatTargetUtility.IsValid(target) || !target.IsAlive || !MatchesTeam(item.TargetTeam, target.Team))
                continue;

            if (item.TargetMode == ConsumableTargetMode.Area)
            {
                Vector2 position = target.Transform.position;
                if (!IsFinite(position.x) || !IsFinite(position.y))
                    continue;

                // 기존 스킬 범위 판정과 동일하게 XY 평면에서 유닛 중심을 기준으로 합니다.
                Vector2 offset = position - selectedPosition.Value;
                if (offset.sqrMagnitude > item.Radius * item.Radius)
                    continue;
            }

            if (seen.Add(target))
                results.Add(target);
        }
    }

    private static bool MatchesTeam(ConsumableTargetTeam setting, UnitTeam team)
    {
        return setting switch
        {
            ConsumableTargetTeam.Ally => team == UnitTeam.Ally,
            ConsumableTargetTeam.Enemy => team == UnitTeam.Enemy,
            ConsumableTargetTeam.All => team == UnitTeam.Ally || team == UnitTeam.Enemy,
            _ => false
        };
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
