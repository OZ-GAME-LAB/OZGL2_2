using System;
using UnityEngine;

namespace Game.Core
{
    public enum UnlockCondition { TotalDamage, SkillKills, BossQuarter }

    [CreateAssetMenu(menuName = "Progress/Unlock", fileName = "Unlock")]
    public sealed class UnlockData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private bool _defaultUnlocked;
        [SerializeField] private UnlockCondition _condition;
        [SerializeField, Min(1)] private int _requiredValue = 1;
        [SerializeField, TextArea] private string _description;
        public string Id => _id;
        public bool DefaultUnlocked => _defaultUnlocked;
        public string Description => _description;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(_id) || _requiredValue < 1 ||
                !Enum.IsDefined(typeof(UnlockCondition), _condition))
                throw new ArgumentException("해금 정의의 ID, 조건, 기준값을 확인하세요: " + name);
        }

        public bool IsMet(PlayerRecordSaveData records)
        {
            if (_defaultUnlocked) return true;
            switch (_condition)
            {
                case UnlockCondition.TotalDamage: return records.TotalDamage >= _requiredValue;
                case UnlockCondition.SkillKills: return records.SkillKills >= _requiredValue;
                case UnlockCondition.BossQuarter: return records.ClearedBossQuarters.Contains(_requiredValue);
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
