using System.Collections.Generic;
using UnityEngine;

namespace Units.UI
{
    /// <summary>
    /// HitAppear 방식의 Unit HP UI를 관리한다.
    ///
    /// 일반 유닛은 Ally / Enemy Spawn 완료 시점에
    /// 미리 생성된 UI Pool과 1:1로 연결한다.
    ///
    /// 어느 진영의 Spawn이 먼저 완료되는지는 가정하지 않는다.
    ///
    /// 일반 UI가 부족한 경우 Pending으로 등록한 뒤
    /// 프레임마다 제한된 수만큼 UI를 추가 생성한다.
    ///
    /// 보스는 일반 Pool에서 제외하고,
    /// 별도의 Boss UI Pool을 사용한다.
    /// </summary>
    public class UnitHPUIManager_HitAppear : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [Header("References")]
        [SerializeField]
        private Camera _worldCamera;

        [SerializeField]
        private RectTransform _uiRoot;

        [SerializeField]
        private UnitHPUI_HitAppear _uiPrefab;

        [SerializeField]
        private SpawnManager _spawnManager;

        [SerializeField]
        private RuntimeUnitManager _runtimeUnitManager;


        // ============================================================
        // Normal UI Pool
        // ============================================================

        [Header("UI Pool")]
        [SerializeField]
        [Min(0)]
        private int _initialPoolSize =
            50;

        [SerializeField]
        [Min(1)]
        [Tooltip("UI가 부족할 때 한 프레임에 추가 생성할 최대 개수")]
        private int _createPerFrame =
            2;


        // ============================================================
        // Boss UI
        // ============================================================

        [Header("Boss UI")]
        [SerializeField]
        [Tooltip("보스 전용 HP UI. 비어 있으면 일반 UI Prefab을 사용합니다.")]
        private UnitHPUI_HitAppear _bossUiPrefab;

        [SerializeField]
        [Tooltip("보스 HP를 항상 표시합니다.")]
        private bool _alwaysShowBossHp =
            true;

        [SerializeField]
        private Vector3 _bossWorldOffset =
            new Vector3(
                0f,
                2f,
                0f
            );

        [SerializeField]
        [Min(1)]
        private int _bossPoolSize =
            2;


        // ============================================================
        // Visibility
        // ============================================================

        [Header("Visibility")]
        [SerializeField]
        [Tooltip("HP가 최대여도 Shield가 존재하면 UI를 표시합니다.")]
        private bool _showWhenHasShield =
            true;


        // ============================================================
        // Position
        // ============================================================

        [Header("Position")]
        [SerializeField]
        private Vector3 _worldOffset =
            new Vector3(
                0f,
                1f,
                0f
            );


        // ============================================================
        // Runtime Pool
        // ============================================================

        private readonly List<UnitHPUI_HitAppear>
            _uiPool =
                new();

        private readonly List<UnitHPUI_HitAppear>
            _bossUiPool =
                new();


        // ============================================================
        // Bindings
        // ============================================================

        private readonly Dictionary<Unit_Gateway, UnitHPUI_HitAppear>
            _bindings =
                new();

        private readonly Dictionary<Unit_Gateway, UnitHPUI_HitAppear>
            _bossBindings =
                new();


        // ============================================================
        // Pending
        // ============================================================

        private readonly Queue<Unit_Gateway>
            _pendingUnits =
                new();

        private readonly HashSet<Unit_Gateway>
            _pendingSet =
                new();


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            InitializeReferences();

            CreateInitialPool();
            CreateInitialBossPool();
        }


        private void OnEnable()
        {
            SubscribeEvents();
        }


        private void Update()
        {
            ProcessPendingUnits();
        }


        private void LateUpdate()
        {
            UpdateNormalUIPositions();
            UpdateBossUIPositions();
        }


        private void OnDisable()
        {
            UnsubscribeEvents();
        }


        private void OnDestroy()
        {
            ClearBindings();
            ClearPool();
        }


        // ============================================================
        // Initialize
        // ============================================================

        private void InitializeReferences()
        {
            if (_worldCamera == null)
            {
                _worldCamera =
                    Camera.main;
            }

            if (_spawnManager == null)
            {
                _spawnManager =
                    FindFirstObjectByType<SpawnManager>();
            }

            if (_runtimeUnitManager == null)
            {
                _runtimeUnitManager =
                    FindFirstObjectByType<RuntimeUnitManager>();
            }
        }


        private void CreateInitialPool()
        {
            if (_uiPrefab == null ||
                _uiRoot == null)
            {
                Debug.LogError(
                    "[UnitHPUIManager_HitAppear] " +
                    "UI Prefab 또는 UI Root가 없습니다.",
                    this
                );

                return;
            }


            for (int i = 0;
                 i < _initialPoolSize;
                 i++)
            {
                CreateNormalUI();
            }
        }


        private void CreateInitialBossPool()
        {
            if (_uiRoot == null)
                return;


            UnitHPUI_HitAppear prefab =
                GetBossPrefab();


            if (prefab == null)
                return;


            for (int i = 0;
                 i < _bossPoolSize;
                 i++)
            {
                CreateBossUI();
            }
        }


        // ============================================================
        // Subscription
        // ============================================================

        private void SubscribeEvents()
        {
            if (_spawnManager != null)
            {
                _spawnManager.AllySpawnCompleted +=
                    HandleAllySpawnCompleted;

                _spawnManager.EnemySpawnCompleted +=
                    HandleEnemySpawnCompleted;
            }


            if (_runtimeUnitManager != null)
            {
                _runtimeUnitManager.UnitDied +=
                    HandleUnitDied;

                _runtimeUnitManager.BossSpawned +=
                    HandleBossSpawned;

                _runtimeUnitManager.BossRemoved +=
                    HandleBossRemoved;

                _runtimeUnitManager.RuntimeCleared +=
                    HandleRuntimeCleared;
            }
        }


        private void UnsubscribeEvents()
        {
            if (_spawnManager != null)
            {
                _spawnManager.AllySpawnCompleted -=
                    HandleAllySpawnCompleted;

                _spawnManager.EnemySpawnCompleted -=
                    HandleEnemySpawnCompleted;
            }


            if (_runtimeUnitManager != null)
            {
                _runtimeUnitManager.UnitDied -=
                    HandleUnitDied;

                _runtimeUnitManager.BossSpawned -=
                    HandleBossSpawned;

                _runtimeUnitManager.BossRemoved -=
                    HandleBossRemoved;

                _runtimeUnitManager.RuntimeCleared -=
                    HandleRuntimeCleared;
            }
        }


        // ============================================================
        // Spawn Events
        // ============================================================

        private void HandleAllySpawnCompleted()
        {
            if (_runtimeUnitManager == null)
                return;


            RegisterSpawnedUnits(
                _runtimeUnitManager.AllyUnits
            );
        }


        private void HandleEnemySpawnCompleted()
        {
            if (_runtimeUnitManager == null)
                return;


            RegisterSpawnedUnits(
                _runtimeUnitManager.EnemyUnits
            );
        }


        // ============================================================
        // Normal Unit Registration
        // ============================================================

        private void RegisterSpawnedUnits(
            IReadOnlyList<Unit_Gateway> units)
        {
            if (units == null)
                return;


            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                Unit_Gateway unit =
                    units[i];


                if (!CanRegisterNormalUnit(
                    unit))
                {
                    continue;
                }


                UnitHPUI_HitAppear ui =
                    FindFreeNormalUI();


                if (ui != null)
                {
                    BindNormalUI(
                        unit,
                        ui
                    );

                    continue;
                }


                AddPendingUnit(
                    unit
                );
            }
        }


        private bool CanRegisterNormalUnit(
            Unit_Gateway unit)
        {
            if (unit == null ||
                !unit.IsTargetable)
            {
                return false;
            }


            // 보스는 전용 UI Pool에서 관리한다.
            if (IsBoss(
                unit))
            {
                return false;
            }


            if (_bindings.ContainsKey(
                unit))
            {
                return false;
            }


            if (_pendingSet.Contains(
                unit))
            {
                return false;
            }


            return true;
        }


        // ============================================================
        // Boss
        // ============================================================

        private void HandleBossSpawned(
            Unit_Gateway boss)
        {
            RegisterBoss(
                boss
            );
        }


        private void HandleBossRemoved(
            Unit_Gateway boss)
        {
            ReleaseBoss(
                boss
            );
        }


        private void RegisterBoss(
            Unit_Gateway boss)
        {
            if (boss == null ||
                !boss.IsTargetable)
            {
                return;
            }


            if (_bossBindings.ContainsKey(
                boss))
            {
                return;
            }


            UnitHPUI_HitAppear ui =
                FindFreeBossUI();


            // 보스 수가 초기 Pool보다 많아지는 경우에는
            // 보스 UI만 즉시 확장한다.
            if (ui == null)
            {
                ui =
                    CreateBossUI();
            }


            if (ui == null)
                return;


            ui.Bind(
                boss,
                _showWhenHasShield,
                _alwaysShowBossHp
            );


            if (!ui.IsBound)
                return;


            _bossBindings.Add(
                boss,
                ui
            );


            UpdateUIPosition(
                boss,
                ui,
                _bossWorldOffset
            );
        }


        private void ReleaseBoss(
            Unit_Gateway boss)
        {
            if (boss == null)
                return;


            if (!_bossBindings.TryGetValue(
                boss,
                out UnitHPUI_HitAppear ui))
            {
                return;
            }


            _bossBindings.Remove(
                boss
            );


            if (ui != null)
            {
                ui.Unbind();
            }
        }


        private bool IsBoss(
            Unit_Gateway unit)
        {
            if (unit == null)
                return false;


            return
                unit.RuntimeStatus?.UnitData != null &&
                unit.RuntimeStatus.UnitData.IsBoss;
        }


        // ============================================================
        // Pending
        // ============================================================

        private void AddPendingUnit(
            Unit_Gateway unit)
        {
            if (unit == null)
                return;


            if (!_pendingSet.Add(
                unit))
            {
                return;
            }


            _pendingUnits.Enqueue(
                unit
            );
        }


        private void ProcessPendingUnits()
        {
            if (_pendingUnits.Count <= 0)
                return;


            int createdThisFrame =
                0;


            while (_pendingUnits.Count > 0)
            {
                Unit_Gateway unit =
                    _pendingUnits.Peek();


                // 이미 사망했거나 다른 경로에서 처리된 Unit은 제거한다.
                if (unit == null ||
                    !unit.IsTargetable ||
                    IsBoss(unit) ||
                    _bindings.ContainsKey(unit))
                {
                    _pendingUnits.Dequeue();

                    if (unit != null)
                    {
                        _pendingSet.Remove(
                            unit
                        );
                    }

                    continue;
                }


                UnitHPUI_HitAppear ui =
                    FindFreeNormalUI();


                if (ui == null)
                {
                    if (createdThisFrame >=
                        _createPerFrame)
                    {
                        break;
                    }


                    ui =
                        CreateNormalUI();


                    if (ui == null)
                        break;


                    createdThisFrame++;
                }


                _pendingUnits.Dequeue();

                _pendingSet.Remove(
                    unit
                );


                BindNormalUI(
                    unit,
                    ui
                );
            }
        }


        // ============================================================
        // Normal UI Pool
        // ============================================================

        private UnitHPUI_HitAppear CreateNormalUI()
        {
            if (_uiPrefab == null ||
                _uiRoot == null)
            {
                return null;
            }


            UnitHPUI_HitAppear ui =
                Instantiate(
                    _uiPrefab,
                    _uiRoot
                );


            ui.Unbind();

            _uiPool.Add(
                ui
            );


            return ui;
        }


        private UnitHPUI_HitAppear FindFreeNormalUI()
        {
            for (int i = 0;
                 i < _uiPool.Count;
                 i++)
            {
                UnitHPUI_HitAppear ui =
                    _uiPool[i];


                if (ui == null)
                    continue;


                if (!ui.IsBound)
                    return ui;
            }


            return null;
        }


        // ============================================================
        // Boss UI Pool
        // ============================================================

        private UnitHPUI_HitAppear GetBossPrefab()
        {
            if (_bossUiPrefab != null)
                return _bossUiPrefab;


            return _uiPrefab;
        }


        private UnitHPUI_HitAppear CreateBossUI()
        {
            UnitHPUI_HitAppear prefab =
                GetBossPrefab();


            if (prefab == null ||
                _uiRoot == null)
            {
                return null;
            }


            UnitHPUI_HitAppear ui =
                Instantiate(
                    prefab,
                    _uiRoot
                );


            ui.Unbind();

            _bossUiPool.Add(
                ui
            );


            return ui;
        }


        private UnitHPUI_HitAppear FindFreeBossUI()
        {
            for (int i = 0;
                 i < _bossUiPool.Count;
                 i++)
            {
                UnitHPUI_HitAppear ui =
                    _bossUiPool[i];


                if (ui == null)
                    continue;


                if (!ui.IsBound)
                    return ui;
            }


            return null;
        }


        // ============================================================
        // Binding
        // ============================================================

        private void BindNormalUI(
            Unit_Gateway unit,
            UnitHPUI_HitAppear ui)
        {
            if (unit == null ||
                ui == null)
            {
                return;
            }


            ui.Bind(
                unit,
                _showWhenHasShield,
                false
            );


            if (!ui.IsBound)
                return;


            _bindings[unit] =
                ui;


            UpdateUIPosition(
                unit,
                ui,
                _worldOffset
            );
        }


        private void ReleaseNormalUnit(
            Unit_Gateway unit)
        {
            if (unit == null)
                return;


            _pendingSet.Remove(
                unit
            );


            if (!_bindings.TryGetValue(
                unit,
                out UnitHPUI_HitAppear ui))
            {
                return;
            }


            _bindings.Remove(
                unit
            );


            if (ui != null)
            {
                ui.Unbind();
            }
        }


        // ============================================================
        // Unit Events
        // ============================================================

        private void HandleUnitDied(
            Unit_Gateway unit)
        {
            ReleaseNormalUnit(
                unit
            );

            // BossRemoved도 이후 호출되지만,
            // 사망 프레임에 즉시 숨길 수 있도록 처리한다.
            if (_bossBindings.TryGetValue(
                unit,
                out UnitHPUI_HitAppear bossUI))
            {
                if (bossUI != null)
                {
                    bossUI.Unbind();
                }

                _bossBindings.Remove(
                    unit
                );
            }
        }


        // ============================================================
        // Position
        // ============================================================

        private void UpdateNormalUIPositions()
        {
            foreach (var pair in _bindings)
            {
                Unit_Gateway unit =
                    pair.Key;

                UnitHPUI_HitAppear ui =
                    pair.Value;


                UpdateUIPosition(
                    unit,
                    ui,
                    _worldOffset
                );
            }
        }


        private void UpdateBossUIPositions()
        {
            foreach (var pair in _bossBindings)
            {
                Unit_Gateway boss =
                    pair.Key;

                UnitHPUI_HitAppear ui =
                    pair.Value;


                UpdateUIPosition(
                    boss,
                    ui,
                    _bossWorldOffset
                );
            }
        }


        private void UpdateUIPosition(
            Unit_Gateway unit,
            UnitHPUI_HitAppear ui,
            Vector3 worldOffset)
        {
            if (_worldCamera == null ||
                unit == null ||
                ui == null)
            {
                return;
            }


            Vector3 worldPosition =
                unit.Transform.position +
                worldOffset;


            Vector3 screenPosition =
                _worldCamera.WorldToScreenPoint(
                    worldPosition
                );


            ui.transform.position =
                screenPosition;
        }


        // ============================================================
        // Cleanup
        // ============================================================

        private void ClearBindings()
        {
            foreach (var pair in _bindings)
            {
                if (pair.Value != null)
                {
                    pair.Value.Unbind();
                }
            }


            foreach (var pair in _bossBindings)
            {
                if (pair.Value != null)
                {
                    pair.Value.Unbind();
                }
            }


            _bindings.Clear();
            _bossBindings.Clear();

            _pendingUnits.Clear();
            _pendingSet.Clear();
        }


        private void ClearPool()
        {
            for (int i = 0;
                 i < _uiPool.Count;
                 i++)
            {
                if (_uiPool[i] != null)
                {
                    Destroy(
                        _uiPool[i].gameObject
                    );
                }
            }


            for (int i = 0;
                 i < _bossUiPool.Count;
                 i++)
            {
                if (_bossUiPool[i] != null)
                {
                    Destroy(
                        _bossUiPool[i].gameObject
                    );
                }
            }


            _uiPool.Clear();
            _bossUiPool.Clear();
        }

        private void HandleRuntimeCleared()
        {
            ClearBindings();
        }
    }
}