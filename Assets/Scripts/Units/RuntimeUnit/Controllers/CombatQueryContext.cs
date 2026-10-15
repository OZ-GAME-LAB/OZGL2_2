using System.Collections.Generic;
using UnityEngine;



namespace Units
{
    // Manager를 참조하지 않는 전투 조회 자료. 배정 원본은 각 GroupAI에 남긴다.
    public sealed class CombatQueryContext
    {
        // =========================
        // Spatial State
        // =========================

        private const float CellSize = 8f;

        private sealed class Entry
        {
            public Unit_Gateway Unit;
            public Vector2 Position;
            public float Radius;
            public ICombatTarget Target;
            public Vector2? Reservation;
        }

        private readonly Dictionary<Unit_Gateway, Entry> _entries = new();

        private readonly Dictionary<Vector2Int, List<Entry>> _cells = new();

        private readonly Dictionary<Vector2Int, List<Entry>> _reservationCells = new();

        private readonly Stack<List<Entry>> _cellPool = new();

        private readonly Dictionary<ICombatTarget, HashSet<Unit_Gateway>> _attackers = new();

        private readonly Stack<HashSet<Unit_Gateway>> _attackerPool = new();

        private int _spatialFrame = -1;

        private bool _spatialDirty = true;

        private float _maxRadius;


        // =========================
        // Registration
        // =========================

        internal void Register(Unit_Gateway unit)
        {
            if (unit == null || _entries.ContainsKey(unit))
                return;

            _entries.Add(unit, new Entry { Unit = unit });
            _spatialDirty = true;
        }


        internal void Unregister(Unit_Gateway unit)
        {
            if (ReferenceEquals(unit, null) || !_entries.TryGetValue(unit, out Entry entry))
                return;

            RemoveAssignment(entry);
            _entries.Remove(unit);
            _spatialDirty = true;

            // 사망/풀 반환된 대상을 향한 이전 배정도 집계에서 즉시 제거한다.
            if (_attackers.TryGetValue(unit, out HashSet<Unit_Gateway> attackers))
            {
                foreach (Unit_Gateway attacker in attackers)
                {
                    if (_entries.TryGetValue(attacker, out Entry owner))
                    {
                        owner.Target = null;
                        RemoveReservation(owner);
                    }
                }

                _attackers.Remove(unit);
                attackers.Clear();
                _attackerPool.Push(attackers);
            }
        }


        internal void Clear()
        {
            _entries.Clear();
            _cells.Clear();
            _reservationCells.Clear();
            _cellPool.Clear();
            _attackers.Clear();
            _attackerPool.Clear();
            _spatialFrame = -1;
            _spatialDirty = true;
            _maxRadius = 0f;
        }


        // =========================
        // Assignment Updates
        // =========================

        internal void UpdateAssignment(Unit_Gateway unit, UnitAssignmentContext context)
        {
            if (unit == null || !_entries.TryGetValue(unit, out Entry entry))
                return;

            RemoveAssignment(entry);

            if (context == null || !unit.IsTargetable)
                return;

            if (context.HasTarget && context.AssignedTarget.Team != unit.Team)
            {
                entry.Target = context.AssignedTarget;

                if (!_attackers.TryGetValue(entry.Target, out HashSet<Unit_Gateway> attackers))
                {
                    attackers = _attackerPool.Count > 0
                        ? _attackerPool.Pop()
                        : new HashSet<Unit_Gateway>();

                    _attackers.Add(entry.Target, attackers);
                }

                attackers.Add(unit);
            }

            if (context.HasPosition)
            {
                entry.Reservation = context.AssignedPosition;
                AddToCell(_reservationCells, Cell(entry.Reservation.Value), entry);
            }
        }


        private void RemoveAssignment(Entry entry)
        {
            if (entry.Target != null && _attackers.TryGetValue(entry.Target, out var attackers))
            {
                attackers.Remove(entry.Unit);

                if (attackers.Count == 0)
                {
                    _attackers.Remove(entry.Target);
                    _attackerPool.Push(attackers);
                }
            }

            entry.Target = null;
            RemoveReservation(entry);
        }


        private void RemoveReservation(Entry entry)
        {
            if (!entry.Reservation.HasValue)
                return;

            Vector2Int cell = Cell(entry.Reservation.Value);

            if (_reservationCells.TryGetValue(cell, out List<Entry> entries))
            {
                entries.Remove(entry);

                if (entries.Count == 0)
                {
                    _reservationCells.Remove(cell);
                    _cellPool.Push(entries);
                }
            }

            entry.Reservation = null;
        }


        // =========================
        // Spatial Updates
        // =========================

        internal void RefreshSpatial()
        {
            // 실행 순서와 무관하게 첫 조회도 같은 프레임 자료를 사용할 수 있다.
            if (!_spatialDirty && _spatialFrame == Time.frameCount)
                return;

            foreach (List<Entry> entries in _cells.Values)
            {
                entries.Clear();
                _cellPool.Push(entries);
            }

            _cells.Clear();
            _maxRadius = 0f;

            foreach (Entry entry in _entries.Values)
            {
                if (entry.Unit == null || !entry.Unit.IsTargetable)
                    continue;

                entry.Position = entry.Unit.Transform.position;
                entry.Radius = entry.Unit.BodyRadius;
                _maxRadius = Mathf.Max(_maxRadius, entry.Radius);
                AddToCell(_cells, Cell(entry.Position), entry);
            }

            _spatialFrame = Time.frameCount;
            _spatialDirty = false;
        }


        private static Vector2Int Cell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / CellSize),
                Mathf.FloorToInt(position.y / CellSize)
            );
        }


        private void AddToCell(
            Dictionary<Vector2Int, List<Entry>> cells,
            Vector2Int cell,
            Entry entry)
        {
            if (!cells.TryGetValue(cell, out List<Entry> entries))
            {
                entries = _cellPool.Count > 0 ? _cellPool.Pop() : new List<Entry>();
                cells.Add(cell, entries);
            }

            entries.Add(entry);
        }


        // =========================
        // Nearby Queries
        // =========================

        public void CollectNearby(
            Vector2 origin,
            float range,
            float sourceRadius,
            List<Unit_Gateway> results)
        {
            results.Clear();
            RefreshSpatial();

            float radius = Mathf.Max(0f, range)
                + CombatRangeUtility.Correction(sourceRadius, _maxRadius);

            Vector2Int min = Cell(origin - new Vector2(radius, radius));
            Vector2Int max = Cell(origin + new Vector2(radius, radius));

            long cellCount = ((long)max.x - min.x + 1) * ((long)max.y - min.y + 1);

            // 먼 잔여 적을 찾을 때 빈 격자를 끝없이 순회하지 않는다.
            if (cellCount > _cells.Count * 4L)
            {
                foreach (var pair in _cells)
                {
                    if (pair.Key.x >= min.x && pair.Key.x <= max.x
                        && pair.Key.y >= min.y && pair.Key.y <= max.y)
                    {
                        CollectEntries(pair.Value, origin, range, sourceRadius, results);
                    }
                }

                return;
            }

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    if (_cells.TryGetValue(new Vector2Int(x, y), out List<Entry> entries))
                        CollectEntries(entries, origin, range, sourceRadius, results);
                }
            }
        }


        private static void CollectEntries(
            List<Entry> entries,
            Vector2 origin,
            float range,
            float sourceRadius,
            List<Unit_Gateway> results)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry.Unit == null || !entry.Unit.IsTargetable)
                    continue;

                float limit = Mathf.Max(0f, range)
                    + CombatRangeUtility.Correction(sourceRadius, entry.Radius);

                if ((entry.Position - origin).sqrMagnitude <= limit * limit)
                    results.Add(entry.Unit);
            }
        }


        public Unit_Gateway FindNearestEnemy(
            Unit_Gateway requester,
            HashSet<Unit_GroupAI> allowedGroups)
        {
            // 주변 전선이 사라졌을 때만 넓은 검색을 한다. 매 평가의 기본 경로가 아니다.
            RefreshSpatial();

            Unit_Gateway nearest = null;
            float distance = float.PositiveInfinity;

            foreach (Entry entry in _entries.Values)
            {
                Unit_Gateway unit = entry.Unit;

                if (unit == null || !unit.IsTargetable || unit.Team == requester.Team
                    || unit.GroupAI == null || !allowedGroups.Contains(unit.GroupAI))
                {
                    continue;
                }

                float current = CombatRangeUtility.Distance(requester, unit);

                if (current < distance)
                {
                    distance = current;
                    nearest = unit;
                }
            }

            return nearest;
        }


        // =========================
        // Pressure & Reservations
        // =========================

        public int GetPressure(ICombatTarget target, Unit_Gateway requester)
        {
            if (target == null || target.Team == requester.Team
                || !_attackers.TryGetValue(target, out HashSet<Unit_Gateway> attackers))
            {
                return 0;
            }

            return attackers.Count - (attackers.Contains(requester) ? 1 : 0);
        }


        public bool IsReserved(Unit_Gateway requester, Vector2 position)
        {
            RefreshSpatial();

            float radius = requester.BodyRadius + _maxRadius;
            Vector2Int min = Cell(position - new Vector2(radius, radius));
            Vector2Int max = Cell(position + new Vector2(radius, radius));

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    if (!_reservationCells.TryGetValue(new Vector2Int(x, y), out var entries))
                        continue;

                    for (int i = 0; i < entries.Count; i++)
                    {
                        Entry entry = entries[i];
                        Unit_Gateway unit = entry.Unit;

                        if (unit == null || unit == requester || !unit.IsTargetable
                            || unit.Team != requester.Team)
                        {
                            continue;
                        }

                        float spacing = requester.BodyRadius + unit.BodyRadius;

                        if ((position - entry.Reservation.Value).sqrMagnitude < spacing * spacing)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
