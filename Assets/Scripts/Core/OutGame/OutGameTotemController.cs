using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>토템 선택 레벨과 누적 보너스를 관리합니다.</summary>
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
        // 단, 최대 단계에서 좌클릭하여 0단계로 순환하는 경우에는 현재 레벨만큼 한 번에 감소할 수 있습니다.
        if (delta == 0) return false;

        int index = FindIndex(id);
        if (index < 0) return false;

        TotemData data = _data[index];
        if (!data.IsAvailable) return false;
        int maxLevel = data.SelectionMode == TotemSelectionMode.Toggle ? 1 : data.MaxLevel;
        int level = Mathf.Clamp(_levels[index].Level + delta, 0, maxLevel);
        if (level == _levels[index].Level) return false;

        _levels[index].Level = level;
        Changed?.Invoke();
        return true;
    }

    public int GetRewardBonusPercent()
    {
        long total = 0;
        for (int i = 0; i < _levels.Count; i++)
        {
            if (_data[i].IsAvailable)
                total += _data[i].GetRewardBonusPercent(_levels[i].Level);
        }

        return (int)Math.Min(total, int.MaxValue);
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

            // 보류한 토템도 저장 ID는 유지하되 선택은 해제합니다.
            restoredLevels[index] = totem.IsAvailable ? entry.Level : 0;
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