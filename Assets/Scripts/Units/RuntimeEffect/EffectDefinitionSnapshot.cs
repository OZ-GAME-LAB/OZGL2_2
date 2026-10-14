using System.Collections.Generic;

// SO 신원을 유지하면서 적용에 필요한 효과 설정을 변경되지 않는 복사본으로 보관한다.
namespace Units.Effects
{
    // SO 신원(Data)과 적용된 정의를 분리해 발사 이후 편집/갱신이 비행 중 효과를 바꾸지 않게 한다.
    public sealed class EffectDefinitionSnapshot
    {

        // ============================================================
        // Properties
        // ============================================================

        public string EffectId { get; }

        public EffectAlignment Alignment { get; }

        public EffectDurationType DurationType { get; }

        public float Duration { get; }

        public EffectStackType StackType { get; }

        public int MaxStack { get; }

        public IReadOnlyList<EffectActionData> Actions { get; }

        public IReadOnlyList<string> Categories { get; }

        // ============================================================
        // Constructor
        // ============================================================

        public EffectDefinitionSnapshot(EffectData data)
        {
            EffectId = data.EffectId;

            Alignment = data.Alignment;

            DurationType = data.DurationType;

            Duration = data.Duration;

            StackType = data.StackType;

            MaxStack = data.MaxStack;

            Actions = SkillDefinitionCopy.Copy(new List<EffectActionData>(data.Actions)).AsReadOnly();

            Categories = new List<string>(data.Categories).AsReadOnly();
        }

        // ============================================================
        // Execution
        // ============================================================

        // 상태 분류는 문자열 태그나 EffectId가 아닌 상태 부여 Action의 Enum으로 판정한다.
        public bool HasStatus(UnitStatusEffectType statusType)
        {
            foreach (var action in Actions)
                if (action is StatusEffectActionData status && status.StatusType == statusType)
                    return true;

            return false;
        }

        public bool HasCategory(string category)
        {
            foreach (var value in Categories)
                if (value == category)
                    return true;

            return false;
        }
    }
}
