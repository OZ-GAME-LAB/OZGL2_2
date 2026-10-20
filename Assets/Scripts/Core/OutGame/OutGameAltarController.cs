using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

/// <summary>제단 목록과 실제 선택된 제단을 관리합니다. 잠긴 제단의 정보 조회는 선택과 별개입니다.</summary>
public class OutGameAltarController : MonoBehaviour, IAltarSelection, ISaveDataProvider<AltarRunSaveData>
{
    [SerializeField] private AltarCatalog _catalog;

    public event Action Changed;
    public IReadOnlyList<AltarData> Data => _data;
    public AltarId SelectedAltar { get; private set; }

    private readonly List<AltarData> _data = new List<AltarData>();
    private UnlockManager _unlocks;

    public void Initialize(UnlockManager unlocks = null)
    {
        if (_unlocks != null) _unlocks.Changed -= NotifyChanged;
        _unlocks = unlocks;
        if (_unlocks != null) _unlocks.Changed += NotifyChanged;
        _data.Clear();
        SelectedAltar = AltarId.None;

        if (_catalog == null)
        {
            Debug.LogError("[OutGameAltarSelector] 제단 Catalog를 연결해주세요.", this);
            return;
        }

        _catalog.RefreshLookup();

        for (int i = 0; i < _catalog.Altars.Count; i++)
        {
            _data.Add(_catalog.Altars[i]);
        }

        if (IsUnlocked(AltarId.Abundance))
        {
            SelectedAltar = AltarId.Abundance;
        }
        else
        {
            Debug.LogError("[OutGameAltarSelector] Catalog에 풍요의 제단이 필요합니다.", this);
        }
    }

    public bool IsUnlocked(AltarId id)
    {
        for (int i = 0; i < _data.Count; i++)
        {
            if (_data[i].Id != id) continue;
            UnlockData definition = _data[i].Unlock;
            return definition != null && (_unlocks != null
                ? _unlocks.IsUnlocked(definition.Id) : definition.DefaultUnlocked);
        }

        return false;
    }

    public string GetUnlockCondition(AltarId id)
    {
        foreach (AltarData data in _data)
            if (data.Id == id && data.Unlock != null) return data.Unlock.Description;
        return "등록되지 않은 제단입니다.";
    }

    private void NotifyChanged() => Changed?.Invoke();
    private void OnDestroy() { if (_unlocks != null) _unlocks.Changed -= NotifyChanged; }

    public bool TrySelect(AltarId id)
    {
        if (!IsUnlocked(id)) return false;
        if (SelectedAltar == id) return true;

        SelectedAltar = id;
        Changed?.Invoke();
        return true;
    }

    public AltarRunSaveData CaptureSaveData()
    {
        if (_data.Count == 0 || !IsUnlocked(SelectedAltar))
            throw new InvalidOperationException("제단 선택 초기화 후 저장하세요.");

        return new AltarRunSaveData
        {
            SelectedAltar = SelectedAltar
        };
    }

    public void RestoreSaveData(AltarRunSaveData data)
    {
        if (_data.Count == 0)
            throw new InvalidOperationException("제단 목록 초기화 후 복원하세요.");
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (!IsUnlocked(data.SelectedAltar))
            throw new ArgumentException("저장된 제단이 없거나 선택할 수 없습니다.", nameof(data));

        TrySelect(data.SelectedAltar);
    }
}
