using System;
using System.Collections.Generic;
using TMPro;
using Units;
using Units.Skills;
using Units.UnitDatas;
using UnityEngine;

namespace Game.UI.InGame
{
    public sealed class UnitPopupView : MonoBehaviour
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField, Tooltip("유닛 초상화를 표시할 Image. UnitData의 UnitIcon을 사용합니다.")]
        private UnityEngine.UI.Image _unitIcon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _statsText;
        [SerializeField] private GameObject[] _unitRows;
        [SerializeField] private GameObject[] _groupTotals;
        [SerializeField] private Transform _skillContent;
        [SerializeField] private UIItemSlot _skillTemplate;
        [SerializeField] private TMP_Text _emptySkillsText;
        [SerializeField] private UnityEngine.UI.ScrollRect _skillScroll;
        [SerializeField] private SkillPopupView _skillPopup;

        private readonly List<UIItemSlot> _skillSlots = new();

        public void ShowSingle(UnitData unit)
        {
            if (unit == null) return;
            Bind(unit);
            _screen.Manager.OpenPopup(_screen.Id);
            ResetSkillScroll();
        }

        // 도감 등 다른 화면의 내용으로도 사용할 수 있도록 팝업 표시와 바인딩을 분리한다.
        public void Bind(UnitData unit)
        {
            if (unit == null) return;
            if (_unitIcon != null)
            {
                _unitIcon.sprite = unit.UnitIcon;
                _unitIcon.preserveAspect = true;
                _unitIcon.enabled = unit.UnitIcon != null;
            }
            _nameText.text = string.IsNullOrWhiteSpace(unit.UnitName) ? unit.name : unit.UnitName;
            _subtitleText.text = "1마리 기본 정보 · 전투 중 강화 효과 미포함";
            _healthText.text = $"기본 체력 {unit.GetStat(UnitStatType.MaxHp):0.##}";
            _statsText.text =
                $"공격력  {unit.GetStat(UnitStatType.AttackPower):0.##}\n" +
                $"방어력  {unit.GetStat(UnitStatType.Defense):0.##}\n" +
                $"공격 속도 배율  {unit.GetStat(UnitStatType.AttackSpeed):0.##}\n" +
                $"이동 속도  {unit.GetStat(UnitStatType.MoveSpeed):0.##}";

            for (int i = 0; i < _unitRows.Length; i++)
                _unitRows[i].SetActive(i == 0);
            foreach (GameObject total in _groupTotals)
                total.SetActive(false);

            foreach (UIItemSlot slot in _skillSlots)
            {
                slot.Unbind();
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            _skillSlots.Clear();

            foreach (UnitActiveSkillEntry entry in unit.ActiveSkills)
            {
                if (entry == null || entry.Skill == null) continue;
                ActiveSkillData skill = entry.Skill;
                AddSkill(skill.Display, skill.name, "액티브", () => _skillPopup.Show(skill));
            }
            foreach (PassiveSkillData skill in unit.PassiveSkillDatas)
            {
                if (skill == null) continue;
                AddSkill(skill.Display, skill.name, "패시브", () => _skillPopup.Show(skill));
            }

            _emptySkillsText.gameObject.SetActive(_skillSlots.Count == 0);
            ResetSkillScroll();
        }

        private void ResetSkillScroll()
        {
            _skillScroll.StopMovement();
            _skillScroll.verticalNormalizedPosition = 1;
        }

        private void AddSkill(SkillDisplayData display, string fallbackName, string type, Action onClick)
        {
            UIItemSlot slot = Instantiate(_skillTemplate, _skillContent);
            slot.Bind(display.Icon, display.GetName(fallbackName), type + " · 상세 보기", false, true, onClick);
            slot.gameObject.SetActive(true);
            _skillSlots.Add(slot);
        }
    }
}
