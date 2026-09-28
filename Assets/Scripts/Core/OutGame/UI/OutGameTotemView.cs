using System;
using TMPro;
using UnityEngine;

/// <summary>고정된 6열에 두 페이지의 토템을 표시하고, 입력을 UIController에 전달합니다.</summary>
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
        _inspectedTotem = TotemId.EnemyDamage;
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
        for (int i = 0; i < _columns.Length; i++)
        {
            TotemData data = FindData(GetColumnId(i));
            _columns[i].gameObject.SetActive(data != null);
            if (data != null) _columns[i].SetDisplay(data, _selection.GetLevel(data.Id));
        }
        _leftButton.interactable = _page > 0;
        _rightButton.interactable = _page < 1;
        _bonusLabel.text = "누적 보너스 " + _selection.GetRewardBonusPercent() + "%";

        TotemData inspected = FindData(_inspectedTotem);
        if (inspected == null) return;
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
    }

    // 기존 열 순서는 3단 / 단일 / 3단 / 3단 / 단일 / 3단입니다.
    private TotemId GetColumnId(int column)
    {
        if (_page == 1)
        {
            if (column == 1) return TotemId.DeathBurst;
            if (column == 4) return TotemId.Pursuer;
            return TotemId.None;
        }

        switch (column)
        {
            case 0: return TotemId.EnemyDamage;
            case 1: return TotemId.EchoAttack;
            case 2: return TotemId.EnemyCount;
            case 3: return TotemId.DamageTaken;
            case 4: return TotemId.Crossfire;
            case 5: return TotemId.GoldReduction;
        }
        return TotemId.None;
    }

    private TotemData FindData(TotemId id)
    {
        for (int i = 0; i < _selection.Data.Count; i++)
        {
            if (_selection.Data[i].Id == id) return _selection.Data[i];
        }
        return null;
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
        _page = 0;
        _inspectedTotem = TotemId.EnemyDamage;
        Refresh();
    }

    private void OnRightClicked()
    {
        _page = 1;
        _inspectedTotem = TotemId.DeathBurst;
        Refresh();
    }

    private void OnBackClicked() { BackRequested?.Invoke(); }
    private void OnStartClicked() { StartRequested?.Invoke(); }
    private void OnDestroy() { Shutdown(); }
}
