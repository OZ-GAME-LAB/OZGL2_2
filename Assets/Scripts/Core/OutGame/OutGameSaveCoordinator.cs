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
    public bool HasReadFailure { get; private set; }
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
    private TraitSaveData _initialTraits;
    private PersistentWalletSaveData _initialWallet;
    private string _lastSettledRunId;
    private bool _isInitialized;
    private bool _hasFallbackBackup;
    private bool _requiresBackupBeforeWrite;

    public void Initialize(SaveManager saveManager, OutGameTraitController traits, PersistentCurrencyManager wallet,
        ISaveDataProvider<AltarRunSaveData> altar = null, ISaveDataProvider<TotemRunSaveData> totem = null)
    {
        HasReadFailure = false;
        if (_isInitialized)
            throw new InvalidOperationException("OutGameSaveCoordinator는 씬에서 한 번만 초기화할 수 있습니다.");

        _saveManager = saveManager;
        _traits = traits;
        _wallet = wallet;
        _altar = altar;
        _totem = totem;
        _initialTraits = traits != null ? traits.CaptureSaveData() : null;
        _initialWallet = wallet != null ? wallet.CaptureSaveData() : null;
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
        HasReadFailure = false;
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
        HasReadFailure = false;
        if (!CheckReferences(out error)) return false;
        if (!_saveManager.TryLoad(SaveKey, out PersistentSaveData data, out error))
        {
            HasReadFailure = true;
            return Fail(error, out error);
        }
        if (data == null)
        {
            HasReadFailure = true;
            return Fail("영구 저장 데이터가 없습니다.", out error);
        }

        // 인게임에는 선택 Selector가 없어도 선택 설정과 지급 영수증은 유지합니다.
        _lastSettledRunId = data.LastSettledRunId;
        bool legacyAltar = data.Altar == null || data.Altar.SelectedAltar == AltarId.None;
        bool legacyTotem = data.Totem == null;
        AltarRunSaveData altarData = Clone(data.Altar);
        TotemRunSaveData totemData = Clone(data.Totem);
        if (!_restoreAltar)
        {
            _cachedAltar = Clone(_initialAltar);
        }
        else if (legacyAltar)
        {
            altarData = new AltarRunSaveData { SelectedAltar = AltarId.Abundance };
            LogInitialValue("제단", "구버전 선택값이 없어 풍요의 제단을 사용합니다.");
        }
        if (!_restoreTotem)
        {
            _cachedTotem = Clone(_initialTotem);
        }
        else if (legacyTotem)
        {
            totemData = new TotemRunSaveData();
            LogInitialValue("토템", "구버전 선택값이 없어 0레벨을 사용합니다.");
        }

        bool needsBackup = !_restoreTraits || !_restoreWallet || !_restoreAltar || !_restoreTotem || legacyAltar || legacyTotem;
        if (needsBackup) RequireBackupBeforeNextWrite();

        if (!TryRestorePart("혈석", _restoreWallet,
                () => _wallet.CaptureSaveData(),
                () => _wallet.RestoreSaveData(data.Wallet, false),
                snapshot => _wallet.RestoreSaveData(snapshot, false), out error)) return false;
        if (!TryRestorePart("특성", _restoreTraits,
                () => _traits.CaptureSaveData(),
                () => _traits.RestoreSaveData(data.Traits, false),
                snapshot => _traits.RestoreSaveData(snapshot, false), out error)) return false;
        if (!TryRestorePart("제단", _restoreAltar,
                () => _altar != null ? Clone(_altar.CaptureSaveData()) : Clone(_initialAltar),
                () =>
                {
                    if (!TryValidateSelections(altarData, null, true, false, out string validationError))
                        throw new ArgumentException(validationError);
                    RestoreAltar(altarData);
                }, RestoreAltar, out error)) return false;
        if (!TryRestorePart("토템", _restoreTotem,
                () => _totem != null ? Clone(_totem.CaptureSaveData()) : Clone(_initialTotem),
                () =>
                {
                    if (!TryValidateSelections(null, totemData, false, true, out string validationError))
                        throw new ArgumentException(validationError);
                    RestoreTotem(totemData);
                }, RestoreTotem, out error)) return false;

        return FinishRestore(out error);
    }

    // 파일 전체를 읽지 못한 경우에도 이미 초기화한 기본값으로 조립 테스트를 시작한다.
    // 여기서는 파일을 쓰지 않고 다음 저장 전에만 원본 백업을 요구한다.
    public bool TryUseInitialState(out string error)
    {
        IsReady = false;
        if (!CheckReferences(out error)) return false;
        if (_initialWallet == null || _initialTraits == null || _initialAltar == null || _initialTotem == null)
            return Fail("초기 영구 데이터 스냅샷이 없습니다.", out error);
        RequireBackupBeforeNextWrite();
        try
        {
            _wallet.RestoreSaveData(_initialWallet, false);
            _traits.RestoreSaveData(_initialTraits, false);
            RestoreAltar(_initialAltar);
            RestoreTotem(_initialTotem);
            // 이미 읽어 둔 정산 영수증은 초기값 복구로 지우지 않는다.
        }
        catch (Exception exception)
        {
            return Fail("초기 영구 상태 복구 실패: " + exception.Message, out error);
        }
        return FinishRestore(out error);
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
        if (_requiresBackupBeforeWrite && !_hasFallbackBackup)
        {
            if (!_saveManager.TryBackup(SaveKey, out string backupPath, out error))
                return Fail("초기값 저장 전 원본 백업에 실패했습니다. " + error, out error);
            _hasFallbackBackup = backupPath != null;
            _requiresBackupBeforeWrite = false;
        }
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

    private bool TryRestorePart<T>(string name, bool enabled, Func<T> capture, Action apply,
        Action<T> rollback, out string error) where T : class
    {
        error = null;
        if (!enabled)
        {
            RequireBackupBeforeNextWrite();
            LogInitialValue(name, "복원이 비활성화되어 초기값을 유지합니다.");
            return true;
        }
        T snapshot;
        try
        {
            snapshot = capture();
            if (snapshot == null) throw new InvalidOperationException("초기 상태 스냅샷이 없습니다.");
        }
        catch (Exception exception)
        {
            return Fail(name + " 복원 전 스냅샷 생성 실패: " + exception.Message, out error);
        }
        try
        {
            apply();
            return true;
        }
        catch (Exception exception)
        {
            RequireBackupBeforeNextWrite();
            try
            {
                rollback(snapshot);
            }
            catch (Exception rollbackException)
            {
                return Fail(name + " 복원 실패 후 초기 상태 복구도 실패했습니다. " +
                    exception.Message + " / " + rollbackException.Message, out error);
            }
            Debug.LogError("[OutGameSave] " + name + " 복원 실패: " + exception.Message +
                " 초기 상태를 유지하고 나머지 복원을 계속합니다.", this);
            return true;
        }
    }

    private void RestoreAltar(AltarRunSaveData data)
    {
        if (_altar != null)
        {
            _altar.RestoreSaveData(Clone(data));
            AltarRunSaveData current = _altar.CaptureSaveData();
            if (current == null || current.SelectedAltar != data.SelectedAltar)
                throw new InvalidOperationException("제단 선택값이 적용되지 않았습니다.");
        }
        _cachedAltar = Clone(data);
    }

    private void RestoreTotem(TotemRunSaveData data)
    {
        if (_totem != null) _totem.RestoreSaveData(Clone(data));
        _cachedTotem = Clone(data);
    }

    private bool FinishRestore(out string error)
    {
        try
        {
            // 모든 상태가 적용된 다음 UI에 한 번씩 알린다.
            _wallet.NotifyChanged();
            _traits.NotifyChanged();
        }
        catch (Exception exception)
        {
            return Fail("영구 상태 변경 알림 실패: " + exception.Message, out error);
        }
        IsReady = true;
        LastError = null;
        error = null;
        return true;
    }

    private void RequireBackupBeforeNextWrite()
    {
        if (!_hasFallbackBackup) _requiresBackupBeforeWrite = true;
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
