using UnityEngine;

namespace Game.UI
{
    /// <summary>표시되는 행동 수만큼 정보 창을 줄인다. 건설 조건이나 거래 상태는 판단하지 않는다.</summary>
    public sealed class PlayerBuildingPopupLayout : MonoBehaviour
    {
        [SerializeField] private PlayerPopup _popup;
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
