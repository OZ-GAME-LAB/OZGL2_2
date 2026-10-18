using System;
using TMPro;
using UnityEngine;

/// <summary>강조 영역과 설명창만 표시합니다. 기존 화면이나 게임 상태는 변경하지 않습니다.</summary>
public sealed class TutorialOverlayView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private RectTransform[] _dim;
    [SerializeField] private RectTransform _highlight;
    [SerializeField] private UnityEngine.UI.Button _targetButton;
    [SerializeField] private RectTransform _description;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _body;
    [SerializeField] private TMP_Text _progress;
    [SerializeField] private UnityEngine.UI.Button _next;
    [SerializeField] private UnityEngine.UI.Button _skip;
    [SerializeField] private TMP_Text _nextLabel;
    private RectTransform _uiTarget;
    private Collider2D _worldTarget;
    private Camera _worldCamera;
    private readonly Vector3[] _corners = new Vector3[4];

    public bool IsVisible => _root.activeSelf;

    public void Show(string title, string body, string progress, Action next, Action skip,
        RectTransform uiTarget = null, Collider2D worldTarget = null, Action targetClick = null,
        string nextLabel = "다음")
    {
        _root.SetActive(true);
        _title.text = title;
        _body.text = body;
        _progress.text = progress;
        _nextLabel.text = nextLabel;
        Bind(_next, next);
        _next.gameObject.SetActive(next != null);
        Bind(_skip, skip);
        _skip.gameObject.SetActive(skip != null);
        Bind(_targetButton, targetClick);
        _targetButton.interactable = targetClick != null;
        _uiTarget = uiTarget;
        _worldTarget = worldTarget;
        _worldCamera = Camera.main;
        Canvas.ForceUpdateCanvases();
        UpdateLayout();
    }

    public void Hide()
    {
        _root.SetActive(false);
        _uiTarget = null;
        _worldTarget = null;
    }

    private static void Bind(UnityEngine.UI.Button button, Action action)
    {
        button.onClick.RemoveAllListeners();
        button.interactable = action != null;
        if (action != null) button.onClick.AddListener(() => action());
    }

    private void LateUpdate()
    {
        if (IsVisible) UpdateLayout();
    }

    private void UpdateLayout()
    {
        Vector2 size = _canvasRect.rect.size;
        Rect focus = new Rect(size.x * .5f, size.y * .5f, 0, 0);
        bool hasTarget = false;
        if (_uiTarget != null && _uiTarget.gameObject.activeInHierarchy)
        {
            _uiTarget.GetWorldCorners(_corners);
            Canvas canvas = _uiTarget.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            focus = FromScreen(RectTransformUtility.WorldToScreenPoint(camera, _corners[0]),
                RectTransformUtility.WorldToScreenPoint(camera, _corners[2]));
            hasTarget = true;
        }
        else if (_worldTarget != null && _worldCamera != null)
        {
            Bounds b = _worldTarget.bounds;
            Vector3 low = _worldCamera.WorldToScreenPoint(b.min);
            Vector3 high = _worldCamera.WorldToScreenPoint(b.max);
            if (low.z > 0 && high.z > 0)
            {
                focus = FromScreen(low, high);
                hasTarget = true;
            }
        }
        if (hasTarget)
            focus = Rect.MinMaxRect(Mathf.Clamp(focus.xMin - 10, 0, size.x), Mathf.Clamp(focus.yMin - 10, 0, size.y),
                Mathf.Clamp(focus.xMax + 10, 0, size.x), Mathf.Clamp(focus.yMax + 10, 0, size.y));
        _highlight.gameObject.SetActive(hasTarget);
        _targetButton.gameObject.SetActive(hasTarget);
        Place(_highlight, focus);
        Place((RectTransform)_targetButton.transform, focus);
        Place(_dim[0], new Rect(0, focus.yMax, size.x, size.y - focus.yMax));
        Place(_dim[1], new Rect(0, 0, size.x, focus.yMin));
        Place(_dim[2], new Rect(0, focus.yMin, focus.xMin, focus.height));
        Place(_dim[3], new Rect(focus.xMax, focus.yMin, size.x - focus.xMax, focus.height));

        float width = Mathf.Min(570, size.x * .37f);
        float height = Mathf.Min(450, size.y - 48);
        float y = Mathf.Clamp(focus.center.y - height * .5f, 24, size.y - height - 24);
        Rect description = new Rect(size.x - width - 28, y, width, height);
        if (hasTarget && description.Overlaps(focus))
        {
            description.x = 28;
            if (description.Overlaps(focus))
            {
                description.x = Mathf.Clamp(focus.center.x - width * .5f, 28, size.x - width - 28);
                description.y = focus.yMin >= height + 48 ? focus.yMin - height - 24 : focus.yMax + 24;
            }
        }
        Place(_description, description);
    }

    private Rect FromScreen(Vector2 min, Vector2 max)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, min, null, out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, max, null, out Vector2 b);
        a -= _canvasRect.rect.min;
        b -= _canvasRect.rect.min;
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private static void Place(RectTransform target, Rect rect)
    {
        target.anchorMin = target.anchorMax = target.pivot = Vector2.zero;
        target.anchoredPosition = rect.position;
        target.sizeDelta = rect.size;
    }
}
