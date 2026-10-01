using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>토템 선택 레벨과 테스트용 누적 보너스를 관리합니다.</summary>
public class OutGameTotemController : MonoBehaviour, ITotemSelection
{
    [SerializeField] private List<TotemData> _totemDatas = new List<TotemData>();

    public event Action Changed;
    public IReadOnlyList<TotemData> Data => _data;

    private readonly List<TotemData> _data = new List<TotemData>();
    private readonly List<TotemLevelEntry> _levels = new List<TotemLevelEntry>();

    public void Initialize()
    {
        _data.Clear();
        _levels.Clear();

        if (_totemDatas == null)
        {
            Debug.LogError("[OutGameTotemSelector] 토템 목록이 없습니다.", this);
            return;
        }

        for (int i = 0; i < _totemDatas.Count; i++)
        {
            TotemData data = _totemDatas[i];
            if (data == null || data.Id == TotemId.None || FindIndex(data.Id) >= 0)
            {
                Debug.LogError("[OutGameTotemSelector] 비어 있거나 ID가 중복된 토템입니다. Index: " + i, this);
                continue;
            }

            _data.Add(data);
            _levels.Add(new TotemLevelEntry { Id = data.Id, Level = 0 });
        }
    }

    public int GetLevel(TotemId id)
    {
        int index = FindIndex(id);
        return index >= 0 ? _levels[index].Level : 0;
    }

    public bool TryChangeLevel(TotemId id, int delta)
    {
        // 한 번의 클릭으로 한 단계만 변경합니다.
        if (delta != 1 && delta != -1) return false;

        int index = FindIndex(id);
        if (index < 0) return false;

        TotemData data = _data[index];
        int maxLevel = data.SelectionMode == TotemSelectionMode.Toggle ? 1 : data.MaxLevel;
        int level = Mathf.Clamp(_levels[index].Level + delta, 0, maxLevel);
        if (level == _levels[index].Level) return false;

        _levels[index].Level = level;
        Changed?.Invoke();
        return true;
    }

    public int GetRewardBonusPercent()
    {
        int totalLevel = 0;
        for (int i = 0; i < _levels.Count; i++)
        {
            totalLevel += _levels[i].Level;
        }

        // 현재 테스트를 위해 토템 종류 수가 아니라 선택 레벨의 합에 20%를 곱합니다.
        return totalLevel * 20;
    }

    public List<TotemLevelEntry> CaptureLevels()
    {
        List<TotemLevelEntry> result = new List<TotemLevelEntry>();
        for (int i = 0; i < _levels.Count; i++)
        {
            result.Add(new TotemLevelEntry { Id = _levels[i].Id, Level = _levels[i].Level });
        }
        return result;
    }

    private int FindIndex(TotemId id)
    {
        for (int i = 0; i < _data.Count; i++)
        {
            if (_data[i].Id == id) return i;
        }
        return -1;
    }
}
