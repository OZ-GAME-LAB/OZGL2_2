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

    // 아직 복원을 지원하지 않는 영역은 호출 전에 제외해 초기화된 값을 유지합니다.
    [SerializeField] private bool _restoreTraits = true;
    [SerializeField] private bool _restoreWallet = true;
    [SerializeField] private bool _restoreAltar = true;
    [SerializeField] private bool _restoreTotem = true;

    private SaveManager _saveManager;
    private OutGameTraitController _traits;
    private PersistentCurrencyManager _wallet;
    private ISaveDataProvider<AltarRunSaveData> _altar;
    private ISaveDataProvider<TotemRunSaveData> _totem;
    private AltarRunSaveData _cachedAltar;
    private TotemRunSaveData _cachedTotem;
    private AltarRunSaveData _initialAltar;
    private TotemRunSaveData _initialTotem;
    private string _lastSettledRunId;
    private string _restoringPart;
    private bool _isInitialized;
    private bool _hasFallbackBackup;

    public void Initialize(SaveManager saveManager, OutGameTraitController traits, PersistentCurrencyManager wallet,
        ISaveDataProvider<AltarRunSaveData> altar = null, ISaveDataProvider<TotemRunSaveData> totem = null)
    {
        if (_isInitialized)
            throw new InvalidOperationException("OutGameSaveCoordinator는 씬에서 한 번만 초기화할 수 있습니다.");

        _saveManager = saveManager;
        _traits = traits;
        _wallet = wallet;
        _altar = altar;
        _totem = totem;
        _initialAltar = altar != null ? Clone(altar.CaptureSaveData()) : new AltarRunSaveData { SelectedAltar = AltarId.Abundance };
        _initialTotem = totem != null ? Clone(totem.CaptureSaveData()) : new TotemRunSaveData();
        _cachedAltar = Clone(_initialAltar);
        _cachedTotem = Clone(_initialTotem);
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

    public bool TryLoad(out string error)
    {
        IsReady = false;
        if (!CheckReferences(out error)) return false;
        if (!_saveManager.TryLoad(SaveKey, out PersistentSaveData data, out error))
            return Fail(error, out error);
        if (data == null) return Fail("영구 저장 데이터가 없습니다.", out error);

        // 인게임에는 선택 Selector가 없어도 선택 설정과 지급 영수증은 유지합니다.
        CacheMetadata(data);
        bool legacyAltar = data.Altar == null || data.Altar.SelectedAltar == AltarId.None;
        bool legacyTotem = data.Totem == null;
        if (!_restoreAltar)
        {
            _cachedAltar = Clone(_initialAltar);
            LogInitialValue("제단", "복원이 비활성화되어 초기값을 유지합니다.");
        }
        else if (legacyAltar)
        {
            _cachedAltar = new AltarRunSaveData { SelectedAltar = AltarId.Abundance };
            LogInitialValue("제단", "구버전 선택값이 없어 풍요의 제단을 사용합니다.");
        }
        if (!_restoreTotem)
        {
            _cachedTotem = Clone(_initialTotem);
            LogInitialValue("토템", "복원이 비활성화되어 초기값을 유지합니다.");
        }
        else if (legacyTotem)
        {
            _cachedTotem = new TotemRunSaveData();
            LogInitialValue("토템", "구버전 선택값이 없어 0레벨을 사용합니다.");
        }

        if (_restoreTraits && !_traits.TryValidateSaveData(data.Traits, out error))
            return Fail("특성 저장 데이터 검증 실패: " + error, out error);
        if (_restoreWallet && !_wallet.TryValidateSaveData(data.Wallet, out error))
            return Fail("혈석 저장 데이터 검증 실패: " + error, out error);
        if (!TryValidateSelections(_cachedAltar, _cachedTotem, _restoreAltar, _restoreTotem, out error))
            return false;

        bool needsBackup = !_restoreTraits || !_restoreWallet || !_restoreAltar || !_restoreTotem || legacyAltar || legacyTotem;
        if (needsBackup && !_hasFallbackBackup)
        {
            if (!_saveManager.TryBackup(SaveKey, out _, out error))
                return Fail("초기값을 사용하기 전 원본 백업에 실패했습니다. " + error, out error);
            _hasFallbackBackup = true;
        }

        try
        {
            Apply(data);
            // 복원과 알림까지 성공해야 구매 및 저장 요청을 받습니다.
            IsReady = true;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            return Fail(_restoringPart + " 복원에 실패했습니다. 저장을 중단합니다. " + exception.Message, out error);
        }
        finally
        {
            _restoringPart = null;
        }
    }

    public PersistentSaveData CaptureSaveData()
    {
        if (!CheckReferences(out string error)) throw new InvalidOperationException(error);
        return new PersistentSaveData
        {
            Traits = _traits.CaptureSaveData(),
            Wallet = _wallet.CaptureSaveData(),
            Altar = _altar != null ? Clone(_altar.CaptureSaveData()) : Clone(_cachedAltar),
            Totem = _totem != null ? Clone(_totem.CaptureSaveData()) : Clone(_cachedTotem),
            LastSettledRunId = _lastSettledRunId
        };
    }

    private bool TryWrite(PersistentSaveData data, out string error)
    {
        if (data == null) return Fail("영구 저장 데이터가 없습니다.", out error);
        // 부분 데이터로 저장을 요청해도 이미 확정된 선택과 영수증을 지우지 않습니다.
        if (!_restoreAltar)
            data.Altar = _altar != null ? Clone(_altar.CaptureSaveData()) : Clone(_cachedAltar);
        else if (data.Altar == null) data.Altar = Clone(_cachedAltar);
        if (!_restoreTotem)
            data.Totem = _totem != null ? Clone(_totem.CaptureSaveData()) : Clone(_cachedTotem);
        else if (data.Totem == null) data.Totem = Clone(_cachedTotem);
        if (string.IsNullOrWhiteSpace(data.LastSettledRunId)) data.LastSettledRunId = _lastSettledRunId;
        if (!TryValidate(data, out error)) return false;
        if (!_saveManager.TrySave(SaveKey, data, out error)) return Fail(error, out error);
        CacheMetadata(data);
        LastError = null;
        return true;
    }

    private bool TryValidate(PersistentSaveData data, out string error)
    {
        if (data == null) return Fail("영구 저장 데이터가 없습니다.", out error);
        if (!_traits.TryValidateSaveData(data.Traits, out error)) return Fail("특성 저장 데이터 검증 실패: " + error, out error);
        if (!_wallet.TryValidateSaveData(data.Wallet, out error)) return Fail("혈석 저장 데이터 검증 실패: " + error, out error);
        return TryValidateSelections(data.Altar, data.Totem, true, true, out error);
    }

    private bool TryValidateSelections(AltarRunSaveData altar, TotemRunSaveData totem,
        bool validateAltar, bool validateTotem, out string error)
    {
        if (validateAltar && (altar == null || altar.SelectedAltar == AltarId.None ||
            !Enum.IsDefined(typeof(AltarId), altar.SelectedAltar)))
            return Fail("제단 저장 데이터의 ID가 올바르지 않습니다.", out error);

        if (validateTotem)
        {
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
        }

        error = null;
        return true;
    }

    private void Apply(PersistentSaveData data)
    {
        _restoringPart = "혈석";
        if (_restoreWallet) _wallet.RestoreSaveData(data.Wallet, false);
        else LogInitialValue("혈석", "복원이 비활성화되어 초기값을 유지합니다.");
        _restoringPart = "특성";
        if (_restoreTraits) _traits.RestoreSaveData(data.Traits, false);
        else LogInitialValue("특성", "복원이 비활성화되어 초기값을 유지합니다.");
        if (_altar != null)
        {
            _restoringPart = "제단";
            if (_restoreAltar) _altar.RestoreSaveData(Clone(_cachedAltar));
        }
        if (_totem != null)
        {
            _restoringPart = "토템";
            if (_restoreTotem) _totem.RestoreSaveData(Clone(_cachedTotem));
        }
        // 모든 상태가 적용된 다음 UI에 알립니다.
        _restoringPart = "혈석 변경 알림";
        _wallet.NotifyChanged();
        _restoringPart = "특성 변경 알림";
        _traits.NotifyChanged();
    }

    private void CacheMetadata(PersistentSaveData data)
    {
        _cachedAltar = Clone(data.Altar);
        _cachedTotem = Clone(data.Totem);
        _lastSettledRunId = data.LastSettledRunId;
    }

    private static AltarRunSaveData Clone(AltarRunSaveData data)
    {
        return data == null ? null : new AltarRunSaveData { SelectedAltar = data.SelectedAltar };
    }

    private static TotemRunSaveData Clone(TotemRunSaveData data)
    {
        if (data == null) return null;
        TotemRunSaveData copy = new TotemRunSaveData
        {
            Totems = data.Totems == null ? null : new List<TotemLevelEntry>()
        };
        if (data.Totems != null)
        {
            foreach (TotemLevelEntry entry in data.Totems)
                copy.Totems.Add(entry == null ? null : new TotemLevelEntry { Id = entry.Id, Level = entry.Level });
        }
        return copy;
    }

    private void LogInitialValue(string part, string reason)
    {
        Debug.LogWarning("[OutGameSave] " + part + ": " + reason, this);
    }

    private bool CheckReferences(out string error)
    {
        if (_saveManager == null || _traits == null || _wallet == null)
            return Fail("SaveManager, 특성 Selector, 혈석 지갑을 먼저 연결해주세요.", out error);
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
