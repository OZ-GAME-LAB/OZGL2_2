using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 테스트용 마우스 범위 지정. 효과/소모는 ConsumableItemManager에 위임합니다.
public class ItemAreaUseTestController : MonoBehaviour
{
    public bool IsAiming { get; private set; }
    public Vector2? Center { get; private set; }
    private ConsumableItemManager _items;
    private GameFlowController _flow;
    private Func<Vector2, bool> _overPanel;
    private Action<string> _report;
    private ConsumableItemData _item;
    private int _slot;
    private Camera _camera;
    private bool _showCircle;
    private int _aimStartedFrame;
    private readonly List<RaycastResult> _uiHits = new();

    public void Initialize(ConsumableItemManager items, GameFlowController flow,
        Func<Vector2, bool> overPanel, Action<string> report)
    {
        _items = items;
        _flow = flow;
        _overPanel = overPanel;
        _report = report;
    }

    public void Begin(int slot)
    {
        Cancel();
        if (_items == null || _flow == null || !_flow.CanBattle() ||
            !_items.TryGetItem(slot, out _item) || _item.TargetMode != ConsumableTargetMode.Area)
        {
            _report?.Invoke("전투 중 범위 아이템을 선택해주세요.");
            return;
        }
        _slot = slot;
        IsAiming = true;
        _aimStartedFrame = Time.frameCount;
        _items.InventoryChanged += Cancel;
        _report?.Invoke("전투 화면 좌클릭: 사용 / 우클릭·Esc: 취소");
    }

    public void Cancel()
    {
        if (_items != null) _items.InventoryChanged -= Cancel;
        IsAiming = false;
        Center = null;
        _showCircle = false;
    }

    private void OnDisable() => Cancel();

    private void Update()
    {
        if (!IsAiming) return;
        if (_items == null || _flow == null || !_flow.CanBattle() ||
            !_items.TryGetItem(_slot, out var current) || current != _item)
        {
            Cancel();
            return;
        }
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) { _showCircle = false; return; }
        Vector2 pointer = Mouse.current.position.ReadValue();
        bool cancel = Mouse.current.rightButton.wasPressedThisFrame ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        bool click = Mouse.current.leftButton.wasPressedThisFrame;
#else
        Vector2 pointer = Input.mousePosition;
        bool cancel = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
        bool click = Input.GetMouseButtonDown(0);
#endif
        if (cancel)
        {
            Cancel();
            _report?.Invoke("범위 지정 취소 / 아이템 유지");
            return;
        }

        _camera = Camera.main;
        _showCircle = false;
        Center = null;
        if (_camera == null || !_camera.pixelRect.Contains(pointer) ||
            (_overPanel != null && _overPanel(pointer)) || IsOverUI(pointer)) return;

        // 전투 유닛과 동일한 XY 평면(z=0)에 마우스 광선을 투영합니다.
        Ray ray = _camera.ScreenPointToRay(pointer);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return;
        Center = (Vector2)ray.GetPoint(distance);
        _showCircle = true;
        // 패널이 접힌 프레임의 선택 클릭을 전투 화면 사용 클릭으로 처리하지 않습니다.
        if (!click || Time.frameCount == _aimStartedFrame) return;
        try
        {
            bool applied = _items.TryUse(_slot, selectedPosition: Center.Value);
            if (applied) Cancel();
            _report?.Invoke(applied ? "범위 효과 적용 / 아이템 1개 소모" : "미적용 / 아이템 유지 — 위치를 다시 선택하세요.");
        }
        catch (Exception exception)
        {
            Cancel();
            Debug.LogException(exception, this);
            _report?.Invoke("사용 중 예외 — Console 확인");
        }
    }

    private bool IsOverUI(Vector2 pointer)
    {
        if (EventSystem.current == null) return false;
        _uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, _uiHits);
        foreach (RaycastResult hit in _uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !_showCircle || !Center.HasValue || _camera == null) return;
        const int segments = 64;
        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.color = Color.cyan;
        try
        {
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = ScreenPoint(i * Mathf.PI * 2f / segments);
                Vector2 b = ScreenPoint((i + 1) * Mathf.PI * 2f / segments);
                Vector2 delta = b - a;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
                GUI.DrawTexture(new Rect(a.x, a.y - 1f, delta.magnitude, 2f), Texture2D.whiteTexture);
                GUI.matrix = previousMatrix;
            }
        }
        finally { GUI.color = previousColor; GUI.matrix = previousMatrix; }
    }

    private Vector2 ScreenPoint(float angle)
    {
        Vector2 point = Center.Value + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _item.Radius;
        Vector3 screen = _camera.WorldToScreenPoint(new Vector3(point.x, point.y, 0f));
        return new Vector2(screen.x, Screen.height - screen.y);
    }
}
