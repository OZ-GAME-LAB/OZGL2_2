using System;
using System.Collections.Generic;
using UnityEngine;
using Units.Effects;
using Units.FX;

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
        StopEmission = 0,
        // 기존 저장 값 1은 유지한다. 신규 설정에서는 사용하지 않는다.
        KeepActive = 1,
        Independent = 2,
        ClearImmediately = 3
    }
    // Legacy는 기존 완료·중단 Hook의 호환성을 보존하기 위한 고급 설정이다.
    public enum SkillFXOperation
    {
        Legacy = 0, Cast = 1, Dash = 2, ProjectileFlight = 3,
        Action = 4, Hit = 5, Collision = 6, Fire = 7, Expire = 8
    }

    public enum SkillFXScaleMode { Catalog, Multiplier, AttackRadius }

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
        private SkillFXOperation _operation;

        [SerializeField]
        private SkillFXEndPolicy _endPolicy;

        // ============================================================
        // Properties
        // ============================================================

        [SerializeField, Tooltip("VFX 또는 SFX 카탈로그를 선택합니다.")]
        private FXKind _kind;

        [SerializeField, Tooltip("월드 고정 또는 추적할 대상을 선택합니다.")]
        private FXAttachment _attachment;

        [SerializeField, Tooltip("위치 추적과 별도로 대상의 방향 변화를 반영합니다.")]
        private bool _followDirection;

        [SerializeField] private SkillFXScaleMode _scaleMode;
        [SerializeField] private Vector2 _scaleMultiplier = Vector2.one;
        public SkillFXScaleMode ScaleMode => _scaleMode;
        public Vector2 ScaleMultiplier => _scaleMultiplier;

        public Vector2 ResolveScale(float attackRadius, float referenceRadius)
        {
            // 피격/비행은 스킬과 무관한 공유 크기를 유지한다.
            if (Hook == SkillFXHook.OnHit || Hook == SkillFXHook.Flight
                || _scaleMode == SkillFXScaleMode.Catalog) return Vector2.one;
            float ratio = _scaleMode == SkillFXScaleMode.AttackRadius && attackRadius > 0f
                ? attackRadius / Mathf.Max(0.01f, referenceRadius) : 1f;
            return new Vector2(Mathf.Max(0.01f, _scaleMultiplier.x), Mathf.Max(0.01f, _scaleMultiplier.y)) * ratio;
        }

        public FXKind Kind => _kind;
        public FXAttachment Attachment => _attachment;
        public bool FollowDirection => _followDirection;

        public SkillFXEntry() { ApplyDefaults(SkillFXOperation.Action); }

        public SkillFXOperation Operation => _operation;

        // 명시적으로 동작을 선택할 때만 초기화한다. 개별 수정값은 재생 중 덮어쓰지 않는다.
        public void ApplyDefaults(SkillFXOperation operation)
        {
            _operation = operation;
            _attachment = operation == SkillFXOperation.ProjectileFlight ? FXAttachment.Projectile
                : operation == SkillFXOperation.Hit ? FXAttachment.Target
                : operation == SkillFXOperation.Cast || operation == SkillFXOperation.Dash ? FXAttachment.Owner
                : FXAttachment.World;
            _followDirection = operation == SkillFXOperation.ProjectileFlight || operation == SkillFXOperation.Dash
                || operation == SkillFXOperation.Hit;
            _endPolicy = operation == SkillFXOperation.ProjectileFlight ? SkillFXEndPolicy.ClearImmediately
                : operation == SkillFXOperation.Cast || operation == SkillFXOperation.Dash ? SkillFXEndPolicy.StopEmission
                : SkillFXEndPolicy.Independent;
        }

        public SkillFXEntry(string key, SkillFXHook hook, SkillFXEndPolicy endPolicy = SkillFXEndPolicy.Independent,
            FXKind kind = FXKind.VFX, FXAttachment? attachment = null, bool? followDirection = null)
        {
            _key = key;
            _hook = hook;
            _endPolicy = endPolicy;
            _kind = kind;
            _attachment = attachment ?? (hook == SkillFXHook.OnHit ? FXAttachment.Target : FXAttachment.World);
            _followDirection = followDirection ?? hook == SkillFXHook.OnHit;
        }

        public string Key => _key;

        public SkillFXHook Hook => _operation switch
        {
            SkillFXOperation.Cast => SkillFXHook.Cast,
            SkillFXOperation.Dash => SkillFXHook.Dash,
            SkillFXOperation.ProjectileFlight => SkillFXHook.Flight,
            SkillFXOperation.Action => SkillFXHook.OnStart,
            SkillFXOperation.Hit => SkillFXHook.OnHit,
            SkillFXOperation.Collision => SkillFXHook.Collision,
            SkillFXOperation.Fire => SkillFXHook.Fire,
            SkillFXOperation.Expire => SkillFXHook.Expire,
            _ => _hook
        };

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
        public FXScope Scope { get; }
        public Vector2 Direction { get; }
        public Vector2 FacingDirection { get; }
        public CombatTargetSnapshot Target { get; }
        public Transform FollowTarget { get; }
        public FXCleanupReason CleanupReason { get; }
        public float AttackRadius { get; }

        public SkillFXRequest AsCleanup(FXCleanupReason reason = FXCleanupReason.ActionEnded)
            => new SkillFXRequest(Entry, Metadata, Position, true, Direction, Target, FollowTarget, Scope, reason, FacingDirection, AttackRadius);

        // ============================================================
        // Constructor
        // ============================================================

        public SkillFXRequest(
            SkillFXEntry entry,
            CombatEventMetadata metadata,
            Vector2 position,
            bool cleanup = false,
            Vector2 direction = default,
            CombatTargetSnapshot target = default,
            Transform followTarget = null,
            FXScope? scope = null,
            FXCleanupReason cleanupReason = FXCleanupReason.ActionEnded,
            Vector2 facingDirection = default,
            float attackRadius = 0f)
        {
            Entry = entry;
            AttackRadius = Mathf.Max(0f, attackRadius);

            Metadata = metadata;

            Position = position;

            IsCleanup = cleanup;
            Scope = scope ?? new FXScope(metadata);
            Target = target;
            FollowTarget = followTarget;
            CleanupReason = cleanupReason;
            var facing = facingDirection.sqrMagnitude > 0.0001f ? facingDirection
                : metadata.Owner.MatchesLifetime ? metadata.Owner.Target.FacingDirection : Vector2.left;
            FacingDirection = facing.x < 0f ? Vector2.left : Vector2.right;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : FacingDirection;
        }
    }
}
