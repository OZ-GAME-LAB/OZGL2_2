using TMPro;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>전달된 유물 정보를 표시하고, 등록된 상세 화면을 현재 팝업 위에 연다.</summary>
    public sealed class ArtifactPopupView : MonoBehaviour
    {
        [SerializeField] private UIScreen _screen;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private UIIcon _emptyIcon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _rarityText;
        [SerializeField] private TMP_Text _effectText;
        [SerializeField] private TMP_Text _loreText;

        private readonly Vector3[] _panelCorners = new Vector3[4];

        public bool Open(ArtifactData artifact, Vector2 screenPosition)
        {
            if (artifact == null || !isActiveAndEnabled || _screen == null || _panel == null ||
                _nameText == null || _rarityText == null || _effectText == null || _screen.Manager == null)
                return false;
            InGameUIManager manager = _screen.Manager;
            if (!manager.IsReady || !manager.TryGetScreen(_screen.Id, out UIScreen registered) ||
                registered != _screen) return false;
            // 이미 다른 팝업 아래에 열린 화면은 OpenPopup으로 앞으로 이동하지 않는다.
            if (_screen.IsVisible && manager.TopPopup != _screen) return false;
            Bind(artifact);
            if (!manager.OpenPopup(_screen.Id)) return false;
            Canvas.ForceUpdateCanvases();
            PositionPanel(screenPosition);
            return true;
        }

        public void Bind(ArtifactData artifact)
        {
            if (artifact == null) return;
            _nameText.text = artifact.DisplayName;
            _rarityText.text = ArtifactRewardView.RarityName(artifact.Rarity);
            _rarityText.color = ArtifactRewardView.RarityColor(artifact.Rarity);
            _effectText.text = string.IsNullOrWhiteSpace(artifact.Description)
                ? "효과 설명 미등록" : artifact.Description.Replace("\\n", "\n");
            if (_icon != null)
            {
                _icon.sprite = artifact.Icon;
                _icon.enabled = artifact.Icon != null;
            }
            if (_emptyIcon != null) _emptyIcon.gameObject.SetActive(artifact.Icon == null);
            // ArtifactData에는 로어가 없으므로 해당 영역은 표시하지 않는다.
            if (_loreText != null)
            {
                _loreText.text = string.Empty;
                _loreText.gameObject.SetActive(false);
            }
        }

        private void PositionPanel(Vector2 screenPosition)
        {
            if (!(_panel.parent is RectTransform parent)) return;
            Canvas canvas = _panel.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera,
                    out Vector2 localPoint)) return;
            _panel.localPosition = new Vector3(localPoint.x, localPoint.y, _panel.localPosition.z);

            _panel.GetWorldCorners(_panelCorners);
            Vector2 minimum = parent.InverseTransformPoint(_panelCorners[0]);
            Vector2 maximum = minimum;
            foreach (Vector3 corner in _panelCorners)
            {
                Vector2 point = parent.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            Rect area = parent.rect;
            float xOffset = maximum.x - minimum.x > area.width
                ? area.center.x - (minimum.x + maximum.x) * .5f
                : Mathf.Max(0, area.xMin - minimum.x) + Mathf.Min(0, area.xMax - maximum.x);
            float yOffset = maximum.y - minimum.y > area.height
                ? area.center.y - (minimum.y + maximum.y) * .5f
                : Mathf.Max(0, area.yMin - minimum.y) + Mathf.Min(0, area.yMax - maximum.y);
            _panel.localPosition += new Vector3(xOffset, yOffset, 0);
        }
    }
}
