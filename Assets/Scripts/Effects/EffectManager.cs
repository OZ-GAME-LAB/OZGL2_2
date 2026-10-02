using System;
using System.Collections.Generic;
using Units;
using UnityEngine;

// 아티팩트·제단·토템의 효과를 Source별로 보관하는 공통 창구
public class EffectManager : MonoBehaviour
{
    public IReadOnlyList<AllyStatModifier> AllyModifiers => _allyModifiers;
    public IReadOnlyList<EnemyStatModifier> EnemyModifiers => _enemyModifiers;
    public IReadOnlyList<AllyPassiveSkillModifier> AllyPassiveSkillModifiers => _allyPassiveSkillModifiers;
    public IReadOnlyList<EnemyPassiveSkillModifier> EnemyPassiveSkillModifiers => _enemyPassiveSkillModifiers;
    public IReadOnlyList<CurrencyModifier> CurrencyModifiers => _currencyModifiers;
    public IReadOnlyList<ConsumableSlotModifier> ConsumableSlotModifiers => _consumableSlotModifiers;
    // 기본 슬롯 수는 포함하지 않은 전체 출처의 보정 합계
    public int ConsumableSlotAdjustment { get; private set; }

    // 목록 갱신 완료 후 이벤트 발생 (기존 공통 효과를 새 목록으로 교체할 때 사용)
    public event Action EffectsChanged;

    private readonly EffectConverter _converter = new EffectConverter();
    private readonly Dictionary<object, ConvertedEffects> _effectsBySource =
        new Dictionary<object, ConvertedEffects>();
    private readonly List<AllyStatModifier> _allyModifiers = new List<AllyStatModifier>();
    private readonly List<EnemyStatModifier> _enemyModifiers = new List<EnemyStatModifier>();
    private readonly List<AllyPassiveSkillModifier> _allyPassiveSkillModifiers = new List<AllyPassiveSkillModifier>();
    private readonly List<EnemyPassiveSkillModifier> _enemyPassiveSkillModifiers = new List<EnemyPassiveSkillModifier>();
    private readonly List<CurrencyModifier> _currencyModifiers = new List<CurrencyModifier>();
    private readonly List<ConsumableSlotModifier> _consumableSlotModifiers = new List<ConsumableSlotModifier>();

    // 같은 Source의 전체 효과를 교체합니다. 생략한 효과도 제거하며, 변환 실패 시 기존 효과를 유지합니다.
    public bool TrySetEffects(object source, EffectDataGroup effects, int stackCount = 1)
    {
        if (!TryConvertEffects(source, effects, stackCount, out ConvertedEffects converted))
        {
            return false;
        }

        RegisterEffects(source, converted);
        return true;
    }

    // 효과 데이터 변환만 처리 (기존 효과 변경 및 이벤트 발생 X)
    public bool TryConvertEffects(object source, EffectDataGroup effects,
        int stackCount, out ConvertedEffects converted)
    {
        converted = null;
        if (source == null || effects == null || stackCount < 1)
        {
            Debug.LogError("[Effects/EffectManager] 출처·효과 묶음·중첩 수(1 이상)를 확인하세요.");
            return false;
        }

        ConvertedUnitStatEffects unitModifiers = _converter.ConvertUnitStatEffects(
            source, effects.StatEffects ?? Array.Empty<UnitStatEffectData>(), stackCount);
        if (unitModifiers == null)
        {
            return false;
        }

        List<CurrencyModifier> currencyModifiers = _converter.ConvertCurrencyEffects(
            source, effects.CurrencyEffects ?? Array.Empty<CurrencyEffectData>(), stackCount);
        if (currencyModifiers == null)
        {
            return false;
        }

        List<ConsumableSlotModifier> slotModifiers =
            _converter.ConvertConsumableSlotEffects(source, effects.SlotEffects ?? Array.Empty<ConsumableSlotEffectData>(), stackCount);
        if (slotModifiers == null)
        {
            return false;
        }

        if (!_converter.TryConvertPassiveSkillEffects(source, effects.PassiveEffects ?? Array.Empty<PassiveSkillEffectData>(),
            out var allyPassives, out var enemyPassives))
            return false;

        // 모든 변환이 성공한 경우에만 해당 Source의 결과 전체를 교체
        converted = new ConvertedEffects(
            unitModifiers.AllyModifiers, unitModifiers.EnemyModifiers, currencyModifiers, slotModifiers,
            allyPassives, enemyPassives);
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

    // 교환 양쪽의 효과를 함께 갱신한 뒤 한 번만 알림. 차감 효과가 null이면 Source 제거
    public void RegisterExchangeEffects(object ownedSource, ConvertedEffects ownedEffects,
        object rewardSource, ConvertedEffects rewardEffects)
    {
        if (ownedEffects == null)
        {
            _effectsBySource.Remove(ownedSource);
        }
        else
        {
            _effectsBySource[ownedSource] = ownedEffects;
        }

        _effectsBySource[rewardSource] = rewardEffects;
        RefreshModifiers();
    }

    // 저장 복원 시 한 시스템의 출처들만 교체하고 최종 상태를 한 번 알립니다.
    internal void ReplaceSourceEffects(IReadOnlyList<object> removed,
        IReadOnlyDictionary<object, ConvertedEffects> restored)
    {
        foreach (var source in removed) _effectsBySource.Remove(source);
        foreach (var pair in restored) _effectsBySource[pair.Key] = pair.Value;
        RefreshModifiers();
    }

    private void RefreshModifiers()
    {
        _allyModifiers.Clear();
        _enemyModifiers.Clear();
        _allyPassiveSkillModifiers.Clear();
        _enemyPassiveSkillModifiers.Clear();
        _currencyModifiers.Clear();
        _consumableSlotModifiers.Clear();
        long slotAdjustment = 0;

        foreach (ConvertedEffects effects in _effectsBySource.Values)
        {
            if (effects.AllyPassiveSkillModifiers != null)
                _allyPassiveSkillModifiers.AddRange(effects.AllyPassiveSkillModifiers);
            if (effects.EnemyPassiveSkillModifiers != null)
                _enemyPassiveSkillModifiers.AddRange(effects.EnemyPassiveSkillModifiers);
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
            if (effects.ConsumableSlotModifiers != null)
            {
                _consumableSlotModifiers.AddRange(effects.ConsumableSlotModifiers);
                foreach (ConsumableSlotModifier modifier in effects.ConsumableSlotModifiers)
                {
                    slotAdjustment += modifier.AdditionalSlots;
                }
            }
        }

        // 음수 합계는 유지하고 정수 범위 초과만 제한
        ConsumableSlotAdjustment = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, slotAdjustment));
        EffectsChanged?.Invoke();
    }

    // 재화 시스템은 지급 시점에 필요한 보상 경로의 보정치를 조회
    // Preparation / WaveReward / Production / QuarterComplete / RunSettlement.
    // 스탯 합산과 재화 지급 계산은 각 소비 시스템이 담당
}
