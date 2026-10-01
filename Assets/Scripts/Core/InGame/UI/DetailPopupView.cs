using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    public sealed class DetailPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Image _icon;

        public void SetContent(string title, string subtitle, string description, Sprite icon = null)
        {
            if (_title != null) _title.text = title ?? string.Empty;
            if (_subtitle != null) _subtitle.text = subtitle ?? string.Empty;
            if (_description != null) _description.text = description ?? string.Empty;
            if (_icon != null) { _icon.sprite = icon; _icon.enabled = icon != null; }
        }
    }
}
