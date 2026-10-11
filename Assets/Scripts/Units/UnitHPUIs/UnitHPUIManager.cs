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
    /// Physics2D 탐색 버퍼와 후보 버퍼는 고정 크기로 재사용하며,
    /// HP UI 역시 시작 시 지정된 수만큼 생성한 뒤 계속 재사용한다.
    ///
    /// HP / Shield 값 갱신은 UnitHpUI가 담당하고,
    /// 이 Manager는 대상 탐색, UI 할당, 위치 갱신만 담당한다.
    /// </summary>
    public class UnitHpUIManager : MonoBehaviour
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
        private UnitHPUI _uiPrefab;


        // ============================================================
        // UI Pool
        // ============================================================

        [Header("UI Pool")]
        [SerializeField]
        [Min(1)]
        private int _poolSize = 10;

        private UnitHPUI[] _uiSlots;


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

        private readonly Collider2D[] _detectionBuffer =
            new Collider2D[DetectionBufferSize];

        private readonly HpUICandidate[] _candidateBuffer =
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
            UpdateUIPositions();

            if (Time.unscaledTime <
                _nextDetectionTime)
            {
                return;
            }

            _nextDetectionTime =
                Time.unscaledTime +
                _detectionInterval;

            DetectUnits();
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
                new UnitHPUI[_poolSize];

            for (int i = 0;
                 i < _poolSize;
                 i++)
            {
                UnitHPUI ui =
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
            if (_worldCamera == null ||
                Mouse.current == null ||
                _uiSlots == null)
            {
                return;
            }

            Vector2 mouseScreenPosition =
                Mouse.current.position.ReadValue();

            Vector2 mouseWorldPosition =
                _worldCamera.ScreenToWorldPoint(
                    mouseScreenPosition
                );

            int hitCount =
                Physics2D.OverlapCircle(
                    mouseWorldPosition,
                    _detectionRadius,
                    _contactFilter,
                    _detectionBuffer
                );

            int candidateCount =
                CollectCandidates(
                    hitCount,
                    mouseWorldPosition
                );

            SortCandidates(
                candidateCount
            );

            BindCandidates(
                candidateCount
            );
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

        private void BindCandidates(
            int candidateCount)
        {
            if (_uiSlots == null)
                return;

            int visibleCount =
                Mathf.Min(
                    candidateCount,
                    _uiSlots.Length
                );

            for (int i = 0;
                 i < visibleCount;
                 i++)
            {
                UnitHPUI ui =
                    _uiSlots[i];

                if (ui == null)
                    continue;

                Unit_Gateway unit =
                    _candidateBuffer[i].Unit;

                if (ui.Target != unit)
                {
                    ui.Bind(unit);
                }
            }

            for (int i = visibleCount;
                 i < _uiSlots.Length;
                 i++)
            {
                UnitHPUI ui =
                    _uiSlots[i];

                if (ui == null ||
                    !ui.IsBound)
                {
                    continue;
                }

                ui.Unbind();
            }
        }


        // ============================================================
        // UI Position
        // ============================================================

        private void UpdateUIPositions()
        {
            if (_uiSlots == null ||
                _worldCamera == null)
            {
                return;
            }

            for (int i = 0;
                 i < _uiSlots.Length;
                 i++)
            {
                UnitHPUI ui =
                    _uiSlots[i];

                if (ui == null ||
                    !ui.IsBound)
                {
                    continue;
                }

                Unit_Gateway target =
                    ui.Target;

                if (target == null ||
                    !target.IsTargetable)
                {
                    ui.Unbind();
                    continue;
                }

                UpdateUIPosition(
                    ui,
                    target
                );
            }
        }


        private void UpdateUIPosition(
            UnitHPUI ui,
            Unit_Gateway target)
        {
            Vector3 worldPosition =
                target.Transform.position +
                _worldOffset;

            Vector3 screenPosition =
                _worldCamera.WorldToScreenPoint(
                    worldPosition
                );

            ui.transform.position =
                screenPosition;
        }


        // ============================================================
        // Hide
        // ============================================================

        private void HideAll()
        {
            if (_uiSlots == null)
                return;

            for (int i = 0;
                 i < _uiSlots.Length;
                 i++)
            {
                UnitHPUI ui =
                    _uiSlots[i];

                if (ui == null)
                    continue;

                ui.Unbind();
            }
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