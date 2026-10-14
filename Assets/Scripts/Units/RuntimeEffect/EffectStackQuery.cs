using System;
using UnityEngine;

// 상태이상 또는 효과 ID로 스택을 조회하며 실제 소비는 대상 API에 요청한다.
namespace Units.Effects
{
    public enum EffectStackQueryKind
    {
        EffectId = 0,
        Status = 2
    }
    [Serializable]
    public sealed class EffectStackQuery
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private EffectStackQueryKind _kind = EffectStackQueryKind.Status;

        [SerializeField]
        private string _key;

        [SerializeField]
        private UnitStatusEffectType _statusType;

        // ============================================================
        // Properties
        // ============================================================

        public EffectStackQueryKind Kind => _kind;

        public string Key => _key;

        public UnitStatusEffectType StatusType => _statusType;

        public bool IsValid => _kind switch
        {
            EffectStackQueryKind.EffectId => !string.IsNullOrWhiteSpace(_key),
            EffectStackQueryKind.Status => Enum.IsDefined(typeof(UnitStatusEffectType), _statusType),
            _ => false
        };

        // ============================================================
        // Constructor
        // ============================================================

        public EffectStackQuery(UnitStatusEffectType statusType)
        {
            _kind = EffectStackQueryKind.Status;
            _statusType = statusType;
        }

        public EffectStackQuery()
        {
        }

        public EffectStackQuery(
            EffectStackQueryKind kind,
            string key)
        {
            _kind = kind;

            _key = key;
        }

        // ============================================================
        // Execution
        // ============================================================

        public bool Matches(EffectDefinitionSnapshot data) => IsValid && data != null && (_kind switch
        {
            EffectStackQueryKind.EffectId => data.EffectId == _key,
            EffectStackQueryKind.Status => data.HasStatus(_statusType),
            _ => false
        });

        public bool Matches(EffectData data) => IsValid && data != null && (_kind switch
        {
            EffectStackQueryKind.EffectId => data.EffectId == _key,
            EffectStackQueryKind.Status => data.HasStatus(_statusType),
            _ => false
        });
    }

    public readonly struct EffectStackConsumeRequest
    {

        // ============================================================
        // Properties
        // ============================================================

        public CombatTargetSnapshot Target { get; }

        public EffectStackQuery Query { get; }

        public int Count { get; }

        public CombatEventMetadata Metadata { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public EffectStackConsumeRequest(
            ICombatTarget target,
            EffectStackQuery query,
            int count,
            CombatEventMetadata metadata)
        {
            Target = new CombatTargetSnapshot(target);

            Query = query;

            Count = count;

            Metadata = metadata;
        }
    }
}
