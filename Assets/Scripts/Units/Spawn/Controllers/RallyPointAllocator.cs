using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    internal class RallyGridAllocator
    {
        // ============================================================
        // Fields
        // ============================================================

        private readonly RallyFormation _rallyFormation;

        private readonly List<Vector2> _sectorCenters =
            new List<Vector2>();

        private readonly List<bool> _occupiedSectors =
            new List<bool>();

        private readonly Queue<int> _preparedSectorIndices =
            new Queue<int>();


        private readonly Vector2 _areaMin;

        private readonly Vector2 _areaMax;

        private readonly Vector2 _sectorSize;

        private readonly float _formationSpacing;


        private int _columnCount;

        private int _rowCount;


        // ============================================================
        // Initialize
        // ============================================================

        public RallyGridAllocator(
            Vector2 areaMin,
            Vector2 areaMax,
            Vector2 sectorSize,
            float formationSpacing)
        {
            _areaMin =
                areaMin;

            _areaMax =
                areaMax;

            _sectorSize =
                sectorSize;

            _formationSpacing =
                formationSpacing;


            _rallyFormation =
                new RallyFormation();


            InitializeSectors();
        }


        private void InitializeSectors()
        {
            _sectorCenters.Clear();

            _occupiedSectors.Clear();

            _preparedSectorIndices.Clear();


            if (_sectorSize.x <= 0f ||
                _sectorSize.y <= 0f)
            {
                Debug.LogError(
                    "[RallyGridAllocator] " +
                    "Sector 크기는 0보다 커야 합니다."
                );

                return;
            }


            float width =
                _areaMax.x -
                _areaMin.x;

            float height =
                _areaMax.y -
                _areaMin.y;


            if (width <= 0f ||
                height <= 0f)
            {
                Debug.LogError(
                    "[RallyGridAllocator] " +
                    "Rally 영역이 올바르지 않습니다."
                );

                return;
            }


            _columnCount =
                Mathf.FloorToInt(
                    width /
                    _sectorSize.x
                );

            _rowCount =
                Mathf.FloorToInt(
                    height /
                    _sectorSize.y
                );


            for (int y = 0;
                 y < _rowCount;
                 y++)
            {
                for (int x = 0;
                     x < _columnCount;
                     x++)
                {
                    Vector2 sectorCenter =
                        new Vector2(
                            _areaMin.x +
                            x * _sectorSize.x +
                            _sectorSize.x * 0.5f,

                            _areaMin.y +
                            y * _sectorSize.y +
                            _sectorSize.y * 0.5f
                        );


                    _sectorCenters.Add(
                        sectorCenter
                    );


                    _occupiedSectors.Add(
                        false
                    );
                }
            }


            Debug.Log(
                $"[RallyGridAllocator] " +
                $"Sector 생성 완료. " +
                $"Count: {_sectorCenters.Count} / " +
                $"Column: {_columnCount} / " +
                $"Row: {_rowCount}"
            );
        }


        // ============================================================
        // Allocation Plan
        // ============================================================

        public bool PrepareCenteredAllocation(
            int groupCount)
        {
            _preparedSectorIndices.Clear();


            if (groupCount <= 0)
            {
                Debug.LogError(
                    $"[RallyGridAllocator] " +
                    $"잘못된 Group Count입니다. " +
                    $"Count={groupCount}"
                );

                return false;
            }


            int availableSectorCount =
                GetAvailableSectorCount();


            if (groupCount >
                availableSectorCount)
            {
                Debug.LogError(
                    $"[RallyGridAllocator] " +
                    $"필요한 Rally Sector보다 " +
                    $"사용 가능한 Sector가 부족합니다. " +
                    $"Required={groupCount} / " +
                    $"Available={availableSectorCount}"
                );

                return false;
            }


            if (_columnCount <= 0 ||
                _rowCount <= 0)
            {
                Debug.LogError(
                    "[RallyGridAllocator] " +
                    "Rally Grid가 초기화되지 않았습니다."
                );

                return false;
            }


            // ========================================================
            // Formation Size
            //
            // 적은 오른쪽에서 왼쪽으로 이동한다.
            //
            // 따라서 Y축으로 전열을 구성하고,
            // 그룹 수가 증가할수록 X축 오른쪽 방향으로
            // 후열을 추가한다.
            // ========================================================

            int requiredRows =
                Mathf.Min(
                    _rowCount,
                    Mathf.CeilToInt(
                        Mathf.Sqrt(
                            groupCount
                        )
                    )
                );


            int requiredColumns =
                Mathf.CeilToInt(
                    groupCount /
                    (float)requiredRows
                );


            if (requiredColumns >
                _columnCount)
            {
                requiredColumns =
                    _columnCount;


                requiredRows =
                    Mathf.CeilToInt(
                        groupCount /
                        (float)requiredColumns
                    );
            }


            if (requiredRows >
                _rowCount)
            {
                Debug.LogError(
                    $"[RallyGridAllocator] " +
                    $"Rally 배치 영역이 부족합니다. " +
                    $"GroupCount={groupCount}"
                );

                return false;
            }


            // ========================================================
            // Centered Block
            //
            // 전체 그룹이 사용할 블록 자체를
            // Rally Area 중앙에 배치한다.
            //
            // X축에서 정확히 중앙을 고를 수 없는 경우에는
            // Enemy Spawn 방향인 오른쪽을 우선한다.
            // ========================================================

            int startColumn =
                Mathf.CeilToInt(
                    (
                        _columnCount -
                        requiredColumns
                    ) * 0.5f
                );


            int startRow =
                Mathf.FloorToInt(
                    (
                        _rowCount -
                        requiredRows
                    ) * 0.5f
                );


            List<int> orderedRows =
                BuildCenterOutRowOrder(
                    startRow,
                    requiredRows
                );


            // ========================================================
            // Directional Allocation
            //
            // Enemy 진행 방향 : Right -> Left
            //
            // 왼쪽 Column이 전열이다.
            // 따라서 왼쪽에서 오른쪽 순서로 Sector를 준비한다.
            //
            // 각 Column 내부에서는 중앙 Y를 먼저 사용한 뒤
            // 위/아래 방향으로 퍼진다.
            // ========================================================

            for (int columnOffset = 0;
                 columnOffset < requiredColumns;
                 columnOffset++)
            {
                int column =
                    startColumn +
                    columnOffset;


                for (int rowOrderIndex = 0;
                     rowOrderIndex < orderedRows.Count;
                     rowOrderIndex++)
                {
                    if (_preparedSectorIndices.Count >=
                        groupCount)
                    {
                        break;
                    }


                    int row =
                        orderedRows[
                            rowOrderIndex
                        ];


                    int sectorIndex =
                        GetSectorIndex(
                            column,
                            row
                        );


                    if (sectorIndex < 0)
                        continue;


                    if (_occupiedSectors[
                        sectorIndex])
                    {
                        continue;
                    }


                    _preparedSectorIndices.Enqueue(
                        sectorIndex
                    );
                }
            }


            // 중앙 블록 내부에 이미 점유된 Sector가 있어
            // 필요한 수를 확보하지 못한 경우,
            // 중앙과 가까운 남은 Sector를 추가한다.
            if (_preparedSectorIndices.Count <
                groupCount)
            {
                FillRemainingPreparedSectors(
                    groupCount
                );
            }


            if (_preparedSectorIndices.Count <
                groupCount)
            {
                Debug.LogError(
                    $"[RallyGridAllocator] " +
                    $"Centered Rally Allocation 준비 실패. " +
                    $"Required={groupCount} / " +
                    $"Prepared={_preparedSectorIndices.Count}"
                );


                _preparedSectorIndices.Clear();

                return false;
            }


            Debug.Log(
                $"[RallyGridAllocator] " +
                $"Centered Rally Allocation 준비 완료. " +
                $"GroupCount={groupCount} / " +
                $"Rows={requiredRows} / " +
                $"Columns={requiredColumns}"
            );


            return true;
        }


        private List<int> BuildCenterOutRowOrder(
            int startRow,
            int rowCount)
        {
            List<int> rows =
                new List<int>(
                    rowCount
                );


            float blockCenter =
                startRow +
                (rowCount - 1) * 0.5f;


            for (int i = 0;
                 i < rowCount;
                 i++)
            {
                rows.Add(
                    startRow + i
                );
            }


            rows.Sort(
                (a, b) =>
                {
                    float distanceA =
                        Mathf.Abs(
                            a -
                            blockCenter
                        );

                    float distanceB =
                        Mathf.Abs(
                            b -
                            blockCenter
                        );


                    int distanceCompare =
                        distanceA.CompareTo(
                            distanceB
                        );


                    if (distanceCompare != 0)
                    {
                        return distanceCompare;
                    }


                    // 중앙에서 같은 거리라면
                    // 위쪽 Sector를 먼저 사용한다.
                    return b.CompareTo(
                        a
                    );
                }
            );


            return rows;
        }


        private void FillRemainingPreparedSectors(
            int requiredCount)
        {
            Vector2 areaCenter =
                (
                    _areaMin +
                    _areaMax
                ) * 0.5f;


            List<int> candidates =
                new List<int>();


            for (int i = 0;
                 i < _sectorCenters.Count;
                 i++)
            {
                if (_occupiedSectors[i])
                    continue;

                if (_preparedSectorIndices.Contains(
                    i))
                {
                    continue;
                }


                candidates.Add(
                    i
                );
            }


            candidates.Sort(
                (a, b) =>
                {
                    Vector2 positionA =
                        _sectorCenters[a];

                    Vector2 positionB =
                        _sectorCenters[b];


                    float distanceA =
                        (
                            positionA -
                            areaCenter
                        ).sqrMagnitude;

                    float distanceB =
                        (
                            positionB -
                            areaCenter
                        ).sqrMagnitude;


                    int distanceCompare =
                        distanceA.CompareTo(
                            distanceB
                        );


                    if (distanceCompare != 0)
                    {
                        return distanceCompare;
                    }


                    // 중앙과 같은 거리라면
                    // 적의 진행 방향을 고려해
                    // 왼쪽 Sector를 우선한다.
                    return positionA.x.CompareTo(
                        positionB.x
                    );
                }
            );


            for (int i = 0;
                 i < candidates.Count;
                 i++)
            {
                if (_preparedSectorIndices.Count >=
                    requiredCount)
                {
                    break;
                }


                _preparedSectorIndices.Enqueue(
                    candidates[i]
                );
            }
        }


        // ============================================================
        // Allocation
        // ============================================================

        public IReadOnlyList<Vector2> Allocate(
            int unitCount,
            Vector2 preferredRallyPoint)
        {
            if (!ValidateUnitCount(
                unitCount))
            {
                return null;
            }


            int sectorIndex =
                FindNearestAvailableSector(
                    preferredRallyPoint
                );


            if (sectorIndex < 0)
            {
                Debug.LogError(
                    "[RallyGridAllocator] " +
                    "사용 가능한 Rally Sector가 없습니다."
                );

                return null;
            }


            return AllocateSector(
                sectorIndex,
                unitCount
            );
        }


        public IReadOnlyList<Vector2> Allocate(
            int unitCount)
        {
            if (!ValidateUnitCount(
                unitCount))
            {
                return null;
            }


            int sectorIndex =
                GetPreparedAvailableSector();


            if (sectorIndex < 0)
            {
                Debug.LogError(
                    "[RallyGridAllocator] " +
                    "준비된 Rally Sector가 없습니다."
                );

                return null;
            }


            return AllocateSector(
                sectorIndex,
                unitCount
            );
        }


        private IReadOnlyList<Vector2> AllocateSector(
            int sectorIndex,
            int unitCount)
        {
            IReadOnlyList<Vector2> formation =
                _rallyFormation.GetFormation(
                    unitCount
                );


            if (formation == null ||
                formation.Count != unitCount)
            {
                Debug.LogError(
                    $"[RallyGridAllocator] " +
                    $"Formation을 가져오지 못했습니다. " +
                    $"Count: {unitCount}"
                );

                return null;
            }


            // Rally 좌표가 확정되는 순간 Sector를 점유 처리한다.
            // 실제 유닛의 도착 여부와는 관계없이 다른 그룹에서는 사용할 수 없다.
            _occupiedSectors[
                sectorIndex
            ] = true;


            Vector2 sectorCenter =
                _sectorCenters[
                    sectorIndex
                ];


            List<Vector2> rallyPositions =
                new List<Vector2>(
                    unitCount
                );


            for (int i = 0;
                 i < formation.Count;
                 i++)
            {
                Vector2 offset =
                    formation[i] *
                    _formationSpacing;


                rallyPositions.Add(
                    sectorCenter +
                    offset
                );
            }


            return rallyPositions;
        }


        // ============================================================
        // Prepared Sector
        // ============================================================

        private int GetPreparedAvailableSector()
        {
            while (_preparedSectorIndices.Count > 0)
            {
                int sectorIndex =
                    _preparedSectorIndices.Dequeue();


                if (sectorIndex < 0 ||
                    sectorIndex >=
                    _occupiedSectors.Count)
                {
                    continue;
                }


                if (_occupiedSectors[
                    sectorIndex])
                {
                    continue;
                }


                return sectorIndex;
            }


            return -1;
        }


        // ============================================================
        // Sector Search
        // ============================================================

        private int FindNearestAvailableSector(
            Vector2 preferredRallyPoint)
        {
            int nearestIndex =
                -1;


            float nearestDistanceSqr =
                float.MaxValue;


            for (int i = 0;
                 i < _sectorCenters.Count;
                 i++)
            {
                if (_occupiedSectors[i])
                {
                    continue;
                }


                float distanceSqr =
                    (
                        _sectorCenters[i] -
                        preferredRallyPoint
                    ).sqrMagnitude;


                if (distanceSqr >=
                    nearestDistanceSqr)
                {
                    continue;
                }


                nearestDistanceSqr =
                    distanceSqr;

                nearestIndex =
                    i;
            }


            return nearestIndex;
        }


        private int GetSectorIndex(
            int column,
            int row)
        {
            if (column < 0 ||
                column >= _columnCount)
            {
                return -1;
            }


            if (row < 0 ||
                row >= _rowCount)
            {
                return -1;
            }


            return row *
                _columnCount +
                column;
        }


        private int GetAvailableSectorCount()
        {
            int count =
                0;


            for (int i = 0;
                 i < _occupiedSectors.Count;
                 i++)
            {
                if (!_occupiedSectors[i])
                {
                    count++;
                }
            }


            return count;
        }


        // ============================================================
        // Clear
        // ============================================================

        public void Clear()
        {
            for (int i = 0;
                 i < _occupiedSectors.Count;
                 i++)
            {
                _occupiedSectors[i] =
                    false;
            }


            _preparedSectorIndices.Clear();


            Debug.Log(
                "[RallyGridAllocator] " +
                "Rally Sector 점유 상태 초기화."
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool ValidateUnitCount(
            int unitCount)
        {
            if (unitCount < 1 ||
                unitCount > 5)
            {
                Debug.LogError(
                    $"[RallyGridAllocator] " +
                    $"그룹 인원은 1~5명이어야 합니다. " +
                    $"Count: {unitCount}"
                );

                return false;
            }


            return true;
        }
    }
}