using System;
using System.Collections.Generic;
using Units;
using UnityEngine;

// 아티팩트·제단·토템의 효과를 Source별로 보관하는 공통 창구
public class EffectManager : MonoBehaviour
{
    public IReadOnlyList<AllyStatModifier> AllyModifiers => _allyModifiers;
    public IReadOnlyList<EnemyStatModifier> EnemyModifiers => _enemyModifiers;
    public IReadOnlyList<CurrencyModifier> CurrencyModifiers => _currencyModifiers;

    // 목록 갱신 완료 후 이벤트 발생 (기존 공통 효과를 새 목록으로 교체할 때 사용)
    public event Action EffectsChanged;

    private readonly EffectConverter _converter = new EffectConverter();
    private readonly Dictionary<object, ConvertedEffects> _effectsBySource =
        new Dictionary<object, ConvertedEffects>();
    private readonly List<AllyStatModifier> _allyModifiers = new List<AllyStatModifier>();
    private readonly List<EnemyStatModifier> _enemyModifiers = new List<EnemyStatModifier>();
    private readonly List<CurrencyModifier> _currencyModifiers = new List<CurrencyModifier>();

    // 같은 Source의 효과를 교체. 변환 실패 시 기존 효과 유지
    public bool TrySetEffects(object source, IReadOnlyList<UnitStatEffectData> effects,
        IReadOnlyList<CurrencyEffectData> currencyEffects, int stackCount = 1)
    {
        if (!TryConvertEffects(source, effects, currencyEffects, stackCount, out ConvertedEffects converted))
        {
            return false;
        }

        RegisterEffects(source, converted);
        return true;
    }

    // 효과 데이터 변환만 처리 (기존 효과 변경 및 이벤트 발생 X)
    public bool TryConvertEffects(object source, IReadOnlyList<UnitStatEffectData> effects,
        IReadOnlyList<CurrencyEffectData> currencyEffects, int stackCount, out ConvertedEffects converted)
    {
        converted = null;
        ConvertedUnitStatEffects unitModifiers = _converter.ConvertUnitStatEffects(source, effects, stackCount);
        if (unitModifiers == null)
        {
            return false;
        }

        List<CurrencyModifier> currencyModifiers = _converter.ConvertCurrencyEffects(source, currencyEffects, stackCount);
        if (currencyModifiers == null)
        {
            return false;
        }

        // 두 변환이 모두 성공한 경우에만 해당 Source의 결과 전체를 교체
        converted = new ConvertedEffects(
            unitModifiers.AllyModifiers, unitModifiers.EnemyModifiers, currencyModifiers);
        return true;
    }

    // TryConvertEffects로 준비한 결과를 같은 Source에 등록
    public void RegisterEffects(object source, ConvertedEffects converted)
    {
        _effectsBySource[source] = converted;
        RefreshModifiers();
    }

    // 해당 Source의 효과만 제거하여 다른 시스템의 효과를 유지
    public bool RemoveEffects(object source)
    {
        if (source == null || !_effectsBySource.Remove(source))
        {
            return false;
        }

        RefreshModifiers();
        return true;
    }

    private void RefreshModifiers()
    {
        _allyModifiers.Clear();
        _enemyModifiers.Clear();
        _currencyModifiers.Clear();

        foreach (ConvertedEffects effects in _effectsBySource.Values)
        {
            if (effects.AllyModifiers != null)
            {
                _allyModifiers.AddRange(effects.AllyModifiers);
            }

            if (effects.EnemyModifiers != null)
            {
                _enemyModifiers.AddRange(effects.EnemyModifiers);
            }
            if (effects.CurrencyModifiers != null)
            {
                _currencyModifiers.AddRange(effects.CurrencyModifiers);
            }
        }

        EffectsChanged?.Invoke();
    }

    // 재화 시스템은 지급 시점에 필요한 보상 경로의 보정치를 조회
    // Preparation / WaveReward / Production / QuarterComplete / RunSettlement.
    // 스탯 합산과 재화 지급 계산은 각 소비 시스템이 담당
}
