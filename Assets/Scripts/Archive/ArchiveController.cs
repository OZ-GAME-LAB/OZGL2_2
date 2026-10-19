using System;
using System.Collections.Generic;
using Game.Core;
using OZGL.KDH;
using Units;
using UnityEngine;

namespace Game.Archive
{
    public enum ArchiveCategory { Artifact = 0, Building = 1, Unit = 2 }

    [Serializable]
    public sealed class ArchiveProgressSaveData
    {
        // 누락된 목록은 빈 기록과 구분하여 복원 실패로 처리합니다.
        public List<string> DiscoveredKeys;
        public List<string> UnreadKeys;

        public static ArchiveProgressSaveData CreateEmpty() => new ArchiveProgressSaveData
        {
            DiscoveredKeys = new List<string>(), UnreadKeys = new List<string>()
        };

        public ArchiveProgressSaveData Copy() => new ArchiveProgressSaveData
        {
            DiscoveredKeys = new List<string>(DiscoveredKeys),
            UnreadKeys = new List<string>(UnreadKeys)
        };

        public void Validate()
        {
            ValidateKeys(DiscoveredKeys);
            ValidateKeys(UnreadKeys);
            var discovered = new HashSet<string>(DiscoveredKeys, StringComparer.Ordinal);
            foreach (string key in UnreadKeys)
                if (!discovered.Contains(key)) throw new ArgumentException("미열람 항목에 발견 기록이 없습니다.");
        }

        private static void ValidateKeys(List<string> keys)
        {
            if (keys == null) throw new ArgumentException("도감 발견/열람 목록이 없습니다.");
            foreach (string key in keys)
                if (string.IsNullOrWhiteSpace(key) || key.Length < 3 || key[1] != ':' ||
                    key[0] < '0' || key[0] > '2' || string.IsNullOrWhiteSpace(key.Substring(2)))
                    throw new ArgumentException("도감 발견/열람 목록의 키가 올바르지 않습니다.");
        }
    }

    // 영구적으로 한 번 발견한 콘텐츠와 읽지 않은 항목을 유지합니다
    public sealed class ArchiveController : MonoBehaviour, ISaveDataProvider<ArchiveProgressSaveData>
    {
        public event Action Changed;
        public event Action SaveRequested;
        public bool IsDirty { get; private set; }

        private readonly HashSet<string> _discovered = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unread = new(StringComparer.Ordinal);
        private IArtifactReader _artifacts;
        private BuildingSlot[] _slots;
        private GameFlowController _flow;
        private RuntimeUnitManager _units;
        private bool _initialized;

        public void Initialize(IArtifactReader artifacts, BuildingSlot[] slots,
            GameFlowController flow, RuntimeUnitManager units)
        {
            Unsubscribe();
            _artifacts = artifacts;
            _slots = slots;
            _flow = flow;
            _units = units;
            _artifacts.StackChanged += HandleArtifactChanged;
            foreach (BuildingSlot slot in _slots) slot.OccupationChanged += HandleBuildingChanged;
            _flow.PhaseChanged += HandlePhaseChanged;
            _initialized = true;
        }

        public void RegisterOwnedEntries()
        {
            if (!_initialized) return;
            bool changed = false;
            // 복원된 보유 항목은 기존 발견으로 합칩니다. 저장된 NEW 표시는 유지합니다.
            foreach (ArtifactInstance instance in _artifacts.Instances)
                changed |= Discover(ArchiveCategory.Artifact, instance.Data.Id, false);
            foreach (BuildingSlot slot in _slots)
                if (slot.CurrentBuilding != null)
                    changed |= Discover(ArchiveCategory.Building, slot.CurrentBuilding.Data.BuildingId, false);
            NotifyDiscovered(changed);
        }

        public ArchiveProgressSaveData CaptureSaveData() => new ArchiveProgressSaveData
        {
            DiscoveredKeys = new List<string>(_discovered), UnreadKeys = new List<string>(_unread)
        };

        public void RestoreSaveData(ArchiveProgressSaveData data)
        {
            ValidateSaveData(data); // 검증에 실패하면 기존 메모리 상태도 보존합니다.
            _discovered.Clear();
            _unread.Clear();
            _discovered.UnionWith(data.DiscoveredKeys);
            _unread.UnionWith(data.UnreadKeys);
            IsDirty = false;
            Changed?.Invoke();
        }

        public static void ValidateSaveData(ArchiveProgressSaveData data, int version = 1)
        {
            if (version != 1 || data == null)
                throw new ArgumentException("도감 버전 또는 필수 데이터가 없습니다.");
            data.Validate();
        }

        public bool IsDiscovered(ArchiveCategory category, string id, bool isAlly = false)
            => (category == ArchiveCategory.Unit && isAlly) || _discovered.Contains(Key(category, id));
        public bool IsNew(ArchiveCategory category, string id) => _unread.Contains(Key(category, id));

        public void MarkRead(ArchiveCategory category, string id)
        {
            if (!_unread.Remove(Key(category, id))) return;
            IsDirty = true;
            Changed?.Invoke();
        }

        //쌓인 정보들을 한번에 저장
        public void RequestSave()
        {
            if (IsDirty) SaveRequested?.Invoke();
        }

        public void MarkSaved() => IsDirty = false;

        private bool Discover(ArchiveCategory category, string id, bool markNew)
        {
            if (string.IsNullOrWhiteSpace(id) || !_discovered.Add(Key(category, id))) return false;
            if (markNew) _unread.Add(Key(category, id));
            IsDirty = true;
            return true;
        }

        private static string Key(ArchiveCategory category, string id) => ((int)category) + ":" + id;

        private void NotifyDiscovered(bool changed)
        {
            if (!changed) return;
            RequestSave();
            Changed?.Invoke();
        }

        private void HandleArtifactChanged(ArtifactInstance instance, int previous, int current)
        {
            if (current > previous) NotifyDiscovered(Discover(ArchiveCategory.Artifact, instance.Data.Id, true));
        }

        private void HandleBuildingChanged(BuildingSlot slot)
        {
            if (slot.CurrentBuilding != null)
                NotifyDiscovered(Discover(ArchiveCategory.Building, slot.CurrentBuilding.Data.BuildingId, true));
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Battle) return;
            bool changed = false;
            foreach (Unit_Gateway unit in _units.EnemyUnits)
                changed |= Discover(ArchiveCategory.Unit, unit.RuntimeStatus.UnitData.UnitId, true);
            NotifyDiscovered(changed);
        }

        private void Unsubscribe()
        {
            if (!_initialized) return;
            _artifacts.StackChanged -= HandleArtifactChanged;
            foreach (BuildingSlot slot in _slots)
                if (slot != null) slot.OccupationChanged -= HandleBuildingChanged;
            if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
            _initialized = false;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
