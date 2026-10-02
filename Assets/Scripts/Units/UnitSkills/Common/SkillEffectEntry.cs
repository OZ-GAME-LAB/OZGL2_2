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

        // 투사체에서 조건을 고정한 뒤에도 소비할 상태를 보존하는 실행 전용 정보.
        private bool _consumptionFrozen;
        private bool _frozenConsumeStacks;
        private SkillConditionSubject _frozenConsumeSubject;
        private EffectStackQuery _frozenStackQuery;
        private int _frozenStackCount;

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
            // Owner 조건이 FrozenCondition으로 바뀌기 전에 소비 정보를 보존한다.
            _frozenConsumeStacks = ConsumeStacks;
            _frozenConsumeSubject = ConsumeSubject;
            _frozenStackQuery = StackQuery;
            _frozenStackCount = StackCount;
            _consumptionFrozen = true;
            _conditions = new List<SkillConditionData>(CombatSourceSnapshot.FreezeConditions(_conditions, context));
        }

        // ============================================================
        // Properties
        // ============================================================

        private SkillStackCondition ConsumptionCondition
        {
            get
            {
                SkillStackCondition selected = null;
                foreach (var condition in _conditions)
                {
                    if (condition is not SkillStackCondition stack || !stack.ConsumeOnApply) continue;
                    // 여러 소비를 부분 실행하지 않고 잘못된 설정을 거부한다.
                    if (selected != null) return null;
                    selected = stack;
                }
                return selected;
            }
        }

        public bool ConsumeStacks
        {
            get
            {
                if (_consumptionFrozen) return _frozenConsumeStacks;
                foreach (var condition in _conditions)
                    if (condition is SkillStackCondition stack && stack.ConsumeOnApply) return true;
                return false;
            }
        }

        public SkillConditionSubject ConsumeSubject => _consumptionFrozen ? _frozenConsumeSubject : ConsumptionCondition?.Subject ?? SkillConditionSubject.Target;

        public EffectStackQuery StackQuery => _consumptionFrozen ? _frozenStackQuery : ConsumptionCondition?.Query;

        public int StackCount => _consumptionFrozen ? _frozenStackCount : ConsumptionCondition?.ConsumeCount ?? 0;

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
