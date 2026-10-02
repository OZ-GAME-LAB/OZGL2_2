using System;
using System.Collections.Generic;
using Units;
using Units.Skills;

// 효과 계산과 적용은 스킬 Resolver에 위임합니다. 아이템 소모는 매니저가 담당합니다.
public class ConsumableItemEffectExecutor
{
    public event Action<ConsumableItemData, ICombatTarget> TargetEffectApplying;
    public event Action<ConsumableItemData, ICombatTarget, IReadOnlyList<CombatApplicationResult>> TargetEffectApplied;

    private readonly SkillEffectResolver _resolver;
    private readonly ConsumableItemCaster _caster;

    public ConsumableItemEffectExecutor(SkillEffectResolver resolver, ConsumableItemCaster caster)
    {
        _resolver = resolver;
        _caster = caster;
    }

    // 여러 대상 중 하나라도 적용되었는지 전달합니다.
    public void Execute(ConsumableItemData item, IReadOnlyList<ICombatTarget> targets,
        Func<bool> canContinue, out bool applied)
    {
        applied = false;
        if (_resolver == null || _caster == null || !_caster.IsAlive || item == null || item.Effects == null || targets == null)
            return;

        var effects = new List<SkillEffectData>(item.Effects);
        var candidates = new List<ICombatTarget>(targets);
        var seen = new HashSet<ICombatTarget>();
        foreach (ICombatTarget target in candidates)
        {
            if (canContinue != null && !canContinue()) break;
            if (!CombatTargetUtility.IsValid(target) || !target.IsAlive || !seen.Add(target)) continue;

            var request = new SkillEffectRequest(_caster, target, effects, canContinue: canContinue);
            TargetEffectApplying?.Invoke(item, target);
            IReadOnlyList<CombatApplicationResult> results = Array.Empty<CombatApplicationResult>();
            try
            {
                results = _resolver.ResolveWithResults(request);
                foreach (CombatApplicationResult result in results)
                {
                    if (result != null && result.WasApplied)
                        applied = true;
                }
            }
            finally
            {
                // 예외로 종료되어도 관찰자가 적용 전 임시 상태를 정리할 수 있게 알립니다.
                TargetEffectApplied?.Invoke(item, target, results);
            }
        }
    }

}
