using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Units.UI
{
    /// <summary>
    /// 마우스 주변의 유닛을 탐색하고,
    /// 미리 생성된 UnitHpUI를 가까운 유닛부터 할당한다.
    ///
    /// 일반/보스 UI 풀을 분리한다. 보스는 마우스 감지 또는 항상 표시를 선택한다.
    /// 탐색 버퍼와 보스 UI 풀은 필요한 경우 확장한 뒤 재사용한다.
    ///
    /// HP / Shield 값 갱신은 UnitHpUI가 담당하고,
    /// 이 Manager는 대상 탐색, UI 할당, 위치 갱신만 담당한다.
    /// </summary>
    public class UnitHPUIManager_Hovering : MonoBehaviour
    {
        // ============================================================
        // Constants
        // ============================================================

        private const int DetectionBufferSize = 32;


        // ============================================================
        // References
        // ============================================================

        [Header("References")]
        [SerializeField]
        private Camera _worldCamera;

        [SerializeField]
        private RectTransform _uiRoot;

        [SerializeField]
        private UnitHPUI_Hovering _uiPrefab;


        // ============================================================
        // UI Pool
        // ============================================================

        [Header("UI Pool")]
        [SerializeField]
        [Min(1)]
        private int _poolSize = 10;

        private UnitHPUI_Hovering[] _uiSlots;

        [Header("Boss UI")]
        [SerializeField, Tooltip("보스 전용 UnitHPUI 프리팹. 미지정 시 일반 프리팹을 사용합니다.")]
        private UnitHPUI_Hovering _bossUiPrefab;

        [SerializeField, Tooltip("켜면 등록된 모든 보스를 표시하고, 끄면 일반 유닛과 같은 마우스 감지 조건을 사용합니다.")]
        private bool _alwaysShowBossHp = true;

        [SerializeField]
        private Vector3 _bossWorldOffset = new Vector3(0f, 2f, 0f);

        [SerializeField, Min(1), Tooltip("보스 UI 초기 개수. 동시에 표시할 보스가 더 많으면 자동 확장합니다.")]
        private int _bossPoolSize = 2;

        [SerializeField, Tooltip("항상 표시할 보스 목록. 비어 있으면 씬에서 자동으로 찾습니다.")]
        private RuntimeUnitManager _runtimeUnitManager;

        private readonly List<UnitHPUI_Hovering> _bossUiSlots = new();
        private readonly List<Unit_Gateway> _bossCandidates = new();


        // ============================================================
        // Detection
        // ============================================================

        [Header("Detection")]
        [SerializeField]
        [Min(0.1f)]
        private float _detectionRadius = 2f;

        [SerializeField]
        [Min(0.02f)]
        private float _detectionInterval = 0.1f;

        [SerializeField]
        private LayerMask _unitLayerMask;


        // ============================================================
        // Position
        // ============================================================

        [Header("Position")]
        [SerializeField]
        private Vector3 _worldOffset =
            new Vector3(0f, 1f, 0f);


        // ============================================================
        // Buffers
        // ============================================================

        private Collider2D[] _detectionBuffer =
            new Collider2D[DetectionBufferSize];

        private HpUICandidate[] _candidateBuffer =
            new HpUICandidate[DetectionBufferSize];


        // ============================================================
        // Runtime
        // ============================================================

        private ContactFilter2D _contactFilter;

        private float _nextDetectionTime;


        // ============================================================
        // Candidate
        // ============================================================

        private struct HpUICandidate
        {
            public Unit_Gateway Unit;

            public float SqrDistance;
        }


        private sealed class HpUICandidateComparer :
            IComparer<HpUICandidate>
        {
            public static readonly HpUICandidateComparer Instance =
                new HpUICandidateComparer();

            public int Compare(
                HpUICandidate x,
                HpUICandidate y)
            {
                return x.SqrDistance.CompareTo(
                    y.SqrDistance
                );
            }
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            Initialize();
        }


        private void Update()
        {
            if (Time.unscaledTime <
                _nextDetectionTime)
            {
                UpdateUIPositions();
                return;
            }

            _nextDetectionTime =
                Time.unscaledTime +
                _detectionInterval;

            DetectUnits();
            UpdateUIPositions();
        }

        private void OnEnable()
        {
            _nextDetectionTime = 0f;
        }

        private void OnDestroy()
        {
            HideAll();
            if (_uiSlots != null)
                foreach (var ui in _uiSlots)
                    if (ui != null) Destroy(ui.gameObject);
            foreach (var ui in _bossUiSlots)
                if (ui != null) Destroy(ui.gameObject);
        }


        private void OnDisable()
        {
            HideAll();
        }


        // ============================================================
        // Initialize
        // ============================================================

        private void Initialize()
        {
            if (_worldCamera == null)
            {
                _worldCamera = Camera.main;
            }

            InitializeContactFilter();
            InitializeUIPool();
            EnsureBossPool(Mathf.Max(1, _bossPoolSize));
        }


        private void InitializeContactFilter()
        {
            _contactFilter =
                new ContactFilter2D();

            _contactFilter.SetLayerMask(
                _unitLayerMask
            );

            _contactFilter.useLayerMask = true;
            _contactFilter.useTriggers = true;
        }


        // ============================================================
        // UI Pool
        // ============================================================

        /// <summary>
        /// 게임 시작 시 필요한 수만큼 HP UI를 미리 생성한다.
        ///
        /// 이후 런타임에서는 Instantiate / Destroy하지 않고
        /// 생성된 Slot을 계속 재사용한다.
        /// </summary>
        private void InitializeUIPool()
        {
            if (_uiPrefab == null)
            {
                Debug.LogError(
                    "[UnitHpUIManager] UnitHpUI Prefab이 설정되지 않았습니다.",
                    this
                );

                return;
            }

            if (_uiRoot == null)
            {
                Debug.LogError(
                    "[UnitHpUIManager] UI Root가 설정되지 않았습니다.",
                    this
                );

                return;
            }

            _uiSlots =
                new UnitHPUI_Hovering[_poolSize];

            for (int i = 0;
                 i < _poolSize;
                 i++)
            {
                UnitHPUI_Hovering ui =
                    Instantiate(
                        _uiPrefab,
                        _uiRoot
                    );

                ui.name =
                    $"{_uiPrefab.name}_{i + 1:00}";

                ui.Unbind();

                _uiSlots[i] = ui;
            }
        }


        // ============================================================
        // Detection
        // ============================================================

        private void DetectUnits()
        {
            _bossCandidates.Clear();
            int candidateCount = 0;
            if (_worldCamera == null) _worldCamera = Camera.main;
            if (_worldCamera == null)
            {
                HideAll();
                return;
            }

            // 마우스가 없어도 항상 표시하는 보스의 목록은 갱신한다.
            if (Mouse.current != null)
            {
                InitializeContactFilter();
                Vector2 mouseWorldPosition = _worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                int hitCount;
                while (true)
                {
                    hitCount = Physics2D.OverlapCircle(mouseWorldPosition, _detectionRadius,
                        _contactFilter, _detectionBuffer);
                    if (hitCount < _detectionBuffer.Length) break;
                    Array.Resize(ref _detectionBuffer, _detectionBuffer.Length * 2);
                }
                if (_candidateBuffer.Length < hitCount)
                    Array.Resize(ref _candidateBuffer, _detectionBuffer.Length);
                candidateCount = CollectCandidates(hitCount, mouseWorldPosition);
                SortCandidates(candidateCount);
            }

            BindCandidates(candidateCount);
            if (_alwaysShowBossHp)
            {
                if (_runtimeUnitManager == null)
                    _runtimeUnitManager = FindFirstObjectByType<RuntimeUnitManager>();
                if (_runtimeUnitManager != null)
                {
                    var bosses = _runtimeUnitManager.BossUnits;
                    for (int i = 0; i < bosses.Count; i++)
                    {
                        var boss = bosses[i];
                        if (boss != null && boss.IsTargetable && IsBoss(boss)
                            && !_bossCandidates.Contains(boss))
                            _bossCandidates.Add(boss);
                    }
                }
            }
            BindBossCandidates();
        }

        private static bool IsBoss(Unit_Gateway unit)
            => unit != null && unit.RuntimeStatus != null
                && unit.RuntimeStatus.UnitData != null && unit.RuntimeStatus.UnitData.IsBoss;

        private void EnsureBossPool(int count)
        {
            var prefab = _bossUiPrefab != null ? _bossUiPrefab : _uiPrefab;
            if (prefab == null || _uiRoot == null) return;
            while (_bossUiSlots.Count < count)
            {
                var ui = Instantiate(prefab, _uiRoot);
                ui.name = $"{prefab.name}_Boss_{_bossUiSlots.Count + 1:00}";
                ui.Unbind();
                _bossUiSlots.Add(ui);
            }
        }

        private void BindBossCandidates()
        {
            EnsureBossPool(_bossCandidates.Count);
            for (int i = 0; i < _bossUiSlots.Count; i++)
            {
                var ui = _bossUiSlots[i];
                if (ui == null) continue;
                if (i < _bossCandidates.Count) ui.Bind(_bossCandidates[i]);
                else ui.Unbind();
            }
        }

        // ============================================================
        // Candidate Collection
        // ============================================================

        private int CollectCandidates(
            int hitCount,
            Vector2 mouseWorldPosition)
        {
            int candidateCount = 0;

            for (int i = 0;
                 i < hitCount;
                 i++)
            {
                Collider2D hit =
                    _detectionBuffer[i];

                if (hit == null)
                    continue;

                Unit_Gateway unit =
                    hit.GetComponentInParent<Unit_Gateway>();

                if (unit == null ||
                    !unit.IsTargetable)
                {
                    continue;
                }

                if (ContainsCandidate(
                    unit,
                    candidateCount))
                {
                    continue;
                }

                Vector2 unitPosition =
                    unit.Transform.position;

                float sqrDistance =
                    (unitPosition -
                     mouseWorldPosition)
                    .sqrMagnitude;

                _candidateBuffer[candidateCount] =
                    new HpUICandidate
                    {
                        Unit = unit,
                        SqrDistance = sqrDistance
                    };

                candidateCount++;

                if (candidateCount >=
                    _candidateBuffer.Length)
                {
                    break;
                }
            }

            return candidateCount;
        }


        private bool ContainsCandidate(
            Unit_Gateway unit,
            int candidateCount)
        {
            for (int i = 0;
                 i < candidateCount;
                 i++)
            {
                if (_candidateBuffer[i].Unit ==
                    unit)
                {
                    return true;
                }
            }

            return false;
        }


        // ============================================================
        // Candidate Sorting
        // ============================================================

        private void SortCandidates(
            int candidateCount)
        {
            if (candidateCount <= 1)
                return;

            Array.Sort(
                _candidateBuffer,
                0,
                candidateCount,
                HpUICandidateComparer.Instance
            );
        }


        // ============================================================
        // UI Binding
        // ============================================================

        private void BindCandidates(int candidateCount)
        {
            int normalCount = 0;
            for (int i = 0; i < candidateCount; i++)
            {
                var unit = _candidateBuffer[i].Unit;
                if (IsBoss(unit))
                {
                    if (!_alwaysShowBossHp) _bossCandidates.Add(unit);
                    continue;
                }
                if (_uiSlots == null || normalCount >= _uiSlots.Length) continue;
                var ui = _uiSlots[normalCount++];
                if (ui != null) ui.Bind(unit);
            }
            if (_uiSlots != null)
                for (int i = normalCount; i < _uiSlots.Length; i++)
                    if (_uiSlots[i] != null) _uiSlots[i].Unbind();
        }

        // ============================================================
        // UI Position
        // ============================================================

        private void UpdateUIPositions()
        {
            if (_worldCamera == null) return;
            if (_uiSlots != null)
                foreach (var ui in _uiSlots) UpdateUIPosition(ui, _worldOffset);
            foreach (var ui in _bossUiSlots) UpdateUIPosition(ui, _bossWorldOffset);
        }

        private void UpdateUIPosition(UnitHPUI_Hovering ui, Vector3 offset)
        {
            if (ui == null || !ui.IsBound) return;
            var target = ui.Target;
            if (target == null || !target.IsTargetable)
            {
                ui.Unbind();
                return;
            }
            ui.transform.position = _worldCamera.WorldToScreenPoint(target.Transform.position + offset);
        }

        private void HideAll()
        {
            if (_uiSlots != null)
                foreach (var ui in _uiSlots)
                    if (ui != null) ui.Unbind();
            foreach (var ui in _bossUiSlots)
                if (ui != null) ui.Unbind();
        }


#if UNITY_EDITOR

        // ============================================================
        // Gizmos
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            if (_worldCamera == null ||
                Mouse.current == null)
            {
                return;
            }

            Vector2 mouseScreenPosition =
                Mouse.current.position.ReadValue();

            Vector2 mouseWorldPosition =
                _worldCamera.ScreenToWorldPoint(
                    mouseScreenPosition
                );

            Gizmos.DrawWireSphere(
                mouseWorldPosition,
                _detectionRadius
            );
        }

#endif
    }
}