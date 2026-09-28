using System;
using UnityEngine;

/// <summary>같은 토템의 단계를 아래에서 위 순서로 표시합니다. 레벨 변경 규칙은 Selector에 있습니다.</summary>
public class OutGameTotemColumn : MonoBehaviour
{
    [Tooltip("아래 단계부터 위 단계 순서로 연결합니다. 토글 토템은 버튼 하나입니다.")]
    [SerializeField] private OutGameTotemStepButton[] _steps;

    private TotemId _id;
    public event Action<TotemId, int> LevelChangeRequested;

    public bool ValidateReferences()
    {
        bool valid = _steps != null && _steps.Length > 0;
        if (valid)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i] == null || !_steps[i].ValidateReferences()) valid = false;
            }
        }
        if (!valid) Debug.LogError("[OutGameTotemColumn] 아래에서 위 순서로 단계 버튼을 연결해주세요.", this);
        return valid;
    }

    public void Initialize()
    {
        Shutdown();
        if (!ValidateReferences()) return;
        for (int i = 0; i < _steps.Length; i++)
        {
            _steps[i].Initialize();
            _steps[i].Clicked += OnStepClicked;
        }
    }

    public void SetDisplay(TotemData data, int level)
    {
        _id = data.Id;
        for (int i = 0; i < _steps.Length; i++)
        {
            string label = data.DisplayName;
            if (data.SelectionMode == TotemSelectionMode.Leveled) label += "\n" + (i + 1) + "단계";
            _steps[i].SetDisplay(label, i < level);
        }
    }

    public void Shutdown()
    {
        if (_steps != null)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i] == null) continue;
                _steps[i].Clicked -= OnStepClicked;
                _steps[i].Shutdown();
            }
        }
        _id = TotemId.None;
    }

    /// <summary> 입력받은 값에 따라 제단의 활성화 수치 변경</summary>
    private void OnStepClicked(int delta)
    {
        if (_id != TotemId.None) LevelChangeRequested?.Invoke(_id, delta);
    }

    private void OnDestroy() { Shutdown(); }
}
