using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Archive;
using Game.Cameras;
using Game.Core;
using Game.UI.InGame;
using OZGL.KDH;
using Units;
using Units.Skills;
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
    [SerializeField] private EventManager _eventManager;
    [SerializeField] private ConsumableItemManager _consumableItemManager;
    [SerializeField] private RunRewardManager _runSettlementManager;
    [SerializeField] private PersistentCurrencyManager _persistentCurrencyManager;
    [SerializeField] private InGameCameraController _cameraController;
    [SerializeField] private BuildingBuildController _buildController;
    [SerializeField] private BuildingCoreProgress _buildingCoreProgress;
    [SerializeField] private BuildingCensus _buildingCensus;
    #endregion

    #region Save
    [Header("Save")]
    [SerializeField] private InGameSaveCoordinator _inGameSaveCoordinator;
    [SerializeField] private OutGameSaveCoordinator _outGameSaveCoordinator;
    [SerializeField] private SaveManager _saveManager;
    #endregion

    #region UI
    [Header("UI")]
    [SerializeField] private InGameUIStartup _uiStartup;
    [SerializeField] private ArchiveController _archiveController;
    private BuildingSlot[] _buildingSlots;
    public bool IsUIConnected => _uiStartup != null && _uiStartup.IsReady;
    #endregion

    #region OutGames
    // Current date KDH 2026-09-29
    // 아웃게임에서 확정한 특성·토템·제단을 이번 Run에 적용합니다.
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

    async void Start()
    {

        if (!ValidateReferences()) return;
        try
        {
            InitializePersistentManagers();
            if (!InitializeManagers())
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
            if (!_eventManager.Initialize(_runCurrencyManager, _artifactManager, _gameFlowController,
                    _uiStartup.EventUI, _uiStartup.ArtifactSelectionUI))
            {
                StopStartup("EventManager 초기화에 실패했습니다.");
                return;
            }
            _gameFlowController.InitializeEventFlow(_eventManager);
            _testScript.Initialize(_gameFlowController, _waveController);
            _inGameSaveCoordinator.Initialize(_saveManager, _gameFlowController, _runCurrencyManager,
                _artifactManager, _shopManager, _consumableItemManager, _buildController, _archiveManager, _eventManager,
                () => _archiveManager.Initialize(_artifactManager, _runtimeUnitManager, _waveController,
                    _totemRunApplier, _altarManager, _traitRunApplier));
            _gameFlowController.InitializeSave(_inGameSaveCoordinator);
            _eventManager.InitializeSave(_inGameSaveCoordinator);

            bool hasRunSave = _saveManager.HasSaveFile(InGameSaveCoordinator.SaveKey);
            if (!_outGameSaveCoordinator.TryLoad(hasRunSave, out string error)
                || !TryPrepareRun(hasRunSave, out error))
            {
                StopStartup(error);
                return;
            }
            _archiveController.Initialize(_saveManager, _artifactManager, _buildingSlots,
                _gameFlowController, _runtimeUnitManager);
            _uiStartup.InitializeArchive(_archiveController);
            _gameFlowController.BeginRun();
        }
        catch (OperationCanceledException) when (this == null) { }
        catch (Exception exception)
        {
            StopStartup(exception.Message);
        }
    }

    private bool TryPrepareRun(bool hasRunSave, out string error)
    {
        if (hasRunSave)
        {
            if (!_inGameSaveCoordinator.TryReadSaveData(out InGameSaveData loaded, out error)) return false;

            if (loaded?.Flow == null)
            {
                error = "필수 Flow 진행값이 없습니다.";
                return false;
            }

            // 결산창에서 게임을 종료할 시 보상수령이 완료된 게임으로 판정해서 준비하지 않음
            if (loaded.Flow.ResumeStep == RunResumeStep.Finished ||
                (!string.IsNullOrWhiteSpace(loaded.Flow.RunId) &&
                 string.Equals(loaded.Flow.RunId, _outGameSaveCoordinator.LastSettledRunId, StringComparison.Ordinal)))
            {
                _gameFlowController.PrepareCompletedRun(loaded.Flow.RunId);
                return true;
            }

            if (!_inGameSaveCoordinator.TryApplySaveData(loaded, out error)) return false;
            if (string.IsNullOrWhiteSpace(loaded.Flow.RunId))
                _inGameSaveCoordinator.RequireBackupBeforeNextWrite();
            return true;
        }

        // 시작 지급까지 마친 상태를 첫 인게임 스냅샷으로 저장합니다.
        return _inGameSaveCoordinator.TryCreateSave(out error);
    }

    private void StopStartup(string error)
    {
        _gameFlowController?.BlockStartup();
        Debug.LogError("[Save/BootStrap] 진입 중단: " + error, this);
    }

    private bool InitializeManagers()
    {
        _gameFlowController.Initialize(_waveController, _testScript, _artifactManager, _archiveManager, 
            _runSettlementManager, _cameraController, _shopManager);
        _waveController.Initialize(_gameFlowController, _spawnManager, _runtimeUnitManager,
            _totemRunApplier.FlowEffects, _unitStatModifierManager);
        _buildController.Initialize(_runCurrencyManager, _gameFlowController, _buildingCoreProgress, _inGameSaveCoordinator,_buildingCensus);
        _gameFlowController.InitializeBuildings(_buildController.Census);
        _artifactManager.Initialize(_waveController, _effectManager, _runCurrencyManager, _uiStartup.ArtifactSelectionUI);
        _runCurrencyManager.Initialize(_waveController,_gameFlowController, _effectManager, _buildingCoreProgress);
        _uiStartup.InitializeArtifactInventory(_artifactManager);

        _consumableItemManager.Initialize(_effectManager, _gameFlowController, _runtimeUnitManager, SkillEffectResolver.Instance);
        if (!_consumableItemManager.IsInitialized)
        {
            Debug.LogError("[BootStrap] ConsumableItemManager 초기화에 실패했습니다.", this);
            return false;
        }

        _shopManager.Initialize(_artifactManager, _runCurrencyManager, _gameFlowController, _consumableItemManager, _uiStartup.ShopUI, _inGameSaveCoordinator);
        if (!_shopManager.IsInitialized)
        {
            Debug.LogError("[BootStrap] ShopManager 초기화에 실패했습니다.", this);
            return false;
        }

        var settlementBridge = new RunSettlementSaveBridge(_outGameSaveCoordinator,
            () => _gameFlowController.RunId, _uiStartup.SettlementUI,
            _gameFlowController.PrepareSettlementViewAsync);
        _runSettlementManager.Initialize(_waveController, _effectManager, _persistentCurrencyManager, _totemRunApplier, 
            settlementBridge, settlementBridge);
        
        _cameraController.Initialize(_buildController,_gameFlowController);
        _traitRunApplier.Initialize(_traitCatalog, _effectManager, _unitStatModifierManager, _runCurrencyManager, _gameFlowController);
        _totemRunApplier.Initialize(_totemCatalog, _unitStatModifierManager, _effectManager, _gameFlowController);
        _altarManager.Initialize(_effectManager, _runCurrencyManager, _unitStatModifierManager, _gameFlowController);

        return true;
    }
    // 아웃게임 시스템을 초기화한다. 실제 로드와 세이브는 각 ISaveDataProvider의 구현체에서 실행
    private void InitializePersistentManagers()
    {
        _persistentCurrencyManager.Initialize();
        _persistentTraits.Initialize(_persistentCurrencyManager, _outGameSaveCoordinator);
        _outGameSaveCoordinator.Initialize(_saveManager, _persistentTraits, _persistentCurrencyManager, _altarManager, _totemRunApplier, _traitRunApplier);
    }
    // UI 부분을 초기화 한다
    public bool InitializeUI()
    {
        var buildingSlots = new List<BuildingSlot>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            buildingSlots.AddRange(root.GetComponentsInChildren<BuildingSlot>(true));
        _buildingSlots = buildingSlots.ToArray();
        if (!_uiStartup.Initialize(_runCurrencyManager, _gameFlowController,
                _waveController, _buildController, _buildingCoreProgress, _buildingSlots,
                _cameraController, _consumableItemManager, _spawnManager))
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
        if (_traitRunApplier == null || _traitCatalog == null || _totemRunApplier == null ||
            _totemCatalog == null || _altarManager == null || _unitStatModifierManager == null || _effectManager == null)
        {
            Debug.LogError("[BootStrap] 특성·토템·제단과 효과 카탈로그·매니저 참조를 연결해주세요.", this);
            valid = false;
        }
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

        if (_eventManager == null)
        {
            Debug.LogError("[BootStrap] EventManager를 Inspector에서 연결해주세요.", this);
            valid = false;
        }
        return valid;
    }
}
