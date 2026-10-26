using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>
    /// 정산 화면의 토템 한 줄을 표시합니다.
    /// 프리팹 구조: TotemRow > Horizontal > Icon, Name, Multiplier / TotemRow > Line
    /// Line은 레이아웃용 장식이므로 코드에서 직접 조작하지 않습니다.
    /// </summary>
    public sealed class TotemRowView : MonoBehaviour
    {
        [Header("Totem Row UI")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _bonus;

        // 기존 정산창의 필수 참조 검증은 그대로 유지합니다.
        // 아이콘 참조가 아직 연결되지 않은 상태에서도 기존 이름/배율 출력이 중단되지 않습니다.
        public bool HasReferences => _name != null && _bonus != null;

        public void Bind(ActiveTotem totem)
        {
            if (!HasReferences)
                throw new InvalidOperationException("토템 행의 텍스트 참조가 필요합니다.");
            if (totem.Data == null)
                throw new ArgumentException("표시할 토템 데이터가 없습니다.", nameof(totem));

            // TotemData SO에 이미 정의된 Icon 프로퍼티를 사용합니다.
            if (_icon != null)
            {
                Sprite sprite = totem.Data.Icon;
                _icon.sprite = sprite;
                _icon.enabled = sprite != null; // 빈 Sprite의 흰색 사각형 방지
            }

            _name.text = totem.Data.SelectionMode == TotemSelectionMode.Toggle
                ? totem.Data.DisplayName
                : $"{totem.Data.DisplayName} ({totem.Level}단)";
            _bonus.text = $"+{totem.Data.GetRewardBonusPercent(totem.Level)}%";
        }
    }
}
