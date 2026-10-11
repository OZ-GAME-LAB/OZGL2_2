using System;
using System.Collections.Generic;
using UnityEngine;

// 아웃게임 저장 데이터를 모으고 저장/복원 순서를 관리합니다
// JSON 변환과 파일 입출력은 SaveManager에서 합니다
public class OutGameSaveCoordinator : MonoBehaviour, IPersistentSaveWriter
{
    // 기존 저장 테스트나 실제 영구재화 파일과 구분하는 키입니다.
    public static readonly SaveKey<PersistentSaveData> SaveKey =
        new SaveKey<PersistentSaveData>("outgame-test-persistent");

    public bool IsReady { get; private set; }
    public string LastError { get; private set; }
    public string LastSettledRunId => _lastSettledRunId;
    public string SavePath => _saveManager == null ? string.Empty : _saveManager.GetFilePath(SaveKey);

    private SaveManager _saveManager;
    private OutGameTraitController _traits;
    private PersistentCurrencyManager _wallet;
    private ISaveDataProvider<AltarRunSaveData> _altar;
    private ISaveDataProvider<TotemRunSaveData> _totem;
    private ISaveDataProvider<TraitRunSaveData> _traitRun;
    private string _lastSettledRunId;
    private bool _isInitialized;

    // 10.9 / 문규성 / 두 씬에서 제단과 토템 Provider를 모두 연결하고, 인게임에서는 런 특성 Provider도 연결하도록 변경했습니다.
    // 아웃게임은 선택 Controller를, 인게임은 기존 런 매니저를 전달해 별도 선택 캐시 없이 같은 저장 데이터를 사용합니다.
    public void Initialize(SaveManager saveManager, OutGameTraitController traits, PersistentCurrencyManager wallet,
        ISaveDataProvider<AltarRunSaveData> altar, ISaveDataProvider<TotemRunSaveData> totem,
        ISaveDataProvider<TraitRunSaveData> traitRun = null)
    {
        if (_isInitialized)
            throw new InvalidOperationException("OutGameSaveCoordinator는 씬에서 한 번만 초기화할 수 있습니다.");

        _saveManager = saveManager;
        _traits = traits;
        _wallet = wallet;
        _altar = altar;
        _totem = totem;
        _traitRun = traitRun;
        _isInitialized = true;
        IsReady = false;
        LastError = null;
    }

    // 최초 실행에만 기본 상태를 저장합니다. 손상된 기존 파일은 덮어쓰지 않습니다.
    public bool TryLoadOrCreate(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (_saveManager.HasSaveFile(SaveKey)) return TryLoad(out error);

        try
        {
            if (!TryWrite(CaptureSaveData(), out error)) return false;
            IsReady = true;
            return true;
        }
        catch (Exception exception)
        {
            return Fail("영구 초기 데이터를 생성하지 못했습니다. " + exception.Message, out error);
        }
    }

    public bool TrySave(out string error)
    {
        if (!CheckReady(out error)) return false;
        try
        {
            return TryWrite(CaptureSaveData(), out error);
        }
        catch (Exception exception)
        {
            return Fail("영구 저장 데이터를 수집하지 못했습니다. " + exception.Message, out error);
        }
    }

    // 콘텐츠가 만든 데이터를 검증하고 파일에 저장만 합니다. 구매나 상태 변경은 하지 않습니다.
    public bool TrySave(PersistentSaveData data, out string error)
    {
        if (!CheckReady(out error)) return false;
        try
        {
            return TryWrite(data, out error);
        }
        catch (Exception exception)
        {
            return Fail("영구 저장 데이터를 준비하지 못했습니다. " + exception.Message, out error);
        }
    }

    // 10.9 / 문규성 / 기본 불러오기는 시작 지급을 생략하도록 명시적인 이어하기 인수를 전달합니다.
    // Inspector에서 다시 불러와도 시작 재화를 다시 지급하지 않습니다. 신규 런은 bool 인수에 false를 전달합니다.
    public bool TryLoad(out string error)
    {
        return TryLoad(true, out error);
    }

    // 10.9 / 문규성 / 신규 런인지 이어하기인지 전달받아 아웃게임 복원과 런 효과 적용을 한 번에 처리하도록 변경했습니다.
    // 시작 지급 여부는 입력 저장 데이터를 변경하지 않고 각 런 Provider에 전달합니다.
    public bool TryLoad(bool isContinue, out string error)
    {
        IsReady = false;
        if (!CheckReferences(out error)) return false;
        if (!_saveManager.TryLoad(SaveKey, out PersistentSaveData data, out error))
            return Fail(error, out error);
        if (!TryValidate(data, out error)) return false;

        try
        {
            Apply(data, isContinue);
            _lastSettledRunId = data.LastSettledRunId;
            IsReady = true;
            LastError = null;
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            return Fail("영구 저장 데이터 복원 실패: " + exception.Message, out error);
        }
    }

    // 10.9 / 문규성 / 연결된 Provider의 현재 사본으로 전체 저장 데이터를 모으고 선택 캐시 사용을 제거했습니다.
    // 영구 파일에는 제단 선택만 유지하므로 런의 시작 지급 상태는 항상 false로 저장합니다.
    public PersistentSaveData CaptureSaveData()
    {
        if (!CheckReferences(out string error)) throw new InvalidOperationException(error);
        AltarRunSaveData altar = _altar.CaptureSaveData();
        altar.isApplied = false;
        return new PersistentSaveData
        {
            Traits = _traits.CaptureSaveData(),
            Wallet = _wallet.CaptureSaveData(),
            Altar = altar,
            Totem = _totem.CaptureSaveData(),
            LastSettledRunId = _lastSettledRunId
        };
    }

    // 10.9 / 문규성 / 완성된 전체 저장 데이터만 받고, 누락된 선택을 캐시에서 보충하던 처리를 제거했습니다.
    // 저장용 루트와 제단 사본을 만들어 시작 지급 상태를 false로 저장하며 호출자가 전달한 데이터는 변경하지 않습니다.
    private bool TryWrite(PersistentSaveData data, out string error)
    {
        if (!TryValidate(data, out error)) return false;
        PersistentSaveData saveData = new PersistentSaveData
        {
            Traits = data.Traits,
            Wallet = data.Wallet,
            Altar = new AltarRunSaveData
            {
                SelectedAltar = data.Altar.SelectedAltar,
                isApplied = false
            },
            Totem = data.Totem,
            LastSettledRunId = string.IsNullOrWhiteSpace(data.LastSettledRunId)
                ? _lastSettledRunId : data.LastSettledRunId
        };
        if (!_saveManager.TrySave(SaveKey, saveData, out error)) return Fail(error, out error);
        _lastSettledRunId = saveData.LastSettledRunId;
        LastError = null;
        return true;
    }

    private bool TryValidate(PersistentSaveData data, out string error)
    {
        if (data == null) return Fail("영구 저장 데이터가 없습니다.", out error);
        if (!_traits.TryValidateSaveData(data.Traits, out error)) return Fail("특성 저장 데이터 검증 실패: " + error, out error);
        if (!_wallet.TryValidateSaveData(data.Wallet, out error)) return Fail("혈석 저장 데이터 검증 실패: " + error, out error);
        return TryValidateSelections(data.Altar, data.Totem, out error);
    }

    private bool TryValidateSelections(AltarRunSaveData altar, TotemRunSaveData totem, out string error)
    {
        if (altar == null || altar.SelectedAltar == AltarId.None ||
            !Enum.IsDefined(typeof(AltarId), altar.SelectedAltar))
            return Fail("제단 저장 데이터의 ID가 올바르지 않습니다.", out error);

        if (totem == null || totem.Totems == null)
            return Fail("토템 저장 데이터의 목록이 없습니다.", out error);

        HashSet<TotemId> seen = new HashSet<TotemId>();
        for (int i = 0; i < totem.Totems.Count; i++)
        {
            TotemLevelEntry entry = totem.Totems[i];
            if (entry == null || entry.Id == TotemId.None || !Enum.IsDefined(typeof(TotemId), entry.Id) ||
                entry.Level < 0 || !seen.Add(entry.Id))
                return Fail("토템 저장 항목의 ID, 레벨과 중복을 확인하세요. Index: " + i, out error);
        }

        error = null;
        return true;
    }

    // 10.9 / 문규성 / 영구 지갑과 특성을 복원한 뒤 특성, 토템, 제단 순서로 기존 런 Provider를 호출하도록 변경했습니다.
    // 특성과 제단의 시작 지급은 isContinue로 판단하며, 영구 저장 데이터의 지급 플래그는 변경하지 않습니다.
    private void Apply(PersistentSaveData data, bool isContinue)
    {
        _wallet.RestoreSaveData(data.Wallet, false);
        _traits.RestoreSaveData(data.Traits, false);
        if (_traitRun != null)
        {
            _traitRun.RestoreSaveData(new TraitRunSaveData
            {
                Levels = data.Traits._levels,
                IsApplied = isContinue
            });
        }
        _totem.RestoreSaveData(data.Totem);
        _altar.RestoreSaveData(new AltarRunSaveData
        {
            SelectedAltar = data.Altar.SelectedAltar,
            isApplied = isContinue
        });

        // 모든 상태가 적용된 다음 UI에 한 번씩 알립니다.
        _wallet.NotifyChanged();
        _traits.NotifyChanged();
    }

    // 10.9 / 문규성 / 선택 캐시 대신 실제 Provider를 사용하므로 제단과 토템 연결도 기존 참조 확인에 포함했습니다.
    private bool CheckReferences(out string error)
    {
        if (_saveManager == null || _traits == null || _wallet == null || _altar == null || _totem == null)
            return Fail("SaveManager, 특성 Selector, 혈석 지갑, 제단과 토템 Provider를 먼저 연결해주세요.", out error);
        if (!_traits.IsInitialized || !_wallet.IsInitialized)
            return Fail("특성 Selector와 혈석 지갑을 먼저 초기화해주세요.", out error);
        error = null;
        return true;
    }

    private bool CheckReady(out string error)
    {
        if (!CheckReferences(out error)) return false;
        if (!IsReady) return Fail("영구 데이터를 먼저 불러오거나 최초 데이터를 생성해주세요.", out error);
        return true;
    }

    private bool Fail(string message, out string error)
    {
        error = message;
        LastError = message;
        return false;
    }

    [ContextMenu("Persistent Save/Save Traits And Bloodstone")]
    private void SaveFromInspector()
    {
        if (TrySave(out string error)) Debug.Log("[PersistentSave] 저장 완료: " + SavePath, this);
        else Debug.LogWarning("[PersistentSave] 저장 실패: " + error, this);
    }

    [ContextMenu("Persistent Save/Load Traits And Bloodstone")]
    private void LoadFromInspector()
    {
        if (TryLoad(out string error)) Debug.Log("[PersistentSave] 복원 완료: " + SavePath, this);
        else Debug.LogWarning("[PersistentSave] 복원 실패: " + error, this);
    }
}
