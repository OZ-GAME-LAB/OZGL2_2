using UnityEngine;
using UnityEngine.Serialization;

// 테스트 시작에 필요한 참조와 초기화 순서만 담당합니다.
public class OutGameBootstrap : MonoBehaviour
{
    [SerializeField] private PersistentCurrencyManager _wallet;
    [SerializeField] private OutGameAltarController altarController;
    [SerializeField] private OutGameTraitController traitController;
    [SerializeField] private OutGameTotemController totemController;
    [SerializeField] private OutGameStartController startController;
    [SerializeField] private SaveManager _saveManager;
    [SerializeField] private OutGameSaveCoordinator _persistentSaveCoordinator;
    [FormerlySerializedAs("_uiBinding")]
    [SerializeField] private OutGameUIController _uiController;

    public bool IsInitialized { get; private set; }
    private bool _initializationStarted;

    private void Start()
    {
        Initialize();
    }

    // 판 설정은 초기화하고, 특성과 혈석은 저장된 상태로 복원합니다.
    public bool Initialize()
    {
        if (IsInitialized) return true;
        if (_initializationStarted)
        {
            Debug.LogError("[OutGameBootstrap] 실패한 초기화를 다시 호출할 수 없습니다. 씬을 다시 열어주세요.", this);
            return false;
        }
        IsInitialized = false;
        if (!ValidateReferences()) return false;
        _initializationStarted = true;

        _uiController.Shutdown();
        _wallet.Initialize();
        altarController.Initialize();
        traitController.Initialize(_wallet, _persistentSaveCoordinator);
        totemController.Initialize();

        if (altarController.SelectedAltar == AltarId.None ||
            traitController.Data.Count == 0 || totemController.Data.Count == 0)
        {
            Debug.LogError("[OutGameBootstrap] 제단 카탈로그와 특성·토템 SO 목록을 확인해주세요.", this);
            return false;
        }

        _persistentSaveCoordinator.Initialize(_saveManager, traitController, _wallet, altarController, totemController);
        if (!_persistentSaveCoordinator.TryLoadOrCreate(out string error))
        {
            Debug.LogError("[OutGameBootstrap] 영구 데이터 초기화 실패: " + error, this);
            return false;
        }

        startController.Initialize(altarController, traitController, totemController, _persistentSaveCoordinator);
        _uiController.Initialize(altarController, traitController, totemController, _wallet, startController);
        IsInitialized = true;
        return true;
    }

    private bool ValidateReferences()
    {
        if (_wallet == null || altarController == null || traitController == null ||
            totemController == null || startController == null || _uiController == null ||
            _saveManager == null || _persistentSaveCoordinator == null)
        {
            Debug.LogError("[OutGameBootstrap] Inspector의 지갑, Selector 3개, Controller, UI Controller, SaveManager, OutGameSaveCoordinator를 연결해주세요.", this);
            return false;
        }

        return _uiController.ValidateReferences();
    }
}
