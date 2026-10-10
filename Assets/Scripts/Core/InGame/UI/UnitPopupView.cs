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
            _screen.Manager.OpenPopup(_screen.Id);
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
