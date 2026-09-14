using System;
using System.Collections.Generic;
using TMPro;
using Units;
using UnityEngine;

namespace Game.UI
{
    /// <summary>명시적으로 등록된 유닛의 생존 수만 표시한다. 스폰·승패 판정은 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeUnitCountHud : MonoBehaviour
    {
        public int RegisteredCount => _entries.Count;
        public int AliveAllies { get; private set; }
        public int AliveEnemies { get; private set; }

        [SerializeField] private TMP_Text _allyText;
        [SerializeField] private TMP_Text _enemyText;

        private readonly Dictionary<RuntimeUnitInfoSource, Entry> _entries = new();
        private readonly List<RuntimeUnitInfoSource> _staleSources = new();
        private bool _isBound;
        private int _renderedAllies = -1;
        private int _renderedEnemies = -1;

        private sealed class Entry
        {
            public string SelectionId;
            public Unit_Core Core;
            public Unit_Life Life;
            public int Allies;
            public int Enemies;
        }

        private void OnEnable()
        {
            _isBound = true;
            Refresh();
            foreach (var source in _entries.Keys) Subscribe(source);
        }

        private void OnDisable()
        {
            foreach (var source in _entries.Keys) Unsubscribe(source);
            _isBound = false;
        }

        private void OnDestroy()
        {
            ClearRegistrations();
        }

        public void Initialize(TMP_Text allyText, TMP_Text enemyText)
        {
            if (allyText == null) throw new ArgumentNullException(nameof(allyText));
            if (enemyText == null) throw new ArgumentNullException(nameof(enemyText));
            _allyText = allyText;
            _enemyText = enemyText;
            _renderedAllies = _renderedEnemies = -1;
            Refresh();
        }

        /// <summary>스폰 주체가 Unit_Core.Initialize 후 호출한다. 같은 수명의 중복 등록은 성공하되 한 번만 집계한다.</summary>
        public bool TryRegister(RuntimeUnitInfoSource source)
        {
            if (source == null || !source.isActiveAndEnabled || string.IsNullOrEmpty(source.SelectionId)) return false;
            var core = source.GetComponent<Unit_Core>();
            var life = source.GetComponent<Unit_Life>();
            if (core == null || !core.isActiveAndEnabled || life == null || !life.isActiveAndEnabled ||
                core.RuntimeStatus == null || core.RuntimeStatus.UnitData == null ||
                !UnitInfoData.IsValidHealth(life.CurrentHp, life.MaxHp) ||
                (core.Team != UnitTeam.Ally && core.Team != UnitTeam.Enemy)) return false;

            if (!_entries.TryGetValue(source, out var entry))
            {
                entry = new Entry();
                _entries.Add(source, entry);
                if (_isBound) Subscribe(source);
            }
            entry.SelectionId = source.SelectionId;
            entry.Core = core;
            entry.Life = life;
            UpdateCount(entry);
            Render();
            return true;
        }

        /// <summary>등록 당시 수명을 함께 전달한다. 이전 스폰의 지연 해제 요청은 새 스폰을 제거하지 않는다.</summary>
        public bool TryUnregister(RuntimeUnitInfoSource source, string selectionId)
        {
            if (source == null || selectionId == null || !_entries.TryGetValue(source, out var entry) ||
                entry.SelectionId != selectionId) return false;
            Remove(source);
            Render();
            return true;
        }

        public void ClearRegistrations()
        {
            foreach (var source in _entries.Keys) Unsubscribe(source);
            _entries.Clear();
            AliveAllies = AliveEnemies = 0;
            Render();
        }

        /// <summary>HUD 재활성화 또는 외부 진영 변경 후 재동기화한다. 폴링용 메서드가 아니다.</summary>
        public void Refresh()
        {
            _staleSources.Clear();
            foreach (var pair in _entries)
            {
                if (!IsCurrent(pair.Key, pair.Value)) _staleSources.Add(pair.Key);
                else UpdateCount(pair.Value);
            }
            foreach (var source in _staleSources) Remove(source);
            _staleSources.Clear();
            Render();
        }

        private void HandleInfoChanged(RuntimeUnitInfoSource source)
        {
            if (!_entries.TryGetValue(source, out var entry)) return;
            if (!IsCurrent(source, entry)) Remove(source);
            else UpdateCount(entry);
            Render();
        }

        private void HandleUnavailable(RuntimeUnitInfoSource source)
        {
            Remove(source);
            Render();
        }

        private static bool IsCurrent(RuntimeUnitInfoSource source, Entry entry) =>
            source != null && source.isActiveAndEnabled && source.SelectionId == entry.SelectionId &&
            entry.Core != null && entry.Life != null;

        private void UpdateCount(Entry entry)
        {
            bool alive = entry.Core.isActiveAndEnabled && entry.Life.isActiveAndEnabled && !entry.Life.IsDead &&
                UnitInfoData.IsValidHealth(entry.Life.CurrentHp, entry.Life.MaxHp) && entry.Life.CurrentHp > 0;
            int allies = alive && entry.Core.Team == UnitTeam.Ally ? 1 : 0;
            int enemies = alive && entry.Core.Team == UnitTeam.Enemy ? 1 : 0;
            AliveAllies += allies - entry.Allies;
            AliveEnemies += enemies - entry.Enemies;
            entry.Allies = allies;
            entry.Enemies = enemies;
        }

        private void Remove(RuntimeUnitInfoSource source)
        {
            if (!_entries.TryGetValue(source, out var entry)) return;
            Unsubscribe(source);
            AliveAllies -= entry.Allies;
            AliveEnemies -= entry.Enemies;
            _entries.Remove(source);
        }

        private void Subscribe(RuntimeUnitInfoSource source)
        {
            source.InfoChanged -= HandleInfoChanged;
            source.Unavailable -= HandleUnavailable;
            source.InfoChanged += HandleInfoChanged;
            source.Unavailable += HandleUnavailable;
        }

        private void Unsubscribe(RuntimeUnitInfoSource source)
        {
            if (source == null) return;
            source.InfoChanged -= HandleInfoChanged;
            source.Unavailable -= HandleUnavailable;
        }

        private void Render()
        {
            if (!isActiveAndEnabled) return;
            if (_allyText != null && _renderedAllies != AliveAllies)
            {
                _allyText.text = "아군 생존 " + AliveAllies;
                _renderedAllies = AliveAllies;
            }
            if (_enemyText != null && _renderedEnemies != AliveEnemies)
            {
                _enemyText.text = "적군 생존 " + AliveEnemies;
                _renderedEnemies = AliveEnemies;
            }
        }
    }
}
