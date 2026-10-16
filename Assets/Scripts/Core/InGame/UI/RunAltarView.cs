using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>선택한 제단의 기존 비주얼과 설명만 표시한다.</summary>
    public sealed class RunAltarView : MonoBehaviour
    {
        [SerializeField] private AltarButtonSlot[] _slots;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _description;

        public void Show(AltarData altar)
        {
            foreach (AltarButtonSlot slot in _slots)
            {
                bool selected = altar != null && slot.Id == altar.Id;
                slot.Button.gameObject.SetActive(selected);
                slot.Button.SetSelected(selected);
            }
            _title.text = altar != null ? altar.DisplayName : "선택한 제단 없음";
            _description.text = altar != null ? altar.Description : "이번 판에 적용된 제단이 없습니다.";
        }
    }
}
