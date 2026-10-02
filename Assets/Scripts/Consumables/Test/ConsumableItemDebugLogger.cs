using System;
using System.Collections.Generic;
using System.Text;
using Units;
using UnityEngine;

// 체크 해제 시 모든 관찰을 종료합니다. 관찰 로그에는 다른 스킬의 변화도 포함됩니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(ConsumableItemManager))]
public sealed class ConsumableItemDebugLogger : MonoBehaviour
{
    private ConsumableItemManager _manager;
    private readonly Dictionary<ICombatTarget, TargetState> _before = new();
    private readonly Dictionary<ICombatTarget, Observation> _observations = new();
    private readonly List<ICombatTarget> _expired = new();

    private sealed class Observation
    {
        public int Lifetime;
        public Unit_RuntimeStatus Status;
        public Unit_Life Life;
        public Action<UnitStatType, float, float> StatHandler;
        public Action<float> HealHandler;
    }

    private void OnEnable()
    {
        _manager = GetComponent<ConsumableItemManager>();
        _manager.TargetEffectApplying += BeforeApply;
        _manager.TargetEffectApplied += AfterApply;
    }

    private void OnDisable()
    {
        if (_manager != null)
        {
            _manager.TargetEffectApplying -= BeforeApply;
            _manager.TargetEffectApplied -= AfterApply;
        }
        foreach (var observation in _observations.Values) Unsubscribe(observation);
        _observations.Clear();
        _before.Clear();
    }

    private void BeforeApply(ConsumableItemData item, ICombatTarget target)
    {
        if (isActiveAndEnabled) _before[target] = new TargetState(target);
    }

    private void AfterApply(ConsumableItemData item, ICombatTarget target,
        IReadOnlyList<CombatApplicationResult> results)
    {
        if (!isActiveAndEnabled || !_before.TryGetValue(target, out var before)) return;
        _before.Remove(target);
        LogChanges(item, target, before, results);
        if (!CombatTargetUtility.Exists(target) || !target.IsAlive || target.LifetimeVersion != before.Lifetime) return;
        foreach (var result in results)
            if (result != null && result.WasApplied) { Observe(target); break; }
    }

    private void Observe(ICombatTarget target)
    {
        if (_observations.TryGetValue(target, out var old))
        {
            if (old.Lifetime == target.LifetimeVersion) return;
            Unsubscribe(old);
            _observations.Remove(target);
        }
        string label = target.Transform.name;
        var observation = new Observation
        {
            Lifetime = target.LifetimeVersion,
            Status = target.RuntimeStatus,
            Life = target.Transform.GetComponent<Unit_Life>()
        };
        observation.StatHandler = (stat, before, after) =>
        {
            if (!CanObserve(target, observation)) return;
            Debug.Log($"[아이템 대상 관찰 / 원인 미구분] {label} / {stat}: {before:0.###} → {after:0.###}", this);
        };
        observation.HealHandler = amount =>
        {
            if (!CanObserve(target, observation)) return;
            Debug.Log($"[아이템 대상 관찰 / 원인 미구분] {label} / 실제 회복 +{amount:0.###} / 현재 체력 {target.CurrentHp:0.###}", this);
        };
        if (observation.Status != null) observation.Status.StatChanged += observation.StatHandler;
        if (observation.Life != null) observation.Life.Healed += observation.HealHandler;
        _observations.Add(target, observation);
    }

    private bool CanObserve(ICombatTarget target, Observation observation)
    {
        return isActiveAndEnabled && !_before.ContainsKey(target) && CombatTargetUtility.Exists(target)
            && target.IsAlive && target.LifetimeVersion == observation.Lifetime;
    }

    private void Update()
    {
        _expired.Clear();
        foreach (var pair in _observations)
            if (!CombatTargetUtility.Exists(pair.Key) || !pair.Key.IsAlive || pair.Key.LifetimeVersion != pair.Value.Lifetime)
                _expired.Add(pair.Key);
        foreach (var target in _expired)
        {
            Unsubscribe(_observations[target]);
            _observations.Remove(target);
        }
    }

    private static void Unsubscribe(Observation observation)
    {
        if (observation.Status != null) observation.Status.StatChanged -= observation.StatHandler;
        if (observation.Life != null) observation.Life.Healed -= observation.HealHandler;
    }

    private sealed class TargetState
    {
        public readonly string Name;
        public readonly int Lifetime;
        public readonly float Hp, Shield;
        public readonly Dictionary<UnitStatType, float> Stats = new();

        public TargetState(ICombatTarget target)
        {
            Name = target.Transform != null ? target.Transform.name : "Unknown";
            Lifetime = target.LifetimeVersion;
            Hp = target.CurrentHp;
            Shield = target.CurrentShield;
            if (target.RuntimeStatus == null) return;
            foreach (UnitStatType stat in Enum.GetValues(typeof(UnitStatType)))
                Stats[stat] = target.RuntimeStatus.GetStat(stat);
        }
    }

    private static void LogChanges(ConsumableItemData item, ICombatTarget target,
        TargetState before, IReadOnlyList<CombatApplicationResult> results)
    {
        var log = new StringBuilder($"[아이템 효과] {item.DisplayName} ({item.Id}) → {before.Name}\n");
        if (results.Count == 0) log.AppendLine("반환된 적용 결과 없음 (중단 또는 예외 여부는 Console 참고).");
        foreach (CombatApplicationResult result in results)
        {
            if (result == null) continue;
            log.Append($"결과: {result.Kind} / {result.Status}");
            switch (result.Kind)
            {
                case CombatApplicationKind.Damage:
                    log.Append($" / 체력 피해 {result.HpDamage:0.###}, 보호막 흡수 {result.ShieldAbsorbed:0.###}");
                    break;
                case CombatApplicationKind.Heal:
                    log.Append($" / 실제 회복 {result.HealedAmount:0.###}");
                    break;
                case CombatApplicationKind.Shield:
                    log.Append($" / 보호막 추가 {result.ShieldAdded:0.###}");
                    break;
            }
            if (!string.IsNullOrEmpty(result.FailureReason)) log.Append($" / {result.FailureReason}");
            log.AppendLine();
        }

        // 사망 후 풀 반환/재사용된 유닛의 초기화 값을 적용 후 스탯으로 비교하지 않습니다.
        if (!CombatTargetUtility.Exists(target) || target.LifetimeVersion != before.Lifetime)
        {
            log.Append("대상 제거 또는 생명주기 변경으로 전후 비교 생략. 위 적용 결과를 확인하세요.");
        }
        else
        {
            bool changed = AppendChange(log, "체력", before.Hp, target.CurrentHp);
            changed |= AppendChange(log, "보호막", before.Shield, target.CurrentShield);
            if (target.RuntimeStatus != null)
                foreach (var stat in before.Stats)
                    changed |= AppendChange(log, stat.Key.ToString(), stat.Value, target.RuntimeStatus.GetStat(stat.Key));
            if (!changed) log.AppendLine("즉시 변경된 체력·보호막·스탯 없음 (지속 효과 등록/시간 갱신은 위 결과 참고).");
        }
        Debug.Log(log.ToString(), item);
    }

    private static bool AppendChange(StringBuilder log, string label, float before, float after)
    {
        if (Mathf.Approximately(before, after)) return false;
        log.AppendLine($"{label}: {before:0.###} → {after:0.###} (변화량 {after - before:+0.###;-0.###;0})");
        return true;
    }
}
