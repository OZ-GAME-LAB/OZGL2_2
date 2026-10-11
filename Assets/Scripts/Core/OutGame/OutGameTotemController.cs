using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>토템 선택 레벨과 테스트용 누적 보너스를 관리합니다.</summary>
public class OutGameTotemController : MonoBehaviour, ITotemSelection, ISaveDataProvider<TotemRunSaveData>
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

    // 레벨 0을 포함한 전체 선택 상태를 복사합니다.
    public TotemRunSaveData CaptureSaveData()
    {
        if (_data.Count == 0)
            throw new InvalidOperationException("토템 목록 초기화 후 저장하세요.");

        return new TotemRunSaveData
        {
            Totems = CaptureLevels()
        };
    }

    public void RestoreSaveData(TotemRunSaveData data)
    {
        if (_data.Count == 0)
            throw new InvalidOperationException("토템 목록 초기화 후 복원하세요.");
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Totems == null)
            throw new ArgumentException("토템 저장 목록이 없습니다.", nameof(data));

        // 모두 검증한 뒤 적용합니다. 저장에 없는 신규 토템은 레벨 0으로 둡니다.
        int[] restoredLevels = new int[_levels.Count];
        var seen = new HashSet<TotemId>();
        foreach (TotemLevelEntry entry in data.Totems)
        {
            if (entry == null || entry.Id == TotemId.None || !seen.Add(entry.Id))
                throw new ArgumentException("토템 저장 항목의 ID와 중복을 확인하세요.", nameof(data));

            int index = FindIndex(entry.Id);
            if (index < 0)
                throw new ArgumentException("저장된 토템을 목록에서 찾을 수 없습니다.", nameof(data));

            TotemData totem = _data[index];
            int maxLevel = totem.SelectionMode == TotemSelectionMode.Toggle ? 1 : totem.MaxLevel;
            if (entry.Level < 0 || entry.Level > maxLevel)
                throw new ArgumentException("저장된 토템 레벨이 허용 범위를 벗어났습니다.", nameof(data));

            restoredLevels[index] = entry.Level;
        }

        bool changed = false;
        for (int i = 0; i < _levels.Count; i++)
        {
            if (_levels[i].Level != restoredLevels[i]) changed = true;
            _levels[i].Level = restoredLevels[i];
        }

        if (changed) Changed?.Invoke();
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
