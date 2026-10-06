using System.Collections.Generic;
using Game.UI.InGame;
using UnityEngine;
using UnityEngine.UI;

public class ArtifactInventoryPopupView : MonoBehaviour
{
    [SerializeField] private UIScreen _screen;
    [SerializeField] private List<UIItemSlot> Slots = new List<UIItemSlot>();
    [SerializeField] private UIItemSlot _slotPrefab;
    [SerializeField] private RectTransform _slotRoot;
    [SerializeField] private Sprite _defaultIcon;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private GameObject _emptyMessage;
    [SerializeField] private ArtifactPopupView _artifactPopupView;
    private IArtifactReader _artifactReader;
    
    public void Initialize(IArtifactReader artifactReader)
    {
        _artifactReader = artifactReader;
    }

    public void Open()
    {
        if (_artifactReader == null || !_artifactReader.IsInitialized)
            return;

        if (_screen == null || _screen.Manager == null || _slotPrefab == null ||
            _slotRoot == null || _artifactPopupView == null)
        {
            Debug.LogWarning("[ArtifactInventory] 인벤토리의 UI 참조를 확인해주세요.", this);
            return;
        }

        if (_screen.IsVisible && _screen.Manager.TopPopup != _screen)
            return;
        if (!_screen.Manager.OpenPopup(_screen.Id))
            return;

        RefreshSlots();
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary> 요구치만큼 슬롯이 없다면 새로 생성하고 Slots 리스트에 추가하는 오브젝트 풀링</summary>
    private void EnsureSlotCount(int requiredCount)
    {
        while (Slots.Count < requiredCount)
        {
            UIItemSlot slot = Instantiate(_slotPrefab, _slotRoot, false);
            slot.gameObject.SetActive(false);
            Slots.Add(slot);
        }
    }

    /// <summary> 보유중인 아티팩트 정보를 조회해 해당 아티팩트를 인벤토리 슬롯과 연결</summary>
    private void RefreshSlots()
    {
        var instances = _artifactReader.Instances;
        EnsureSlotCount(instances.Count);

        for (int i = 0; i < Slots.Count; i++)
        {
            UIItemSlot slot = Slots[i];
            if (i >= instances.Count || instances[i]?.Data == null)
            {
                slot.Unbind();
                slot.gameObject.SetActive(false);
                continue;
            }

            ArtifactInstance instance = instances[i];
            slot.Bind(
                instance.Data.Icon != null ? instance.Data.Icon : _defaultIcon,
                instance.Data.DisplayName,
                $"×{instance.StackCount}",
                false,
                true,
                () => ShowDetail(instance.Data, slot) 
            );
            slot.gameObject.SetActive(true);
        }

        if (_emptyMessage != null)
            _emptyMessage.SetActive(instances.Count == 0);
    }

    /// <summary> 아티팩트 이미지를 클릭했을 때 클릭한 이미지 위치에 상세화면을 여는 기능</summary>
    private void ShowDetail(ArtifactData artifact, UIItemSlot slot)
    {
        if (!_screen.IsVisible || _screen.Manager.TopPopup != _screen)
            return;

        RectTransform rect = (RectTransform)slot.transform;
        Canvas canvas = slot.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
            camera, rect.TransformPoint(rect.rect.center));

        _artifactPopupView.Open(artifact, screenPosition);
    }
}
