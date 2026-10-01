using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>제단 목록과 실제 선택된 제단을 관리합니다. 잠긴 제단의 정보 조회는 선택과 별개입니다.</summary>
public class OutGameAltarController : MonoBehaviour, IAltarSelection
{
    [SerializeField] private AltarCatalog _catalog;

    public event Action Changed;
    public IReadOnlyList<AltarData> Data => _data;
    public AltarId SelectedAltar { get; private set; }

    private readonly List<AltarData> _data = new List<AltarData>();

    public void Initialize()
    {
        _data.Clear();
        SelectedAltar = AltarId.None;

        if (_catalog == null)
        {
            Debug.LogError("[OutGameAltarSelector] 제단 Catalog를 연결해주세요.", this);
            return;
        }

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
        // MVP에서는 풍요의 제단만 열려 있습니다. 실제 해금 판정은 추후 연결합니다.
        if (id != AltarId.Abundance) return false;

        for (int i = 0; i < _data.Count; i++)
        {
            if (_data[i].Id == id) return true;
        }

        return false;
    }

    public string GetUnlockCondition(AltarId id)
    {
        switch (id)
        {
            case AltarId.Abundance:
                return string.Empty;
            case AltarId.Conquest:
                return "5분기 보스웨이브 클리어";
            case AltarId.Arcane:
                return "스킬피해로 적 500마리 누적 처치";
            case AltarId.Guardian:
                return "무한모드 1분기 클리어";
            default:
                return "등록되지 않은 제단입니다.";
        }
    }

    public bool TrySelect(AltarId id)
    {
        if (!IsUnlocked(id)) return false;
        if (SelectedAltar == id) return true;

        SelectedAltar = id;
        Changed?.Invoke();
        return true;
    }
}
