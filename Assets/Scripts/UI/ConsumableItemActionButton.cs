using System;
using Game.UI.InGame;
using UnityEngine;

public class ConsumableItemActionButton : MonoBehaviour
{
    public event Action UseRequested;
    public event Action RemoveRequested;

    [SerializeField] private UIScreen _ui;
    [SerializeField] private UnityEngine.UI.Button _useButton;
    [SerializeField] private UnityEngine.UI.Button _RemoveButton;

    private RectTransform _rectTransform;
    private readonly Vector3[] _panelCorners = new Vector3[4];

    public UIScreen Screen => _ui;
    public bool IsOpen => _ui != null && _ui.Manager != null && _ui.IsVisible &&
        _ui.Manager.TopPopup == _ui;
    public bool IsReady
    {
        get
        {
            CacheReferences();
            return enabled && _ui != null && !_ui.IsHud && _useButton != null &&
                _RemoveButton != null && _rectTransform != null &&
                _rectTransform.parent is RectTransform && _ui.Manager != null &&
                _ui.Manager.IsReady && _ui.Manager.TryGetScreen(_ui.Id, out UIScreen registered) &&
                registered == _ui;
        }
    }

    private void Awake() => CacheReferences();

    private void CacheReferences()
    {
        if (_ui == null) _ui = GetComponent<UIScreen>();
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (_useButton != null) _useButton.onClick.AddListener(UseAction);
        if (_RemoveButton != null) _RemoveButton.onClick.AddListener(RemoveAction);
    }

    private void OnDisable()
    {
        if (_useButton != null) _useButton.onClick.RemoveListener(UseAction);
        if (_RemoveButton != null) _RemoveButton.onClick.RemoveListener(RemoveAction);
    }

    public bool Open(RectTransform slotTransform)
    {
        if (slotTransform == null || !IsReady) return false;
        InGameUIManager manager = _ui.Manager;
        if ((_ui.IsVisible && manager.TopPopup != _ui) ||
            (manager.HasBlockingPopup && manager.TopPopup != _ui)) return false;
        if (!manager.OpenPopup(_ui.Id)) return false;

        RectTransform content = _ui.Root != null ? _ui.Root.GetComponent<RectTransform>() : null;
        if (content != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (PositionPopup(slotTransform)) return true;
        Close(UICloseReason.ContextLost);
        return false;
    }

    public void Close(UICloseReason reason = UICloseReason.ContextLost)
    {
        if (_ui != null && _ui.Manager != null) _ui.Manager.ClosePopup(_ui.Id, reason);
    }

    private bool PositionPopup(RectTransform slotTransform)
    {
        if (!(_rectTransform.parent is RectTransform parent)) return false;
        Canvas sourceCanvas = slotTransform.GetComponentInParent<Canvas>();
        if (sourceCanvas != null) sourceCanvas = sourceCanvas.rootCanvas;
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera : null;
        Vector3 belowSlot = slotTransform.TransformPoint(new Vector3(slotTransform.rect.center.x,
            slotTransform.rect.yMin, 0));
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(sourceCamera, belowSlot);

        Canvas destinationCanvas = _rectTransform.GetComponentInParent<Canvas>();
        if (destinationCanvas != null) destinationCanvas = destinationCanvas.rootCanvas;
        Camera destinationCamera = destinationCanvas != null &&
            destinationCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? destinationCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition,
                destinationCamera, out Vector2 localPoint)) return false;
        _rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, _rectTransform.localPosition.z);

        _rectTransform.GetWorldCorners(_panelCorners);
        Vector2 minimum = parent.InverseTransformPoint(_panelCorners[0]);
        Vector2 maximum = minimum;
        foreach (Vector3 corner in _panelCorners)
        {
            Vector2 point = parent.InverseTransformPoint(corner);
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }
        // 팝업의 위쪽 중앙을 클릭한 슬롯 아래에 맞춘다.
        Vector2 alignment = new Vector2(localPoint.x - (minimum.x + maximum.x) * .5f,
            localPoint.y - maximum.y);
        minimum += alignment;
        maximum += alignment;
        Rect area = parent.rect;
        float xOffset = maximum.x - minimum.x > area.width
            ? area.center.x - (minimum.x + maximum.x) * .5f
            : Mathf.Max(0, area.xMin - minimum.x) + Mathf.Min(0, area.xMax - maximum.x);
        float yOffset = maximum.y - minimum.y > area.height
            ? area.center.y - (minimum.y + maximum.y) * .5f
            : Mathf.Max(0, area.yMin - minimum.y) + Mathf.Min(0, area.yMax - maximum.y);
        _rectTransform.localPosition += new Vector3(alignment.x + xOffset, alignment.y + yOffset, 0);
        return true;
    }

    private void UseAction()
    {
        if (isActiveAndEnabled && IsOpen) UseRequested?.Invoke();
    }

    private void RemoveAction()
    {
        if (isActiveAndEnabled && IsOpen) RemoveRequested?.Invoke();
    }
}
