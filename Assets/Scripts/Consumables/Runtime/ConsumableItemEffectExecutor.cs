using System;
using System.Collections.Generic;
using Units;
using Units.Skills;

// 효과 계산과 적용은 스킬 Resolver에 위임합니다. 아이템 소모는 매니저가 담당합니다.
public class ConsumableItemEffectExecutor
{
    private readonly SkillEffectResolver _resolver;

    public ConsumableItemEffectExecutor(SkillEffectResolver resolver)
    {
        _resolver = resolver;
    }

    // 여러 대상 중 하나라도 적용되었는지 전달합니다.
    public void Execute(ConsumableItemData item, IReadOnlyList<ICombatTarget> targets,
        Func<bool> canContinue, out bool applied)
    {
        applied = false;
        if (_resolver == null || item == null || item.Effects == null || targets == null)
            return;

        var effects = new List<SkillEffectData>(item.Effects);
        var candidates = new List<ICombatTarget>(targets);
        var seen = new HashSet<ICombatTarget>();
        foreach (ICombatTarget target in candidates)
        {
            if (canContinue != null && !canContinue()) break;
            if (!CombatTargetUtility.IsValid(target) || !target.IsAlive || !seen.Add(target)) continue;

            // 아이템에는 유닛 시전자가 없습니다. Resolver의 null 시전자 지원이 필요합니다.
            var request = new SkillEffectRequest(null, target, effects, canContinue: canContinue);
            IReadOnlyList<CombatApplicationResult> results = _resolver.ResolveWithResults(request);
            foreach (CombatApplicationResult result in results)
            {
                if (result != null && result.WasApplied)
                    applied = true;
            }
        }
    }
}
