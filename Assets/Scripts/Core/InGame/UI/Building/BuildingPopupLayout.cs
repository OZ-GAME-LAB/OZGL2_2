using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>기존 건설 창과 같은 간격으로 표시 중인 행동 행과 창 높이를 정리한다.</summary>
    public sealed class BuildingPopupLayout : MonoBehaviour
    {
        [SerializeField] private UIScreen _popup;
        [SerializeField] private RectTransform[] _rows;
        [SerializeField] private RectTransform _status;
        [SerializeField] private RectTransform _details;
        private int _visibleRows = -1;

        private void LateUpdate()
        {
            int count = 0;
            foreach (var row in _rows)
            {
                if (!row.gameObject.activeSelf) continue;
                row.anchoredPosition = new Vector2(row.anchoredPosition.x, -182 - count * 66);
                count++;
            }

            if (count == _visibleRows) return;
            _visibleRows = count;
            float height = count == 0 ? 290 : 320 + count * 66;
            _popup.SetContentHeights(height, height + 180);
            _status.anchoredPosition = new Vector2(_status.anchoredPosition.x, -182 - count * 66);
            _details.anchoredPosition = new Vector2(_details.anchoredPosition.x, -(height - 70 + 10));
        }
    }
}
