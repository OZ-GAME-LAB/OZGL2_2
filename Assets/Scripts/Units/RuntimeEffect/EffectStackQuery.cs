using System;
using UnityEngine;

// 효과 ID 또는 Category로 스택을 조회하며 실제 소비는 대상 API에 요청한다.
namespace Units.Effects
{
    public enum EffectStackQueryKind
    {
        EffectId,
        Category
    }
    [Serializable]
    public sealed class EffectStackQuery
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private EffectStackQueryKind _kind;

        [SerializeField]
        private string _key;

        // ============================================================
        // Properties
        // ============================================================

        public EffectStackQueryKind Kind => _kind;

        public string Key => _key;

        public bool IsValid => !string.IsNullOrWhiteSpace(_key);

        // ============================================================
        // Constructor
        // ============================================================

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

        public bool Matches(EffectDefinitionSnapshot data) => IsValid && data != null && (_kind == EffectStackQueryKind.EffectId ? data.EffectId == _key : data.HasCategory(_key));

        public bool Matches(EffectData data) => IsValid && data != null && (_kind == EffectStackQueryKind.EffectId ? data.EffectId == _key : data.HasCategory(_key));
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
