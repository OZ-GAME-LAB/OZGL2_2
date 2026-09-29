using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class AltarButtonSlot
{
    public AltarId Id;
    public OutGameChoiceButton Button;
}

/// <summary>제단 버튼과 설명 표시입니다. 잠긴 제단의 미리보기와 실제 선택을 구분합니다.</summary>
public class OutGameAltarView : MonoBehaviour
{
    [SerializeField] private AltarButtonSlot[] _slots; //제단 정보가 들어있는 슬롯
    [SerializeField] private TMP_Text _description;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _traitButton;
    [SerializeField] private Button _nextButton;

    private IAltarSelection _selection;
    private AltarId _inspectedAltar;

    public event Action<AltarId> Selected;
    public event Action TraitRequested;
    public event Action NextRequested;

    /// <summary> 제단 패널에서 사용할 제단정보나 버튼들이 다 들어가 있는지 검사 </summary>
    public bool ValidateReferences()
    {
        bool valid = _slots != null && _slots.Length > 0 && _description != null &&
            _previousButton != null && _traitButton != null && _nextButton != null;
        if (valid)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i].Button == null ||
                    !_slots[i].Button.ValidateReferences()) valid = false;
            }
        }
        if (!valid) Debug.LogError("[OutGameAltarView] Inspector의 슬롯과 UI 참조를 확인해주세요.", this);
        return valid;
    }

    public void Initialize(IAltarSelection selection)
    {
        Shutdown();
        if (!ValidateReferences() || selection == null) return;
        _selection = selection;
        _inspectedAltar = AltarId.Abundance;
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Button.Initialize();
            _slots[i].Button.Clicked += OnAltarClicked;
        }
        _previousButton.interactable = false;
        _traitButton.onClick.AddListener(OnTraitClicked);
        _nextButton.onClick.AddListener(OnNextClicked);
        Refresh();
    }

    public void Inspect(AltarId id)
    {
        _inspectedAltar = id;
        Refresh();
    }

    public void Refresh()
    {
        if (_selection == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            AltarData data = FindData(_slots[i].Id);
            if (data == null) continue;
            bool unlocked = _selection.IsUnlocked(data.Id);
            Color color = unlocked ? Color.white : new Color(0.72f, 0.72f, 0.72f);
            if (_selection.SelectedAltar == data.Id) color = new Color(1f, 0.8f, 0.25f);
            string label = data.DisplayName;
            if (!unlocked) label += "\n잠김";
            _slots[i].Button.SetDisplay(label, color);
        }

        AltarData inspected = FindData(_inspectedAltar);
        if (inspected == null) return;
        if (_selection.IsUnlocked(inspected.Id))
            _description.text = inspected.DisplayName + "\n\n" + inspected.Description;
        else
            _description.text = inspected.DisplayName + "\n\n잠금 해제 조건\n" +
                _selection.GetUnlockCondition(inspected.Id);
    }

    public void Shutdown()
    {
        if (_slots != null)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i].Button == null) continue;
                _slots[i].Button.Clicked -= OnAltarClicked;
                _slots[i].Button.Shutdown();
            }
        }
        if (_traitButton != null) _traitButton.onClick.RemoveListener(OnTraitClicked);
        if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNextClicked);
        _selection = null;
    }

    private AltarData FindData(AltarId id)
    {
        for (int i = 0; i < _selection.Data.Count; i++)
        {
            if (_selection.Data[i].Id == id) return _selection.Data[i];
        }
        return null;
    }

    private void OnAltarClicked(OutGameChoiceButton button)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Button != button) continue;
            Selected?.Invoke(_slots[i].Id);
            return;
        }
    }

    private void OnTraitClicked() { TraitRequested?.Invoke(); }
    private void OnNextClicked() { NextRequested?.Invoke(); }
    private void OnDestroy() { Shutdown(); }
}
