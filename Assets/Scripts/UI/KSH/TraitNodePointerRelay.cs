using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hierarchy에 배치된 특성 Button 오브젝트의 포인터 입력만 전달합니다.
/// 프리팹 생성이나 Update() 폴링을 하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TraitNodePointerRelay : MonoBehaviour,
    IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    public TraitId Id { get; private set; } = TraitId.None;
    public bool IsHovered { get; private set; }

    public event Action<TraitId, Vector2> PointerEntered;
    public event Action<TraitId, Vector2> PointerMoved;
    public event Action<TraitId> PointerExited;

    public void Configure(TraitId id)
    {
        Id = id;
        IsHovered = false;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (Id == TraitId.None) return;
        IsHovered = true;
        PointerEntered?.Invoke(Id, e.position);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (!IsHovered || Id == TraitId.None) return;
        PointerMoved?.Invoke(Id, e.position);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!IsHovered) return;
        IsHovered = false;
        PointerExited?.Invoke(Id);
    }

    private void OnDisable()
    {
        if (IsHovered)
        {
            IsHovered = false;
            PointerExited?.Invoke(Id);
        }
    }
}
