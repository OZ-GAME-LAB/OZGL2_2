using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.QA
{
    public sealed class QAArenaInput : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private QABattleController _controller;
        [SerializeField] private UnityEngine.UI.RawImage _view;
        public void Configure(QABattleController controller, UnityEngine.UI.RawImage view) { _controller = controller; _view = view; }
        public void OnPointerClick(PointerEventData data)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.rectTransform, data.position, data.pressEventCamera, out var local)) return;
            var rect = _view.rectTransform.rect;
            var viewport = new Vector3((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height, -_controller.BattleCamera.transform.position.z);
            var point = (Vector2)_controller.BattleCamera.ViewportToWorldPoint(viewport);
            var selected = _controller.Entries.Where(e => e.Unit != null && e.Unit.gameObject.activeInHierarchy)
                .OrderBy(e => Vector2.Distance(e.Unit.transform.position, point)).FirstOrDefault();
            if (selected != null && Vector2.Distance(selected.Unit.transform.position, point) < .9f) _controller.Select(selected.Id);
            else _controller.PlaceSelected(point);
        }
    }
}
