using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>제단 고정 영역과 두 스크롤 목록을 표시합니다.</summary>
    public sealed class RunInfoView : MonoBehaviour
    {
        [SerializeField] private UIScreen _screen;
        [Header("Altar")]
        [SerializeField] private Image _altarIcon;
        [SerializeField] private TMP_Text _altarName;
        [SerializeField] private TMP_Text _altarEffect;
        [Header("Shared Row")]
        [SerializeField] private RunInfoRow _rowPrefab;
        [Header("Traits")]
        [SerializeField] private ScrollRect _traitScroll;
        [SerializeField] private TMP_Text _emptyTraits;
        [Header("Totems")]
        [SerializeField] private ScrollRect _totemScroll;
        [SerializeField] private TMP_Text _emptyTotems;

        private readonly List<RunInfoRow> _traitRows = new List<RunInfoRow>();
        private readonly List<RunInfoRow> _totemRows = new List<RunInfoRow>();
        public UIScreen Screen => _screen;

        public bool ValidateReferences() => _screen != null && _altarIcon != null &&
            _altarName != null && _altarEffect != null && _rowPrefab != null &&
            _rowPrefab.ValidateReferences() && _traitScroll != null && _totemScroll != null &&
            _traitScroll.content != null && _totemScroll.content != null &&
            _traitScroll.content != _totemScroll.content && _emptyTraits != null &&
            _emptyTotems != null;

        public void Show(RunInfoRowData altar, IReadOnlyList<RunInfoRowData> traits,
            IReadOnlyList<RunInfoRowData> totems)
        {
            _altarIcon.sprite = altar.Icon;
            _altarIcon.enabled = altar.Icon != null;
            _altarName.text = altar.Name;
            _altarEffect.text = altar.Effect;
            ShowRows(_traitRows, _traitScroll.content, traits);
            ShowRows(_totemRows, _totemScroll.content, totems);
            _emptyTraits.text = "적용된 특성이 없습니다.";
            _emptyTraits.gameObject.SetActive(traits.Count == 0);
            _emptyTotems.text = "활성화된 토템이 없습니다.";
            _emptyTotems.gameObject.SetActive(totems.Count == 0);
        }

        private void ShowRows(List<RunInfoRow> rows, RectTransform content,
            IReadOnlyList<RunInfoRowData> data)
        {
            for (int i = 0; i < data.Count; i++)
            {
                if (i == rows.Count) rows.Add(Instantiate(_rowPrefab, content));
                rows[i].Show(data[i]);
                rows[i].gameObject.SetActive(true);
            }
            for (int i = data.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        }

        // 팝업 활성화 후 호출해야 첫 표시의 레이아웃도 반영됩니다.
        public void ResetScrollPositions()
        {
            Canvas.ForceUpdateCanvases();
            ResetScroll(_traitScroll);
            ResetScroll(_totemScroll);
        }

        private static void ResetScroll(ScrollRect scroll)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }
}
