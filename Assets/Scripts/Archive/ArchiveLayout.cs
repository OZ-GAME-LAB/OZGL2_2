using UnityEngine;

namespace Game.Archive
{
    // 카드 높이로 폭을 정하고, 남는 가로 공간을 목록에 배분한다.
    public sealed class ArchiveLayout : UnityEngine.UI.LayoutGroup
    {
        [SerializeField] private RectTransform _tabs;
        [SerializeField] private RectTransform _progress;
        [SerializeField] private RectTransform _entries;
        [SerializeField] private RectTransform _hint;
        [SerializeField] private RectTransform _details;

        private const float Side = 48;
        private const float Gap = 32;
        private const float Top = 136;
        private const float Bottom = 84;
        private float CardHeight => rectTransform.rect.height - Top - Bottom;

        public override void CalculateLayoutInputVertical() { }

        public override void SetLayoutHorizontal()
        {
            float cardWidth = CardHeight * 5f / 7f;
            float listWidth = rectTransform.rect.width - Side * 2 - Gap - cardWidth;
            SetChildAlongAxis(_tabs, 0, Side, listWidth);
            SetChildAlongAxis(_entries, 0, Side, listWidth);
            SetChildAlongAxis(_hint, 0, Side, listWidth);
            SetChildAlongAxis(_progress, 0, Side + listWidth - 280, 280);
            SetChildAlongAxis(_details, 0, Side + listWidth + Gap, cardWidth);
        }

        public override void SetLayoutVertical()
        {
            float height = rectTransform.rect.height;
            SetChildAlongAxis(_tabs, 1, Top, 56);
            SetChildAlongAxis(_progress, 1, 208, 38);
            SetChildAlongAxis(_entries, 1, 270, height - 270 - Bottom);
            SetChildAlongAxis(_hint, 1, height - 64, 28);
            SetChildAlongAxis(_details, 1, Top, CardHeight);
        }
    }
}
