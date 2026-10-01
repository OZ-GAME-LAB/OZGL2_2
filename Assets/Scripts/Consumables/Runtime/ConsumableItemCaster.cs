using System;
using System.Collections.Generic;
using Units;
using Units.Effects;
using Units.Skills;
using UnityEngine;

// 아이템 계산과 전투 이벤트의 출처. 전투 유닛 목록/AI/콜라이더에는 등록하지 않습니다.
[RequireComponent(typeof(Unit_RuntimeStatus))]
public sealed class ConsumableItemCaster : MonoBehaviour, ICombatTarget
{
    public event Action<CombatStateChange> CombatStateChanged;
    public Transform Transform => transform;
    // 아이템은 대상/위치를 직접 지정하므로 시전자 방향은 기본값으로 고정합니다.
    public Vector2 FacingDirection => Vector2.left;
    public Unit_RuntimeStatus RuntimeStatus { get; private set; }
    public UnitTeam Team => UnitTeam.Ally;
    public bool IsAlive => _initialized && isActiveAndEnabled;
    // Resolver는 출처에도 IsTargetable을 요구합니다. 실제 대상 선정 목록에는 포함하지 않습니다.
    public bool IsTargetable => IsAlive;
    public int LifetimeVersion { get; private set; }
    public float CurrentHp => IsAlive ? RuntimeStatus.MaxHp : 0f;
    public float CurrentShield => 0f;
    private bool _initialized;

    private void Awake()
    {
        RuntimeStatus = GetComponent<Unit_RuntimeStatus>();
        if (RuntimeStatus.UnitData == null)
        {
            Debug.LogError("[Consumables/ConsumableItemCaster] 아이템 전용 UnitData를 연결해주세요.", this);
            return;
        }
        // 아티팩트/유닛 스폰 보정을 전달하지 않고 전용 기본 스탯만 사용합니다.
        RuntimeStatus.Initialize(null);
        _initialized = true;
    }

    private void OnEnable() => LifetimeVersion++;
    private void OnDisable()
    {
        LifetimeVersion++;
        CombatStateChanged?.Invoke(CombatStateChange.Lifetime);
    }

    public CombatApplicationResult TakeDamageWithResult(DamageResult result) =>
        CombatApplicationResult.Invalid(CombatApplicationKind.Damage, this, result.Metadata, "Item caster is source-only");
    public CombatApplicationResult HealWithResult(float amount, CombatEventMetadata metadata) =>
        CombatApplicationResult.Invalid(CombatApplicationKind.Heal, this, metadata, "Item caster is source-only");
    public CombatApplicationResult AddShieldWithResult(float amount, CombatEventMetadata metadata) =>
        CombatApplicationResult.Invalid(CombatApplicationKind.Shield, this, metadata, "Item caster is source-only");

    public bool TryConsumeEffectStacks(EffectStackConsumeRequest request) => false;
    public void TakeDamage(DamageResult result) { }
    public void Heal(float amount) { }
    public void AddShield(float amount) { }
    public void NotifySkillEvent(CombatSkillEvent notification) { }
    public void NotifyDamageDealt(ICombatTarget target) { }
    public void CollectDamageModifiers(PassiveDamageOwnerType ownerType, List<PassiveDamageModifier> results) { }
    public bool EvaluateDamageModifierConditions(RuntimePassiveSkill runtimePassive, ICombatTarget target,
        CombatSourceSnapshot frozenTarget = null) => false;
}
