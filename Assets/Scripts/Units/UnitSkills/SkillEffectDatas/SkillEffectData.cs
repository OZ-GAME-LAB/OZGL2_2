using System;
using System.Collections.Generic;
using UnityEngine;

namespace Units.Skills
{
    [Serializable]
    public abstract class SkillEffectData
    {
        [SerializeField, Tooltip("실제 효과 적용 성공 시 대상에서 재생하는 FX. 독립 재생을 사용합니다.")]
        private List<SkillFXEntry> _applicationFX = new();
        public IReadOnlyList<SkillFXEntry> ApplicationFX => _applicationFX;
    }
}