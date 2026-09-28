// Current date KDH 2026-09-22
// 씬의 BuildingSlot 점유를 한곳에서 기억합니다.
// Find는 시작 때 한 번만 하고, 이후는 슬롯 이벤트로 카운트만 고칩니다.
// Update에서 슬롯을 훑지 않습니다. 매 프레임 Find/new List는 GC가 납니다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZGL.KDH
{
    public class BuildingCensus : MonoBehaviour
    {
        public event Action Changed;

        private readonly List<BuildingSlot> _slots = new List<BuildingSlot>(16);
        private readonly Dictionary<int, OccupancyKey> _known = new Dictionary<int, OccupancyKey>(16);
        private readonly Dictionary<string, int> _countById = new Dictionary<string, int>(16);
        private readonly Dictionary<string, int> _countByFamily = new Dictionary<string, int>(16);
        private readonly Dictionary<BuildingType, int> _countByType = new Dictionary<BuildingType, int>(8);

        public int OccupiedCount => _known.Count;

        public void Register(BuildingSlot slot)
        {
            if (slot == null)
            {
                Debug.LogWarning("[BuildingCensus] Register에 BuildingSlot이 null입니다.", this);
                return;
            }

            if (!_slots.Contains(slot))
            {
                _slots.Add(slot);
                slot.OccupationChanged += OnOccupationChanged;
            }

            ApplySlot(slot, true);
        }

        public void Initialize()
        {
            UnsubscribeAll();
            _slots.Clear();
            _known.Clear();
            _countById.Clear();
            _countByFamily.Clear();
            _countByType.Clear();

            // 시작 때 한 번만 모읍니다. 미리 배치한 코어는 Start에서 점유되므로,
            // 이 호출이 더 빨라도 이후 OccupationChanged로 맞춰집니다.
            BuildingSlot[] found = FindObjectsByType<BuildingSlot>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                BuildingSlot slot = found[i];
                if (slot == null)
                    continue;

                _slots.Add(slot);
                slot.OccupationChanged += OnOccupationChanged;
                ApplySlot(slot, false);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        public bool Has(string buildingId)
        {
            return Count(buildingId) > 0;
        }

        public int Count(string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
                return 0;

            return _countById.TryGetValue(buildingId, out int count) ? count : 0;
        }

        public bool HasFamily(string familyId)
        {
            return CountFamily(familyId) > 0;
        }

        public int CountFamily(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
                return 0;

            return _countByFamily.TryGetValue(familyId, out int count) ? count : 0;
        }

        public int CountType(BuildingType type)
        {
            return _countByType.TryGetValue(type, out int count) ? count : 0;
        }

        public void CollectOccupied(List<BuildingSlot> results)
        {
            if (results == null)
            {
                Debug.LogWarning("[BuildingCensus] CollectOccupied에 results List가 null입니다.", this);
                return;
            }

            results.Clear();
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                BuildingSlot slot = _slots[i];
                if (slot == null)
                {
                    _slots.RemoveAt(i);
                    continue;
                }

                if (slot.IsOccupied)
                    results.Add(slot);
            }
        }

        private void OnOccupationChanged(BuildingSlot slot)
        {
            ApplySlot(slot, true);
        }

        private void ApplySlot(BuildingSlot slot, bool notify)
        {
            if (slot == null)
                return;

            int instanceId = slot.GetInstanceID();
            RemoveKnown(instanceId);

            if (slot.IsOccupied && slot.CurrentBuilding != null && slot.CurrentBuilding.Data != null)
            {
                OccupancyKey key = OccupancyKey.From(slot.CurrentBuilding.Data);
                _known[instanceId] = key;
                AddCounts(key);
            }

            if (notify)
                Changed?.Invoke();
        }

        private void RemoveKnown(int instanceId)
        {
            if (!_known.TryGetValue(instanceId, out OccupancyKey key))
                return;

            _known.Remove(instanceId);
            RemoveCounts(key);
        }

        private void AddCounts(OccupancyKey key)
        {
            AddCount(_countById, key.id);
            AddCount(_countByFamily, key.family);
            if (_countByType.TryGetValue(key.type, out int typeCount))
                _countByType[key.type] = typeCount + 1;
            else
                _countByType[key.type] = 1;
        }

        private void RemoveCounts(OccupancyKey key)
        {
            RemoveCount(_countById, key.id);
            RemoveCount(_countByFamily, key.family);
            if (!_countByType.TryGetValue(key.type, out int typeCount))
                return;

            if (typeCount <= 1)
                _countByType.Remove(key.type);
            else
                _countByType[key.type] = typeCount - 1;
        }

        private static void AddCount(Dictionary<string, int> map, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (map.TryGetValue(key, out int count))
                map[key] = count + 1;
            else
                map[key] = 1;
        }

        private static void RemoveCount(Dictionary<string, int> map, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !map.TryGetValue(key, out int count))
                return;

            if (count <= 1)
                map.Remove(key);
            else
                map[key] = count - 1;
        }

        private void UnsubscribeAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    _slots[i].OccupationChanged -= OnOccupationChanged;
            }
        }

        private struct OccupancyKey
        {
            public string id;
            public string family;
            public BuildingType type;

            public static OccupancyKey From(BuildingData data)
            {
                OccupancyKey key = new OccupancyKey();
                key.id = data.BuildingId;
                key.family = data.HasFamilyId ? data.BuildingFamilyId : null;
                key.type = data.BuildingType;
                return key;
            }
        }
    }
}
