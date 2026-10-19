using System;
using System.Collections.Generic;
using Game.Core;
using OZGL.KDH;
using Units;
using UnityEngine;

namespace Game.Archive
{
    public enum ArchiveCategory
    {
        Artifact = 0,
        Building = 1,
        Unit = 2
    }

    [Serializable]
    public sealed class ArchiveProgressSaveData
    {
        public List<string> DiscoveredKeys = new();
        public List<string> UnreadKeys = new();
    }

    // 런 통계와 별도로 한 번 발견한 콘텐츠와 아직 읽지 않은 항목을 유지합니다.
    public sealed class ArchiveController : MonoBehaviour
    {
        public static readonly SaveKey<ArchiveProgressSaveData> SaveKey =
            new("archive-progress", version: 1);

        public event Action Changed;

        private readonly HashSet<string> _discovered = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unread = new(StringComparer.Ordinal);
        private SaveManager _saveManager;
        private IArtifactReader _artifacts;
        private BuildingSlot[] _slots;
        private GameFlowController _flow;
        private RuntimeUnitManager _units;
        private bool _initialized;
        private bool _canSave;
        private bool _dirty;

        public void Initialize(SaveManager saveManager, IArtifactReader artifacts, BuildingSlot[] slots,
            GameFlowController flow, RuntimeUnitManager units)
        {
            Flush();
            Unsubscribe();
            _saveManager = saveManager;
            _artifacts = artifacts;
            _slots = slots;
            _flow = flow;
            _units = units;
            _dirty = false;
            _canSave = true;
            _discovered.Clear();
            _unread.Clear();
            LoadProgress();

            // 복원된 현재 보유 항목은 기존 발견으로 합칩니다. 저장된 NEW 표시는 유지합니다.
            foreach (ArtifactInstance instance in _artifacts.Instances)
                Discover(ArchiveCategory.Artifact, instance.Data.Id, false);
            foreach (BuildingSlot slot in _slots)
                if (slot.CurrentBuilding != null)
                    Discover(ArchiveCategory.Building, slot.CurrentBuilding.Data.BuildingId, false);

            _artifacts.StackChanged += HandleArtifactChanged;
            foreach (BuildingSlot slot in _slots)
                slot.OccupationChanged += HandleBuildingChanged;
            _flow.PhaseChanged += HandlePhaseChanged;
            _initialized = true;
            Flush();
            Changed?.Invoke();
        }

        public bool IsDiscovered(ArchiveCategory category, string id, bool isAlly = false)
        {
            return (category == ArchiveCategory.Unit && isAlly) ||
                _discovered.Contains(Key(category, id));
        }

        public bool IsNew(ArchiveCategory category, string id)
        {
            return _unread.Contains(Key(category, id));
        }

        public void MarkRead(ArchiveCategory category, string id)
        {
            if (!_unread.Remove(Key(category, id))) return;
            _dirty = true;
            Flush();
            Changed?.Invoke();
        }

        public void Flush()
        {
            if (!_dirty || !_canSave) return;
            var data = new ArchiveProgressSaveData
            {
                DiscoveredKeys = new List<string>(_discovered),
                UnreadKeys = new List<string>(_unread)
            };
            if (_saveManager.TrySave(SaveKey, data, out string error))
                _dirty = false;
            else
                Debug.LogError("[Archive] 도감 저장 실패: " + error, this);
        }

        private void LoadProgress()
        {
            if (!_saveManager.HasSaveFile(SaveKey)) return;
            if (!_saveManager.TryLoad(SaveKey, out ArchiveProgressSaveData data, out string error))
            {
                BlockSave(error);
                return;
            }
            if (!HasValidKeys(data.DiscoveredKeys) || !HasValidKeys(data.UnreadKeys))
            {
                BlockSave("발견/열람 목록의 키가 올바르지 않습니다.");
                return;
            }
            _discovered.UnionWith(data.DiscoveredKeys);
            _unread.UnionWith(data.UnreadKeys);
            if (!_unread.IsSubsetOf(_discovered))
            {
                _discovered.Clear();
                _unread.Clear();
                BlockSave("미열람 항목에 발견 기록이 없습니다.");
            }
        }

        private static bool HasValidKeys(List<string> keys)
        {
            if (keys == null) return false;
            foreach (string key in keys)
                if (string.IsNullOrWhiteSpace(key) || key.Length < 3 || key[1] != ':' ||
                    key[0] < '0' || key[0] > '2' || string.IsNullOrWhiteSpace(key.Substring(2)))
                    return false;
            return true;
        }

        private void BlockSave(string error)
        {
            _canSave = false;
            Debug.LogError("[Archive] 기존 도감 파일을 복원하지 못해 덮어쓰지 않습니다. " + error, this);
        }

        private bool Discover(ArchiveCategory category, string id, bool markNew)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string key = Key(category, id);
            if (!_discovered.Add(key)) return false;
            if (markNew) _unread.Add(key);
            _dirty = true;
            return true;
        }

        private static string Key(ArchiveCategory category, string id) => ((int)category) + ":" + id;

        private void HandleArtifactChanged(ArtifactInstance instance, int previous, int current)
        {
            if (current <= previous) return;
            bool changed = Discover(ArchiveCategory.Artifact, instance.Data.Id, true);
            Flush();
            if (changed) Changed?.Invoke();
        }

        private void HandleBuildingChanged(BuildingSlot slot)
        {
            if (slot.CurrentBuilding == null) return;
            bool changed = Discover(ArchiveCategory.Building, slot.CurrentBuilding.Data.BuildingId, true);
            Flush();
            if (changed) Changed?.Invoke();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Battle) return;
            bool changed = false;
            foreach (Unit_Gateway unit in _units.EnemyUnits)
                changed |= Discover(ArchiveCategory.Unit, unit.RuntimeStatus.UnitData.UnitId, true);
            Flush();
            if (changed) Changed?.Invoke();
        }

        private void Unsubscribe()
        {
            if (!_initialized) return;
            _artifacts.StackChanged -= HandleArtifactChanged;
            foreach (BuildingSlot slot in _slots)
                if (slot != null) slot.OccupationChanged -= HandleBuildingChanged;
            _flow.PhaseChanged -= HandlePhaseChanged;
            _initialized = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void OnDestroy()
        {
            Flush();
            Unsubscribe();
        }
    }
}
