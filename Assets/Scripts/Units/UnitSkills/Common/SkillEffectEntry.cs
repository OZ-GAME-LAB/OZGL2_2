using System;
using System.Collections.Generic;
using UnityEngine;
using Units.Effects;

// 효과의 시점·대상·빈도와 조건부 소비 설정, FX 요청 계약을 정의한다.
namespace Units.Skills
{
    public enum SkillEffectTiming
    {
        OnStart,
        OnHit,
        OnComplete
    }
    public enum SkillEffectOrder
    {
        BeforeBase,
        AfterBase
    }
    public enum SkillEffectSubject
    {
        HitTarget,
        Self,
        SnapshotArea
    }
    public enum SkillEffectFrequency
    {
        PerTarget,
        OncePerExecution
    }

    [Serializable]
    public class SkillEffectEntry
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField, HideInInspector]
        private int _entryId;

        [SerializeField]
        private SkillEffectTiming _timing = SkillEffectTiming.OnHit;

        [SerializeField]
        private SkillEffectSubject _subject;

        [SerializeField]
        private SkillEffectFrequency _frequency;

        [SerializeField]
        private SkillTargetSettings _snapshotAreaTarget = new(
            SkillTargetSource.Search,
            SkillTargetRelation.Hostile,
            false,
            1f,
            10,
            1f
        );

        [SerializeReference]
        private List<SkillEffectData> _effects = new();

        // ============================================================
        // Properties
        // ============================================================

        public int EntryId => _entryId;

        public SkillEffectTiming Timing => _timing;

        public SkillEffectSubject Subject => _subject;

        public SkillEffectFrequency Frequency => _frequency;

        public SkillTargetSettings SnapshotAreaTarget => _snapshotAreaTarget;

        public IReadOnlyList<SkillEffectData> Effects => _effects;

        // ============================================================
        // Execution
        // ============================================================

        internal void SetEntryId(int id)
        {
            _entryId = id;
        }

        // ============================================================
        // Constructor
        // ============================================================

        public SkillEffectEntry()
        {
        }

        public SkillEffectEntry(
            int id,
            SkillEffectTiming timing,
            IEnumerable<SkillEffectData> effects,
            SkillEffectSubject subject = SkillEffectSubject.HitTarget,
            SkillEffectFrequency frequency = SkillEffectFrequency.PerTarget)
        {
            _entryId = id;

            _timing = timing;

            _effects = new(effects);

            _subject = subject;

            _frequency = frequency;
        }
    }

    [Serializable]
    public sealed class SkillConditionalEffectEntry : SkillEffectEntry
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private SkillEffectOrder _order = SkillEffectOrder.AfterBase;

        [SerializeReference]
        private List<SkillConditionData> _conditions = new();

        [SerializeField]
        private bool _consumeStacks;

        [SerializeField]
        private SkillConditionSubject _consumeSubject = SkillConditionSubject.Target;

        [SerializeField]
        private EffectStackQuery _stackQuery = new();

        [SerializeField, Min(1)]
        private int _stackCount = 1;

        [SerializeField]
        private List<SkillFXEntry> _fxEntries = new();

        // ============================================================
        // Properties
        // ============================================================

        public SkillEffectOrder Order => _order;

        public IReadOnlyList<SkillConditionData> Conditions => _conditions;

        // ============================================================
        // Execution
        // ============================================================

        internal void FreezeConditions(SkillConditionContext context)
        {
            _conditions = new List<SkillConditionData>(CombatSourceSnapshot.FreezeConditions(_conditions, context));
        }

        // ============================================================
        // Properties
        // ============================================================

        public bool ConsumeStacks => _consumeStacks;

        public SkillConditionSubject ConsumeSubject => _consumeSubject;

        public EffectStackQuery StackQuery => _stackQuery;

        public int StackCount => _stackCount;

        public IReadOnlyList<SkillFXEntry> FXEntries => _fxEntries;
    }

    public enum SkillFXHook
    {
        OnStart,
        OnHit,
        OnComplete,
        OnInterrupted,
        OnFailed,
        Cast,
        Dash,
        Fire,
        Flight,
        Collision,
        Expire
    }
    public enum SkillFXEndPolicy
    {
        Stop,
        KeepActive,
        Independent
    }
    [Serializable]
    public sealed class SkillFXEntry
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        [SerializeField]
        private string _key;

        [SerializeField]
        private SkillFXHook _hook;

        [SerializeField]
        private SkillFXEndPolicy _endPolicy;

        // ============================================================
        // Properties
        // ============================================================

        public string Key => _key;

        public SkillFXHook Hook => _hook;

        public SkillFXEndPolicy EndPolicy => _endPolicy;
    }
    public readonly struct SkillFXRequest
    {

        // ============================================================
        // Properties
        // ============================================================

        public SkillFXEntry Entry { get; }

        public CombatEventMetadata Metadata { get; }

        public Vector2 Position { get; }

        public bool IsCleanup { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public SkillFXRequest(
            SkillFXEntry entry,
            CombatEventMetadata metadata,
            Vector2 position,
            bool cleanup = false)
        {
            Entry = entry;

            Metadata = metadata;

            Position = position;

            IsCleanup = cleanup;
        }
    }
}
