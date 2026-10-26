using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>전달받은 표시 정보만 출력하는 공용 목록 행입니다.</summary>
    public sealed class RunInfoRow : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _effectText;

        public bool ValidateReferences() => _icon != null && _nameText != null &&
            _levelText != null && _effectText != null;

        public void Show(RunInfoRowData data)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
            _nameText.text = data.Name ?? string.Empty;
            _levelText.text = data.Level ?? string.Empty;
            _effectText.text = data.Effect ?? string.Empty;
        }
    }

    public readonly struct RunInfoRowData
    {
        public readonly Sprite Icon;
        public readonly string Name, Level, Effect;
        public RunInfoRowData(Sprite icon, string name, string level, string effect)
        {
            Icon = icon;
            Name = name;
            Level = level;
            Effect = effect;
        }
    }
}
