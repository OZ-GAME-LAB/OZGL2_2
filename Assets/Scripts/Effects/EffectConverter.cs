using System.Collections.Generic;
using Units;
using UnityEngine;

// 아티팩트·제단·토템의 공통 효과 데이터를 유닛·재화 보정치로 변환
public class EffectConverter
{
    // source: 효과를 제공한 객체. 이후 같은 객체로 효과를 제거 가능
    // stackCount: 선형 중첩 수. 이미 계산된 수치를 전달할 때는 1을 사용
    // 잘못된 입력은 오류 로그 출력 후 null 반환 (등록 전 결과 확인 필요)
    public ConvertedUnitStatEffects ConvertUnitStatEffects(
        object source,
        IReadOnlyList<UnitStatEffectData> effects,
        int stackCount = 1)
    {
        if (source == null)
        {
            Debug.LogError("[Effects/EffectConverter] Source가 없습니다.");
            return null;
        }

        if (effects == null)
        {
            Debug.LogError("[Effects/EffectConverter] 효과 목록이 없습니다.");
            return null;
        }

        if (stackCount < 1)
        {
            Debug.LogError("[Effects/EffectConverter] 중첩 수는 1 이상이어야 합니다.");
            return null;
        }

        List<AllyStatModifier> allyModifiers = new List<AllyStatModifier>();
        List<EnemyStatModifier> enemyModifiers = new List<EnemyStatModifier>();

        foreach (UnitStatEffectData effect in effects)
        {
            float value = effect.Value * stackCount;
            if (!ValidateEffect(effect, value))
            {
                return null;
            }

            if (effect.TargetTeam == UnitTeam.Ally)
            {
                AllyStatModifier modifier = new AllyStatModifier(
                    source,
                    effect.ApplyType,
                    effect.AllyClass,
                    effect.AllyType,
                    effect.StatType,
                    effect.ModifierType,
                    value);

                allyModifiers.Add(modifier);
            }
            else if (effect.TargetTeam == UnitTeam.Enemy)
            {
                EnemyStatModifier modifier = new EnemyStatModifier(
                    source,
                    effect.ApplyType,
                    effect.EnemyClass,
                    effect.EnemyType,
                    effect.StatType,
                    effect.ModifierType,
                    value);

                enemyModifiers.Add(modifier);
            }
        }

        return new ConvertedUnitStatEffects(allyModifiers, enemyModifiers);
    }

    public List<CurrencyModifier> ConvertCurrencyEffects(
        object source,
        IReadOnlyList<CurrencyEffectData> currencyEffects,
        int stackCount = 1)
    {
        if (source == null)
        {
            Debug.LogError("[Effects/EffectConverter] Source가 없습니다.");
            return null;
        }

        if (currencyEffects == null)
        {
            Debug.LogError("[Effects/EffectConverter] 재화 효과 목록이 없습니다.");
            return null;
        }

        if (stackCount < 1)
        {
            Debug.LogError("[Effects/EffectConverter] 중첩 수는 1 이상이어야 합니다.");
            return null;
        }

        List<CurrencyModifier> currencyModifiers = new List<CurrencyModifier>();
        foreach (CurrencyEffectData effect in currencyEffects)
        {
            float value = effect.Value * stackCount;
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Debug.LogError("[Effects/EffectConverter] 재화 효과 수치는 유한한 값이어야 합니다.");
                return null;
            }

            CurrencyModifier modifier = new CurrencyModifier(
                source,
                effect.CurrencyType,
                effect.RewardType,
                effect.ModifierType,
                value);
            currencyModifiers.Add(modifier);
        }

        return currencyModifiers;
    }

    private bool ValidateEffect(UnitStatEffectData effect, float value)
    {
        if (effect.TargetTeam != UnitTeam.Ally && effect.TargetTeam != UnitTeam.Enemy)
        {
            Debug.LogError("[Effects/EffectConverter] 효과 대상 팀이 유효하지 않습니다.");
            return false;
        }

        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            Debug.LogError("[Effects/EffectConverter] 효과 수치는 유한한 값이어야 합니다.");
            return false;
        }

        return true;
    }

}
