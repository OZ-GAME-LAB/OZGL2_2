using TMPro;
using Units.Skills;
using UnityEngine;

namespace Game.UI.InGame
{
    public sealed class SkillPopupView : MonoBehaviour
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _typeText;
        [SerializeField] private TMP_Text _effectText;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private GameObject _emptyIcon;
        [SerializeField] private UnityEngine.UI.ScrollRect _descriptionScroll;

        public void Show(ActiveSkillData skill)
        {
            Show(skill.Display, skill.name, $"액티브 · 재사용 {skill.SkillCooldown:0.##}초 · 사거리 {skill.SkillRange:0.##}");
        }

        public void Show(PassiveSkillData skill)
        {
            Show(skill.Display, skill.name, "패시브");
        }

        private void Show(SkillDisplayData display, string fallbackName, string type)
        {
            _nameText.text = display.GetName(fallbackName);
            _typeText.text = type;
            _effectText.text = display.Description;
            _icon.sprite = display.Icon;
            _icon.enabled = display.Icon != null;
            _emptyIcon.SetActive(display.Icon == null);
            _screen.Manager.OpenPopup(_screen.Id);
            _descriptionScroll.StopMovement();
            _descriptionScroll.verticalNormalizedPosition = 1;
        }
    }
}
