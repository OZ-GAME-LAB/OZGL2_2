using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>사용 가능한 토템을 6열씩 표시하고, 입력을 UIController에 전달합니다.</summary>
public class OutGameTotemView : MonoBehaviour
{
    [SerializeField] private OutGameTotemColumn[] _columns;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private TMP_Text _bonusLabel;
    [SerializeField] private UnityEngine.UI.Button _leftButton;
    [SerializeField] private UnityEngine.UI.Button _rightButton;
    [SerializeField] private UnityEngine.UI.Button _backButton;
    [SerializeField] private UnityEngine.UI.Button _startButton;

    private ITotemSelection _selection;
    private readonly List<TotemData> _visibleTotems = new List<TotemData>();
    private TotemId _inspectedTotem;
    private int _page;

    public event Action<TotemId, int> LevelChangeRequested;
    public event Action BackRequested;
    public event Action StartRequested;

    public bool ValidateReferences()
    {
        bool valid = _columns != null && _columns.Length == 6 && _description != null &&
            _bonusLabel != null && _leftButton != null && _rightButton != null &&
            _backButton != null && _startButton != null;
        if (valid)
        {
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i] == null || !_columns[i].ValidateReferences()) valid = false;
            }
        }
        if (!valid) Debug.LogError("[OutGameTotemView] 6개 열과 UI 참조를 연결해주세요.", this);
        return valid;
    }

    public void Initialize(ITotemSelection selection)
    {
        Shutdown();
        if (!ValidateReferences() || selection == null) return;
        _selection = selection;
        _page = 0;
        _inspectedTotem = TotemId.None;
        for (int i = 0; i < _columns.Length; i++)
        {
            _columns[i].Initialize();
            _columns[i].LevelChangeRequested += OnLevelChangeRequested;
        }
        _leftButton.onClick.AddListener(OnLeftClicked);
        _rightButton.onClick.AddListener(OnRightClicked);
        _backButton.onClick.AddListener(OnBackClicked);
        _startButton.onClick.AddListener(OnStartClicked);
        Refresh();
    }

    public void Refresh()
    {
        if (_selection == null) return;
        _visibleTotems.Clear();
        foreach (TotemData data in _selection.Data)
        {
            if (data.IsAvailable) _visibleTotems.Add(data);
        }
        int lastPage = Mathf.Max(0, (_visibleTotems.Count - 1) / _columns.Length);
        _page = Mathf.Clamp(_page, 0, lastPage);
        TotemData inspected = null;
        for (int i = 0; i < _columns.Length; i++)
        {
            TotemData data = GetColumnData(i);
            _columns[i].gameObject.SetActive(data != null);
            if (data != null) _columns[i].SetDisplay(data, _selection.GetLevel(data.Id));
            if (data != null && data.Id == _inspectedTotem) inspected = data;
        }
        _leftButton.interactable = _page > 0;
        _rightButton.interactable = _page < lastPage;
        _bonusLabel.text = "누적 보너스 " + _selection.GetRewardBonusPercent() + "%";

        if (inspected == null) inspected = GetColumnData(0);
        _inspectedTotem = inspected != null ? inspected.Id : TotemId.None;
        if (inspected == null)
        {
            _description.text = string.Empty;
            return;
        }
        int level = _selection.GetLevel(inspected.Id);
        _description.text = inspected.DisplayName + " (" + level + " / " + inspected.MaxLevel + ")\n" +
            inspected.Description + "\n" + inspected.EffectLabel + ": " +
            (inspected.ValuePerLevel * level).ToString("0.##") + inspected.ValueUnit;
    }

    public void Shutdown()
    {
        if (_columns != null)
        {
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i] == null) continue;
                _columns[i].LevelChangeRequested -= OnLevelChangeRequested;
                _columns[i].Shutdown();
            }
        }
        if (_leftButton != null) _leftButton.onClick.RemoveListener(OnLeftClicked);
        if (_rightButton != null) _rightButton.onClick.RemoveListener(OnRightClicked);
        if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
        if (_startButton != null) _startButton.onClick.RemoveListener(OnStartClicked);
        _selection = null;
        _visibleTotems.Clear();
    }

    private TotemData GetColumnData(int column)
    {
        int index = _page * _columns.Length + column;
        return index < _visibleTotems.Count ? _visibleTotems[index] : null;
    }

    private void OnLevelChangeRequested(TotemId id, int delta)
    {
        _inspectedTotem = id;
        LevelChangeRequested?.Invoke(id, delta);
        // 상한/하한이라 실제 레벨이 바뀌지 않아도 클릭한 토템의 설명은 표시합니다.
        Refresh();
    }

    private void OnLeftClicked()
    {
        _page--;
        _inspectedTotem = TotemId.None;
        Refresh();
    }

    private void OnRightClicked()
    {
        _page++;
        _inspectedTotem = TotemId.None;
        Refresh();
    }

    private void OnBackClicked() { BackRequested?.Invoke(); }
    private void OnStartClicked() { StartRequested?.Invoke(); }
    private void OnDestroy() { Shutdown(); }
}
