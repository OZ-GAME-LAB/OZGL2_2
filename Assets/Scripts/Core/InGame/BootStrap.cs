using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Cameras;
using Game.Core;
using Game.UI.InGame;
using OZGL.KDH;
using Units;
using UnityEngine;
using UnityEngine.Serialization;
/// 각 시스템의 참조 연결과 초기화 순서를 관리하고,
/// 준비가 완료되면 GameFlowController.BeginRun()을 호출한다.
///
/// 각 담당 파트 요청 사항:
/// - 외부 연결이 필요한 대표 매니저와 Initialize 메서드를 제공
/// - Initialize에서 필요한 참조를 전달받고 내부 준비와 이벤트 구독을 처리
/// - 필요한 참조와 선행 초기화 대상은 구현 전에 공유
/// - 모든 시스템 초기화 이후 실행해야 하는 작업이 있다면 별도 시작 메서드를 제공하고 호출 시점 공지
/// - 비동기 초기화는 완료까지 기다릴 수 있도록 UniTask를 반환
/// - 구현 후 Bootstrap에서 연결 가능한 시점에 안내
///
/// 초기화 중 다른 시스템에 게임 진행을 요청하는 이벤트는 발생시키지 않고
/// 이벤트 구독 해제와 재초기화 시 중복 구독 방지는 각 시스템에서 처리.
public class BootStrap : MonoBehaviour
{
    #region Systems
    [Header("Systems")]
    [SerializeField] private TestWaitingScript _testScript; //테스트용으로 , 실제 구현시 삭제할것
    [SerializeField] private GameFlowController _gameFlowController;
    [SerializeField] private WaveController _waveController;
    [SerializeField] private ArchiveManager _archiveManager;
    [SerializeField] private ArtifactManager _artifactManager;
    [SerializeField] private EffectManager _effectManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private RuntimeUnitManager _runtimeUnitManager;
    [SerializeField] private RunCurrencyManager _runCurrencyManager;
    [SerializeField] private ShopManager _shopManager;
    [SerializeField] private ConsumableItemManager _consumableItemManager;
    [SerializeField] private RunSettlementManager _runSettlementManager;
    [SerializeField] private PersistentCurrencyManager _persistentCurrencyManager;
    [SerializeField] private InGameCameraController _cameraController;
    [SerializeField] private BuildingBuildController _buildController;
    [SerializeField] private BuildingCoreProgress _buildingCoreProgress;
    [SerializeField] private BuildingCensus _buildingCensus;
    #endregion

    #region Save
    [Header("Save")]
    [SerializeField] private InGameSaveCoordinator _inGameSaveCoordinator;
    [FormerlySerializedAs("_persistentSaveCoordinator")]
    [SerializeField] private OutGameSaveCoordinator _outGameSaveCoordinator;
    [SerializeField] private SaveManager _saveManager;
    #endregion

    #region UI
    [Header("UI")]
    [SerializeField] private InGameUIStartup _uiStartup;
    public bool IsUIConnected => _uiStartup != null && _uiStartup.IsReady;
    #endregion

    #region OutGames
    // Current date KDH 2026-09-29
    // 아웃게임 효과(특성·토템·제단)입니다. 연결하지 않아도 게임은 시작됩니다.
    [SerializeField] private OutGameTraitController _persistentTraits;
    [Header("OutGame Effects (KDH)")]
    [Tooltip("SpawnManager에 연결된 것과 같은 오브젝트여야 스폰 유닛에 반영됩니다.")]
    [SerializeField] private UnitStatModifierManager _unitStatModifierManager;
    [SerializeField] private TraitRunApplier _traitRunApplier;
    [SerializeField] private TraitCatalog _traitCatalog;
    [SerializeField] private TotemRunApplier _totemRunApplier;
    [SerializeField] private TotemEffectCatalog _totemCatalog;
    [SerializeField] private AltarManager _altarManager;
    #endregion
    //각자 대표매니저 1개 만들고 각각 필요한 참조를 말하면 제공
    public bool IsContinue => _isContinue;

    private bool _isContinue;
    
    async void Start()
    {
        if (!ValidateReferences()) return;
        try
        {
            if (!InitializePersistentData() || !InitializeManagers())
            {
                _gameFlowController.BlockStartup();
                return;
            }
            // 모든 슬롯의 Start에서 기본 배치 연결이 끝난 뒤 UI와 저장 상태를 연결한다.
            await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
            if (!InitializeUI())
            {
                _gameFlowController.BlockStartup();
                return;
            }
            _testScript.Initialize(_gameFlowController, _waveController);
            _inGameSaveCoordinator.Initialize(_saveManager, _gameFlowController, _runCurrencyManager,
                _artifactManager, _shopManager, _consumableItemManager, _buildController, _archiveManager,
                () => _archiveManager.Initialize(_artifactManager, _runtimeUnitManager, _waveController));
            _gameFlowController.InitializeSave(_inGameSaveCoordinator);

            bool hasRun = _saveManager.HasSaveFile(InGameSaveCoordinator.SaveKey);
            InGameSaveData loaded = null;
            if (hasRun)
            {
                if (!_inGameSaveCoordinator.TryReadSaveData(out loaded, out string readError))
                {
                    StopStartup(readError);
                    return;
                }
                if (loaded.Flow == null)
                {
                    StopStartup("필수 Flow 진행값이 없습니다.");
                    return;
                }
                // 결산 파일을 읽은 직후 판정한다. 다른 파트가 미완성이어도 재지급하지 않는다.
                if (loaded.Flow.ResumeStep == RunResumeStep.Finished ||
                    (!string.IsNullOrWhiteSpace(loaded.Flow.RunId) &&
                     string.Equals(loaded.Flow.RunId, _outGameSaveCoordinator.LastSettledRunId, StringComparison.Ordinal)))
                {
                    _gameFlowController.PrepareCompletedRun(loaded.Flow.RunId);
                    _gameFlowController.BeginRun();
                    return;
                }
            }

            var profile = _outGameSaveCoordinator.CaptureSaveData();
            if (hasRun && !CanRestoreOutGameEffects(profile, loaded.Flow, out string effectError))
            {
                StopStartup(effectError);
                return;
            }
            InitializeOutGameEffects(profile);
            if (hasRun)
            {
                if (!_inGameSaveCoordinator.TryApplySaveData(loaded, out string restoreError))
                {
                    StopStartup(restoreError);
                    return;
                }
                if (string.IsNullOrWhiteSpace(loaded.Flow.RunId))
                    _inGameSaveCoordinator.RequireBackupBeforeNextWrite();
            }

            string saveError;
            bool saved = hasRun ? _inGameSaveCoordinator.TrySave(out saveError) :
                _inGameSaveCoordinator.TryCreateSave(out saveError);
            if (!saved)
            {
                StopStartup(saveError);
                return;
            }
            _gameFlowController.BeginRun();
        }
        catch (OperationCanceledException) when (this == null) { }
        catch (Exception exception)
        {
            StopStartup(exception.Message);
        }
    }

    private void StopStartup(string error)
    {
        _gameFlowController?.BlockStartup();
        Debug.LogError("[Save/BootStrap] 진입 중단: " + error, this);
    }

    private bool InitializeManagers()
    {
        _gameFlowController.Initialize
            (_waveController, _testScript, _artifactManager, _archiveManager, _runSettlementManager, _cameraController, _shopManager);
        _waveController.Initialize(_gameFlowController, _spawnManager, _runtimeUnitManager);
        _buildController.Initialize(_runCurrencyManager, _gameFlowController, _buildingCoreProgress, _buildingCensus);
        _artifactManager.Initialize(_waveController, _effectManager, _runCurrencyManager, _uiStartup.ArtifactSelectionUI);
        _uiStartup.InitializeArtifactInventory(_artifactManager);
        _runCurrencyManager.Initialize(_waveController,_gameFlowController, _effectManager, _buildingCoreProgress);
        _consumableItemManager.Initialize(_effectManager, _gameFlowController, _runtimeUnitManager,
            Units.Skills.SkillEffectResolver.Instance);
        if (!_consumableItemManager.IsInitialized)
        {
            Debug.LogError("[BootStrap] 상점에 연결할 소모품 매니저 초기화에 실패했습니다.", this);
            return false;
        }

        _shopManager.Initialize(_artifactManager, _runCurrencyManager, _gameFlowController, _consumableItemManager, _uiStartup.ShopUI);
        if (!_shopManager.IsInitialized)
        {
            Debug.LogError("[BootStrap] 상점 매니저 초기화에 실패했습니다.", this);
            return false;
        }

        var settlementBridge = new RunSettlementSaveBridge(_outGameSaveCoordinator,
            () => _gameFlowController.RunId, _uiStartup.SettlementUI,
            _gameFlowController.PrepareSettlementViewAsync);
        _runSettlementManager.Initialize(_waveController, _effectManager, _persistentCurrencyManager, _totemRunApplier,
            settlementBridge, settlementBridge);
        _cameraController.Initialize(_buildController,_gameFlowController);
        _traitRunApplier.Initialize(_traitCatalog, _effectManager, _unitStatModifierManager,
            _runCurrencyManager, _gameFlowController);
        _totemRunApplier.Initialize(_totemCatalog, _unitStatModifierManager, _effectManager, _gameFlowController);
        _altarManager.Initialize(_effectManager, _runCurrencyManager, _unitStatModifierManager,
            _gameFlowController);

        return true;
    }
    // 저장용 특성은 런 효과용 선택 목록과 별개다. 파일에서 복원한 특성을 그대로 보존한다.
    private bool InitializePersistentData()
    {
        _persistentCurrencyManager.Initialize();
        _persistentTraits.Initialize(_persistentCurrencyManager, _outGameSaveCoordinator);
        _outGameSaveCoordinator.Initialize(_saveManager, _persistentTraits, _persistentCurrencyManager);
        if (_outGameSaveCoordinator.TryLoadOrCreate(out string error))
            return true;
        Debug.LogError("[BootStrap] 영구 데이터 초기화 실패: " + error, this);
        return false;
    }
    // UI 부분을 초기화 한다
    public bool InitializeUI()
    {
        var buildingSlots = new List<BuildingSlot>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            buildingSlots.AddRange(root.GetComponentsInChildren<BuildingSlot>(true));
        if (!_uiStartup.Initialize(_runCurrencyManager, _gameFlowController,
                _waveController, _buildController, _buildingCoreProgress, buildingSlots.ToArray(), _cameraController))
        {
            Debug.LogError("[BootStrap] UI 초기화에 실패했습니다.", this);
            return false;
        }
        _uiStartup.UIManager.gameObject.SetActive(true);
        return true;
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        if (_shopManager == null || _consumableItemManager == null)
        {
            Debug.LogError("[BootStrap] ShopManager와 ConsumableItemManager 참조를 연결해주세요.", this);
            valid = false;
        }
        if (_persistentCurrencyManager == null || _runSettlementManager == null || _saveManager == null ||
            _outGameSaveCoordinator == null || _inGameSaveCoordinator == null || _persistentTraits == null ||
            _archiveManager == null || _runCurrencyManager == null || _buildController == null || _cameraController == null)
        {
            Debug.LogError("[BootStrap] 정산 매니저·혈석 지갑·SaveManager·영구 저장·특성 데이터 참조를 연결해주세요.", this);
            valid = false;
        }
        if (_uiStartup == null)
        {
            Debug.LogError("[BootStrap] _uiStartup 참조가 없습니다. UI 초기화 컴포넌트를 연결해주세요.", this);
            valid = false;
        }
        if (_testScript == null)
        {
            Debug.LogError("[BootStrap] _testScript 참조가 없습니다. Inspector에서 연결해주세요.", this);
            valid = false;
        }
        if (_gameFlowController == null)
        {
            Debug.LogError("[BootStrap] _gameFlowController 참조가 없습니다. Inspector에서 연결해주세요.", this);
            valid = false;
        }
        if (_waveController == null)
        {
            Debug.LogError("[BootStrap] _waveController 참조가 없습니다. Inspector에서 연결해주세요.", this);
            valid = false;
        }
        if (_spawnManager == null)
        {
            Debug.LogError("[BootStrap] _spawnManager가 없습니다. 생성·주입 코드를 확인해주세요.", this);
            valid = false;
        }
        if (_runtimeUnitManager == null)
        {
            Debug.LogError("[BootStrap] _runtimeUnitManager가 없습니다. 생성·주입 코드를 확인해주세요.", this);
            valid = false;
        }
        if (_artifactManager == null)
        {
            Debug.LogError("[BootStrap] _artifactManager가 없습니다. 생성·주입 코드를 확인해주세요.", this);
            valid = false;
        }

        return valid;
    }

    // 기존 API는 특성·제단 효과와 시작 지급을 분리하지 못하므로 적용 전에 차단한다.
    private bool CanRestoreOutGameEffects(PersistentSaveData profile, GameFlowSaveData flow, out string error)
    {
        var unsupported = new List<string>();
        if (_traitRunApplier != null && profile.Traits?._levels != null)
            foreach (var entry in profile.Traits._levels)
                if (entry != null && entry.Level > 0)
                {
                    unsupported.Add("Trait");
                    break;
                }
        if (_altarManager != null && profile.Altar != null && profile.Altar.SelectedAltar != AltarId.None)
            unsupported.Add("Altar");
        // 토템은 첫 Preparation에서 지급 없이 효과를 구성할 수 있다.
        if (_totemRunApplier != null && flow.ResumeStep != RunResumeStep.Preparation && profile.Totem?.Totems != null)
            foreach (var entry in profile.Totem.Totems)
                if (entry != null && entry.Level > 0)
                {
                    unsupported.Add("Totem");
                    break;
                }
        if (unsupported.Count == 0)
        {
            error = null;
            return true;
        }
        error = string.Join(", ", unsupported) + " 이어하기는 효과만 복구하는 API가 필요합니다. " +
            "복원·자동 저장·BeginRun을 실행하지 않고 저장 원본을 보존합니다.";
        return false;
    }

    // 신규 런의 효과와 시작 재화는 기존 파트의 첫 Preparation 처리에서 적용한다.
    private void InitializeOutGameEffects(PersistentSaveData profile)
    {
        // 저장된 영구 특성으로 이번 씬의 효과를 다시 구성한다.
        if (_traitRunApplier != null)
        {
            _traitRunApplier.SetLevels(_persistentTraits.CaptureLevels());
        }
        // 토템: catalog가 없어도 기본 스탯 효과(TotemBuiltinEffects)는 적용됩니다.
        if (_totemRunApplier != null)
        {
            _totemRunApplier.RestoreSaveData(profile.Totem);
        }
        // 제단 선택 복원만 수행하고 효과 적용은 기존 페이즈 처리에 맡긴다.
        if (_altarManager != null)
        {
            _altarManager.RestoreSaveData(profile.Altar);
            if (!_altarManager.IsInitialized ||
                _altarManager.CaptureSaveData().SelectedAltar != profile.Altar.SelectedAltar)
                throw new InvalidOperationException("제단 초기화 또는 선택 복원에 실패했습니다.");
        }
    }
}
