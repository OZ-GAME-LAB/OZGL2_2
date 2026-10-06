using System.Collections.Generic;
using Game.Cameras;
using Game.Core;
using Game.UI.InGame;
using OZGL.KDH;
using Units;
using UnityEngine;
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
    [Header("Save")]
    [SerializeField] private InGameSaveCoordinator _inGameSaveCoordinator;
    [SerializeField] private PersistentSaveCoordinator _persistentSaveCoordinator;
    [SerializeField] private SaveManager _saveManager;
    
    [SerializeField] private OutGameTraitController _persistentTraits;
    // [SerializeField] private TeamBuildingUiStartup _uiManager;
    [Header("UI Connections")]
    [SerializeField] private InGameUIStartup _uiStartup;
    public bool IsUIConnected => _uiStartup != null && _uiStartup.IsReady;
    // Current date KDH 2026-09-29
    // 아웃게임 효과(특성·토템·제단)입니다. 연결하지 않아도 게임은 시작됩니다.
    [Header("OutGame Effects (KDH)")]
    [Tooltip("SpawnManager에 연결된 것과 같은 오브젝트여야 스폰 유닛에 반영됩니다.")]
    [SerializeField] private UnitStatModifierManager _unitStatModifierManager;
    [SerializeField] private TraitRunApplier _traitRunApplier;
    [SerializeField] private TraitCatalog _traitCatalog;
    [SerializeField] private TotemRunApplier _totemRunApplier;
    [SerializeField] private TotemEffectCatalog _totemCatalog;
    [SerializeField] private AltarManager _altarManager;
    private IOutGameDataSetter _data;
    //각자 대표매니저 1개 만들고 각각 필요한 참조를 말하면 제공

    void Start()
    {
        if (!ValidateReferences()) return;

        _data = new TestOutGameDataSetter();

        // Current date KDH 2026-09-29
        // Pending을 비우기 전에 아웃게임에서 넘어왔는지 기억합니다.
        // 씬을 직접 실행하면 목록이 빈 context가 만들어지므로, 이때는 각 Applier의 _testLevels를 씁니다.
        bool fromOutGame = OutGameStartContext.Pending != null;

        OutGameStartContext context = OutGameStartContext.Pending;
        if (context == null)
        {
            // 인게임 씬을 직접 실행하면 풍요의 제단만 선택합니다.
            // 특성·토템 목록은 비어 있고 누적 보너스는 0입니다.
            context = new OutGameStartContext();
            context.SelectedAltar = AltarId.Abundance;
        }

        if (!InitializePersistentData()) return;

        _testScript.Initialize(_gameFlowController, _waveController);
        _gameFlowController.Initialize
            (_waveController, _testScript, _artifactManager, _archiveManager, _runSettlementManager, _cameraController, _shopManager);
        _waveController.Initialize(_gameFlowController, _spawnManager, _runtimeUnitManager);
        _buildController.Initialize(_runCurrencyManager, _gameFlowController, _buildingCoreProgress, _buildingCensus);
        _artifactManager.Initialize(_waveController, _effectManager, _uiStartup.ArtifactSelectionUI);
        _uiStartup.InitializeArtifactInventory(_artifactManager);
        _runCurrencyManager.Initialize(_waveController,_gameFlowController, _effectManager, _buildingCoreProgress);
        _consumableItemManager.Initialize(_effectManager, _gameFlowController, _runtimeUnitManager,
            Units.Skills.SkillEffectResolver.Instance);
        if (!_consumableItemManager.IsInitialized)
        {
            Debug.LogError("[BootStrap] 상점에 연결할 소모품 매니저 초기화에 실패했습니다.", this);
            return;
        }
        _shopManager.Initialize(_artifactManager, _runCurrencyManager, _gameFlowController, _consumableItemManager, _uiStartup.ShopUI);
        if (!_shopManager.IsInitialized)
        {
            Debug.LogError("[BootStrap] 상점 매니저 초기화에 실패했습니다.", this);
            return;
        }
        _runSettlementManager.Initialize(_waveController, _effectManager, _persistentCurrencyManager, _totemRunApplier,
            _uiStartup.SettlementUI, _persistentSaveCoordinator);
        _archiveManager.Initialize(_artifactManager,_runtimeUnitManager,_waveController);
        _cameraController.Initialize(_buildController,_gameFlowController);
        
        var buildingSlots = new List<BuildingSlot>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            buildingSlots.AddRange(root.GetComponentsInChildren<BuildingSlot>(true));
        if (!_uiStartup.Initialize(_runCurrencyManager, _gameFlowController,
                _waveController, _buildController, _buildingCoreProgress, buildingSlots.ToArray(), _cameraController))
        {
            Debug.LogError("[BootStrap] UI 초기화에 실패했습니다.", this);
            return;
        }
        _uiStartup.UIManager.gameObject.SetActive(true);
        
        // Current date KDH 2026-09-29
        // GameFlow·RunCurrency 초기화 뒤, BeginRun 전에 연결해야 첫 Preparation 이벤트를 받습니다.
        InitializeOutGameEffects(context, fromOutGame);

        _data.SetOutGameData(context);
        OutGameStartContext.Pending = null;
        
        switch (context.StartMode)
        {
            case StartMode.NewGame:
                _data.SetOutGameData(context);
                _archiveManager.NewGame();
                _gameFlowController.NewGame();
                break;

            case StartMode.Continue:
                if (!_inGameSaveCoordinator.TryLoad(out string error))
                {
                    Debug.LogError(error);
                    return;
                }

                _gameFlowController.Continue();
                break;
        }
    }

    // 저장용 특성은 런 효과용 선택 목록과 별개다. 파일에서 복원한 특성을 그대로 보존한다.
    private bool InitializePersistentData()
    {
        _persistentCurrencyManager.Initialize();
        _persistentTraits.Initialize(_persistentCurrencyManager, _persistentSaveCoordinator);
        _persistentSaveCoordinator.Initialize(_saveManager, _persistentTraits, _persistentCurrencyManager);
        if (_persistentSaveCoordinator.TryLoadOrCreate(out string error)) 
            return true;
        Debug.LogError("[BootStrap] 영구 데이터 초기화 실패: " + error, this);
        return false;
    }
    public bool InitializeUI(BuildingSlot[] slots)
    {
        return _uiStartup != null && _uiStartup.Initialize(_runCurrencyManager, _gameFlowController,
            _waveController, _buildController, _buildingCoreProgress, slots, _cameraController);
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
            _persistentSaveCoordinator == null || _persistentTraits == null)
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

    // Current date KDH 2026-09-29
    // 각 Initialize가 null 참조를 직접 경고하므로, 여기서는 오브젝트가 있는지만 확인합니다.
    // 효과 적용은 각 시스템이 PhaseChanged 이벤트로 처리하므로 Update에서 확인하지 않습니다.
    private void InitializeOutGameEffects(OutGameStartContext context, bool fromOutGame)
    {
        // 특성: 첫 Preparation에서 스탯·재화 효과를 적용합니다.
        if (_traitRunApplier != null)
        {
            _traitRunApplier.Initialize(_traitCatalog, _effectManager, _unitStatModifierManager,
                _runCurrencyManager, _gameFlowController);
            if (fromOutGame)
            {
                _traitRunApplier.SetLevels(context.Traits);
            }
        }
        // 토템: catalog가 없어도 기본 스탯 효과(TotemBuiltinEffects)는 적용됩니다.
        if (_totemRunApplier != null)
        {
            _totemRunApplier.Initialize(_totemCatalog, _unitStatModifierManager, _gameFlowController);
            if (fromOutGame)
            {
                _totemRunApplier.SetLevels(context.Totems);
            }
        }
        // 제단: 여기서는 선택만 하고, 적용은 AltarManager가 첫 Preparation에서 합니다.
        if (_altarManager != null)
        {
            _altarManager.Initialize(_effectManager, _runCurrencyManager, _unitStatModifierManager,
                _gameFlowController);
            _altarManager.TrySelectById(context.SelectedAltar);
        }
    }
}
