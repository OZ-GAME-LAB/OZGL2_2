using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>특성 레벨, 선행 조건과 구매를 관리합니다. SO의 고정 데이터는 변경하지 않습니다.</summary>
public class OutGameTraitController : MonoBehaviour, ITraitProgression, ISaveDataProvider<TraitSaveData>
{
    [SerializeField] private List<TraitData> _traitDatas = new List<TraitData>();

    public event Action Changed;
    public IReadOnlyList<TraitData> Data => _data;
    public bool IsInitialized { get; private set; }

    private readonly List<TraitData> _data = new List<TraitData>();
    private readonly List<TraitLevelEntry> _levels = new List<TraitLevelEntry>();
    private OutGameTestWallet _wallet;
    private IPersistentSaveWriter _saveWriter;

    public void Initialize(OutGameTestWallet wallet, IPersistentSaveWriter saveWriter)
    {
        IsInitialized = false;
        _wallet = wallet;
        _saveWriter = saveWriter;
        _data.Clear();
        _levels.Clear();

        if (_wallet == null || _saveWriter == null)
        {
            Debug.LogError("[OutGameTraitSelector] 영구 지갑과 저장 요청 대상을 연결해주세요.", this);
        }

        if (_traitDatas == null)
        {
            Debug.LogError("[OutGameTraitSelector] 특성 목록이 없습니다.", this);
            return;
        }

        bool validCatalog = true;
        for (int i = 0; i < _traitDatas.Count; i++)
        {
            TraitData data = _traitDatas[i];
            if (data == null || data.Id == TraitId.None || FindIndex(data.Id) >= 0)
            {
                Debug.LogError("[OutGameTraitSelector] 비어 있거나 ID가 중복된 특성입니다. Index: " + i, this);
                validCatalog = false;
                continue;
            }

            _data.Add(data);
            _levels.Add(new TraitLevelEntry { Id = data.Id, Level = 0 });
        }

        // 저장 준비는 이 초기화 이후 로드 단계에서 완료됩니다.
        IsInitialized = _wallet != null && _wallet.IsInitialized && _saveWriter != null && validCatalog && _data.Count > 0;
    }

    public int GetLevel(TraitId id)
    {
        int index = FindIndex(id);
        return index >= 0 ? _levels[index].Level : 0;
    }

    public bool CanUpgrade(TraitId id, out string reason)
    {
        reason = string.Empty;
        if (!IsInitialized)
        {
            reason = "특성 초기화가 완료되지 않았습니다.";
            return false;
        }

        if (!_saveWriter.IsReady)
        {
            reason = "영구 데이터를 먼저 불러오거나 최초 데이터를 생성해주세요.";
            return false;
        }

        int index = FindIndex(id);
        if (index < 0)
        {
            reason = "등록되지 않은 특성입니다.";
            return false;
        }

        TraitData data = _data[index];
        if (_levels[index].Level >= data.MaxLevel)
        {
            reason = "최대 레벨";
            return false;
        }

        TraitData prerequisite = data.Prerequisite;
        if (prerequisite != null && GetLevel(prerequisite.Id) < 1)
        {
            reason = prerequisite.DisplayName + " 1레벨이 필요합니다.";
            return false;
        }

        if (!_wallet.CanSpend(CurrencyType.Bloodstone, data.UpgradeCost))
        {
            reason = "혈석이 부족합니다.";
            return false;
        }

        return true;
    }

    public bool TryUpgrade(TraitId id)
    {
        return TryUpgrade(id, out string error);
    }

    // 구매 규칙과 적용은 Selector가 담당하고, 저장 요청에는 완성된 사본만 전달합니다.
    public bool TryUpgrade(TraitId id, out string error)
    {
        TraitSaveData data;
        int cost;
        if (!TryCreateUpgradeData(id, out data, out cost, out error)) return false;

        // 전체 사본에서 이번 구매에 필요한 영역만 바꿔 다른 영구 정보도 유지합니다.
        PersistentSaveData next = _saveWriter.CaptureSaveData();
        next.Traits = data;
        next.Wallet = _wallet.CaptureSaveData();
        next.Wallet.Bloodstone -= cost;
        if (!_saveWriter.TrySave(next, out error)) return false;

        // 파일 저장 실패 시에는 어느 쪽도 바뀌지 않습니다. 성공 후 한 번만 적용합니다.
        _wallet.RestoreSaveData(next.Wallet, false);
        RestoreSaveData(next.Traits, false);
        _wallet.NotifyChanged();
        NotifyChanged();
        return true;
    }

    /// <summary>구매할 수 있는지 확인하고 구매 후 데이터를 만듭니다. 실제 상태와 혈석은 변경하지 않습니다.</summary>
    private bool TryCreateUpgradeData(TraitId id, out TraitSaveData data, out int cost, out string error)
    {
        data = null;
        cost = 0;
        if (!CanUpgrade(id, out error)) return false;

        int index = FindIndex(id);
        data = CaptureSaveData();
        data._levels[index].Level++;
        cost = _data[index].UpgradeCost;
        return true;
    }

    public List<TraitLevelEntry> CaptureLevels()
    {
        // 원본 리스트와 원소를 모두 복사해 시작 컨텍스트가 이후 구매에 영향을 받지 않게 합니다.
        List<TraitLevelEntry> result = new List<TraitLevelEntry>();
        for (int i = 0; i < _levels.Count; i++)
        {
            result.Add(new TraitLevelEntry { Id = _levels[i].Id, Level = _levels[i].Level });
        }
        return result;
    }

    private int FindIndex(TraitId id)
    {
        for (int i = 0; i < _data.Count; i++)
        {
            if (_data[i].Id == id) return i;
        }
        return -1;
    }

    public TraitSaveData CaptureSaveData()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("특성 초기화 후 저장 데이터를 가져올 수 있습니다.");
        }

        return new TraitSaveData(_levels);
    }

    /// <summary>상태를 변경하지 않고 저장된 특성 ID, 레벨과 선행 조건을 검사합니다.</summary>
    public bool TryValidateSaveData(TraitSaveData data, out string error)
    {
        error = string.Empty;
        if (!IsInitialized)
        {
            error = "특성 초기화가 완료되지 않았습니다.";
            return false;
        }

        if (data == null || data._levels == null || data._levels.Count == 0)
        {
            error = "특성 저장 데이터 또는 레벨 목록이 비어 있습니다.";
            return false;
        }

        if (data.Version != 1)
        {
            error = "특성 저장 영역이 없거나 지원하지 않는 버전입니다.";
            return false;
        }

        for (int i = 0; i < data._levels.Count; i++)
        {
            TraitLevelEntry entry = data._levels[i];
            if (entry == null)
            {
                error = "특성 저장 항목이 비어 있습니다. Index: " + i;
                return false;
            }

            int index = FindIndex(entry.Id);
            if (entry.Id == TraitId.None || index < 0)
            {
                error = "등록되지 않은 특성 ID입니다: " + entry.Id;
                return false;
            }

            for (int previous = 0; previous < i; previous++)
            {
                if (data._levels[previous].Id == entry.Id)
                {
                    error = "중복된 특성 ID입니다: " + entry.Id;
                    return false;
                }
            }

            if (entry.Level < 0 || entry.Level > _data[index].MaxLevel)
            {
                error = "특성 레벨이 허용 범위를 벗어났습니다: " + entry.Id;
                return false;
            }
        }

        for (int i = 0; i < data._levels.Count; i++)
        {
            TraitLevelEntry entry = data._levels[i];
            if (entry.Level == 0) continue;

            TraitData prerequisite = _data[FindIndex(entry.Id)].Prerequisite;
            if (prerequisite != null && GetSavedLevel(data, prerequisite.Id) < 1)
            {
                error = "선행 특성이 없는 저장 데이터입니다: " + entry.Id;
                return false;
            }
        }

        return true;
    }

    public void RestoreSaveData(TraitSaveData data)
    {
        RestoreSaveData(data, true);
    }

    /// <summary>여러 Provider를 함께 복원할 때는 알림을 미룬 뒤 NotifyChanged를 호출합니다.</summary>
    public void RestoreSaveData(TraitSaveData data, bool notifyChanged)
    {
        string error;
        if (!TryValidateSaveData(data, out error))
        {
            throw new ArgumentException(error, nameof(data));
        }

        _levels.Clear();
        for (int i = 0; i < _data.Count; i++)
        {
            // 저장 순서가 달라도 카탈로그 순서를 유지합니다. 새로 추가된 특성은 0레벨입니다.
            _levels.Add(new TraitLevelEntry
            {
                Id = _data[i].Id,
                Level = GetSavedLevel(data, _data[i].Id)
            });
        }

        if (notifyChanged) NotifyChanged();
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private int GetSavedLevel(TraitSaveData data, TraitId id)
    {
        for (int i = 0; i < data._levels.Count; i++)
        {
            if (data._levels[i].Id == id) return data._levels[i].Level;
        }
        return 0;
    }
}

[Serializable]
public class TraitSaveData
{
    // JsonUtility가 null 영역을 빈 객체로 읽더라도 정상 저장 데이터와 구분합니다.
    // 기본 생성자에서는 0을 유지하고 실제 저장 데이터를 만들 때만 1로 설정합니다.
    public int Version;
    public List<TraitLevelEntry> _levels = new List<TraitLevelEntry>();

    public TraitSaveData()
    {
    }

    public TraitSaveData(List<TraitLevelEntry> levels)
    {
        if (levels == null) throw new ArgumentNullException(nameof(levels));

        Version = 1;

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == null)
            {
                throw new ArgumentException("특성 저장 항목이 비어 있습니다.", nameof(levels));
            }

            _levels.Add(new TraitLevelEntry { Id = levels[i].Id, Level = levels[i].Level });
        }
    }
}
