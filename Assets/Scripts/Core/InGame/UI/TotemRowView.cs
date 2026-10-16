using System;
using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>토템 한 개의 이름과 보상 보너스를 표시한다.</summary>
    public sealed class TotemRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _bonus;

        public bool HasReferences => _name != null && _bonus != null;

        public void Bind(ActiveTotem totem)
        {
            if (!HasReferences)
                throw new InvalidOperationException("토템 행의 텍스트 참조가 필요합니다.");
            if (totem.Data == null)
                throw new ArgumentException("표시할 토템 데이터가 없습니다.", nameof(totem));

            _name.text = totem.Data.SelectionMode == TotemSelectionMode.Toggle
                ? totem.Data.DisplayName
                : $"{totem.Data.DisplayName} ({totem.Level}단)";
            _bonus.text = $"+{totem.Data.GetRewardBonusPercent(totem.Level)}%";
        }
    }
}
