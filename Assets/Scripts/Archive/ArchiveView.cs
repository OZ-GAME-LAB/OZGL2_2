using System;
using System.Collections.Generic;
using Game.UI;
using Game.UI.InGame;
using OZGL.KDH;
using TMPro;
using Units;
using Units.UnitDatas;
using UnityEngine;

namespace Game.Archive
{
    // UIScreen의 비활성 Root에 배치한다. 상세 내용은 별도의 UIScreen 없이 재사용한다.
    public sealed class ArchiveView : MonoBehaviour
    {
        [Header("Existing catalogs")]
        [SerializeField] private ArtifactCatalog _artifactCatalog;
        [SerializeField] private BuildingDatabase _buildingDatabase;
        [SerializeField] private AllyUnitSpawnDatabaseSO _allyUnitDatabase;
        [SerializeField] private EnemyUnitSpawnDatabaseSO _enemyUnitDatabase;

        [Header("Navigation")]
        [SerializeField] private UnityEngine.UI.Button _artifactTabButton;
        [SerializeField] private UnityEngine.UI.Button _buildingTabButton;
        [SerializeField] private UnityEngine.UI.Button _unitTabButton;
        [SerializeField] private UnityEngine.UI.Button _allyButton;
        [SerializeField] private UnityEngine.UI.Button _enemyButton;
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private GameObject _teamControls;
        [SerializeField] private TMP_Text _progressText;

        [Header("List")]
        [SerializeField] private UnityEngine.UI.ScrollRect _listScroll;
        [SerializeField] private Transform _listContent;
        [SerializeField] private UIItemSlot _slotTemplate;
        [SerializeField] private Sprite _placeholderIcon;
        [SerializeField] private Sprite _unknownIcon;

        [Header("Reused detail content")]
        [SerializeField] private GameObject _emptyDetail;
        [SerializeField] private ArtifactPopupView _artifactDetail;
        [SerializeField] private BuildingInfoView _buildingDetail;
        [SerializeField] private UnitPopupView _unitDetail;

        [Header("Relations")]
        [SerializeField] private GameObject _relationsRoot;
        [SerializeField] private Transform _relationsContent;
        [SerializeField] private TMP_Text _relationHeaderTemplate;
        [SerializeField] private UIItemSlot _relationSlotTemplate;

        private readonly List<Entry> _entries = new();
        private readonly Dictionary<string, Entry> _byKey = new(StringComparer.Ordinal);
        private readonly Dictionary<AllyUnitType, Entry> _allyByType = new();
        private readonly List<UIItemSlot> _slots = new();
        private readonly List<GameObject> _relationObjects = new();
        private readonly Stack<NavigationState> _history = new();
        private ArchiveController _controller;
        private NavigationState _state = new() { Category = ArchiveCategory.Artifact, IsAlly = true, Scroll = 1f };
        private bool _refreshing;
        private bool _holdsPause;
        private float _previousTimeScale;
        private bool _revealSelection;

        private readonly struct Entry
        {
            public readonly ArchiveCategory Category;
            public readonly string Id;
            public readonly bool IsAlly;
            public readonly UnityEngine.Object Data;

            public Entry(ArchiveCategory category, string id, UnityEngine.Object data, bool isAlly = false)
            {
                Category = category;
                Id = id;
                Data = data;
                IsAlly = isAlly;
            }
        }

        private struct NavigationState
        {
            public ArchiveCategory Category;
            public bool IsAlly;
            public string SelectedId;
            public float Scroll;
        }

        public void Initialize(ArchiveController controller)
        {
            if (_controller != null) _controller.Changed -= HandleChanged;
            _controller = controller;
            BuildCatalog();
            if (!isActiveAndEnabled) return;
            if (_controller != null) _controller.Changed += HandleChanged;
            Refresh();
        }

        private void OnEnable()
        {
            if (!_holdsPause)
            {
                _previousTimeScale = Time.timeScale;
                _holdsPause = true;
                Time.timeScale = 0f;
            }
            SetListeners(true);
            if (_controller != null)
            {
                _controller.Changed += HandleChanged;
                Refresh();
            }
        }

        private void OnDisable()
        {
            CaptureScroll();
            _history.Clear();
            SetListeners(false);
            RestoreTimeScale();
            if (_controller != null)
            {
                _controller.Changed -= HandleChanged;
                _controller.Flush();
            }
        }

        private void OnDestroy() => RestoreTimeScale();

        private void RestoreTimeScale()
        {
            if (!_holdsPause) return;
            _holdsPause = false;
            Time.timeScale = _previousTimeScale;
        }

        private void SetListeners(bool add)
        {
            SetListener(_artifactTabButton, ShowArtifacts, add);
            SetListener(_buildingTabButton, ShowBuildings, add);
            SetListener(_unitTabButton, ShowUnits, add);
            SetListener(_allyButton, ShowAllies, add);
            SetListener(_enemyButton, ShowEnemies, add);
            SetListener(_backButton, GoBack, add);
        }

        private static void SetListener(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action, bool add)
        {
            if (add) button.onClick.AddListener(action);
            else button.onClick.RemoveListener(action);
        }

        private void ShowArtifacts() => ChangeList(ArchiveCategory.Artifact, _state.IsAlly);
        private void ShowBuildings() => ChangeList(ArchiveCategory.Building, _state.IsAlly);
        private void ShowUnits() => ChangeList(ArchiveCategory.Unit, _state.IsAlly);
        private void ShowAllies() => ChangeList(ArchiveCategory.Unit, true);
        private void ShowEnemies() => ChangeList(ArchiveCategory.Unit, false);

        private void ChangeList(ArchiveCategory category, bool isAlly)
        {
            if (_state.Category == category && (category != ArchiveCategory.Unit || _state.IsAlly == isAlly)) return;
            SaveHistory();
            _state = new NavigationState { Category = category, IsAlly = isAlly, Scroll = 1f };
            Refresh();
        }

        // 목록과 관계 링크 모두 이 검사를 거쳐 미발견 상세에 진입하지 못한다.
        public bool Navigate(ArchiveCategory category, string id, bool isAlly = false)
        {
            if (_controller == null || !_byKey.TryGetValue(Key(category, id, isAlly), out Entry entry) ||
                !IsDiscovered(entry)) return false;
            if (_state.Category == category && _state.SelectedId == id &&
                (category != ArchiveCategory.Unit || _state.IsAlly == isAlly)) return true;
            SaveHistory();
            bool sameList = _state.Category == category &&
                (category != ArchiveCategory.Unit || _state.IsAlly == isAlly);
            _state = new NavigationState
            {
                Category = category, IsAlly = category == ArchiveCategory.Unit ? isAlly : _state.IsAlly,
                SelectedId = id, Scroll = sameList ? _state.Scroll : 1f
            };
            _revealSelection = true;
            Refresh();
            return true;
        }

        private void SaveHistory()
        {
            CaptureScroll();
            _history.Push(_state);
        }

        private void CaptureScroll()
        {
            _state.Scroll = _listScroll.verticalNormalizedPosition;
        }

        private void GoBack()
        {
            if (_history.Count == 0) return;
            _state = _history.Pop();
            Refresh();
        }

        private void HandleChanged()
        {
            if (_refreshing || !isActiveAndEnabled) return;
            CaptureScroll();
            Refresh();
        }

        private void BuildCatalog()
        {
            _entries.Clear();
            _byKey.Clear();
            _allyByType.Clear();
            foreach (ArtifactData artifact in _artifactCatalog.Artifacts)
                if (artifact != null) Register(new Entry(ArchiveCategory.Artifact, artifact.Id, artifact));
            if (_buildingDatabase.Buildings != null)
                foreach (BuildingData building in _buildingDatabase.Buildings)
                    if (building != null) Register(new Entry(ArchiveCategory.Building, building.BuildingId, building));
            if (_allyUnitDatabase.Classes != null)
                foreach (AllyUnitClassData group in _allyUnitDatabase.Classes)
                    if (group != null) RegisterUnits(group.UnitPrefabs, true);
            if (_enemyUnitDatabase.Factions != null)
                foreach (EnemyUnitFactionData group in _enemyUnitDatabase.Factions)
                    if (group != null) RegisterUnits(group.UnitPrefabs, false);
        }

        private void RegisterUnits(IReadOnlyList<GameObject> prefabs, bool isAlly)
        {
            if (prefabs == null) return;
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null || !prefab.TryGetComponent(out Unit_RuntimeStatus status)) continue;
                UnitData unit = status.UnitData;
                if (unit == null || unit.Team != (isAlly ? UnitTeam.Ally : UnitTeam.Enemy)) continue;
                var entry = new Entry(ArchiveCategory.Unit, unit.UnitId, unit, isAlly);
                if (!Register(entry)) continue;
                if (isAlly && !_allyByType.ContainsKey(unit.GetAllyType()))
                    _allyByType.Add(unit.GetAllyType(), entry);
            }
        }

        private bool Register(Entry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)) return false;
            string key = Key(entry.Category, entry.Id, entry.IsAlly);
            if (_byKey.ContainsKey(key)) return false;
            _byKey.Add(key, entry);
            _entries.Add(entry);
            return true;
        }

        private static string Key(ArchiveCategory category, string id, bool isAlly)
            => ((int)category) + ":" + (category == ArchiveCategory.Unit && isAlly ? "ally:" : "") + id;

        private bool IsDiscovered(Entry entry)
            => _controller != null && _controller.IsDiscovered(entry.Category, entry.Id, entry.IsAlly);

        private void Refresh()
        {
            if (_controller == null || _refreshing) return;
            _refreshing = true;
            try
            {
                bool hasSelection = _byKey.TryGetValue(Key(_state.Category, _state.SelectedId, _state.IsAlly),
                    out Entry selected) && IsDiscovered(selected);
                if (hasSelection && !(selected.Category == ArchiveCategory.Unit && selected.IsAlly))
                    _controller.MarkRead(selected.Category, selected.Id);
                SetInteractable(_artifactTabButton, _state.Category != ArchiveCategory.Artifact);
                SetInteractable(_buildingTabButton, _state.Category != ArchiveCategory.Building);
                SetInteractable(_unitTabButton, _state.Category != ArchiveCategory.Unit);
                SetInteractable(_allyButton, !_state.IsAlly);
                SetInteractable(_enemyButton, _state.IsAlly);
                SetInteractable(_backButton, _history.Count > 0);
                _teamControls.SetActive(_state.Category == ArchiveCategory.Unit);

                ClearSlots();
                int total = 0, discovered = 0;
                UIItemSlot selectedSlot = null;
                foreach (Entry entry in _entries)
                {
                    if (entry.Category != _state.Category ||
                        (entry.Category == ArchiveCategory.Unit && entry.IsAlly != _state.IsAlly)) continue;
                    total++;
                    if (IsDiscovered(entry)) discovered++;
                    UIItemSlot slot = Instantiate(_slotTemplate, _listContent);
                    BindSlot(slot, entry, hasSelection && entry.Id == _state.SelectedId);
                    slot.gameObject.SetActive(true);
                    _slots.Add(slot);
                    if (hasSelection && entry.Id == _state.SelectedId) selectedSlot = slot;
                }
                _progressText.text = $"발견 {discovered} / {total}";
                ShowDetail(hasSelection, selected);
                Canvas.ForceUpdateCanvases();
                _listScroll.StopMovement();
                _listScroll.verticalNormalizedPosition = _state.Scroll;
                if (_revealSelection && selectedSlot != null) EnsureVisible((RectTransform)selectedSlot.transform);
                _revealSelection = false;
            }
            finally { _refreshing = false; }
        }

        private static void SetInteractable(UnityEngine.UI.Button button, bool interactable)
        {
            button.interactable = interactable;
        }

        private void EnsureVisible(RectTransform slot)
        {
            RectTransform viewport = _listScroll.viewport;
            float range = _listScroll.content.rect.height - viewport.rect.height;
            if (range <= 0f) return;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, slot);
            float offset = bounds.max.y > viewport.rect.yMax ? bounds.max.y - viewport.rect.yMax :
                bounds.min.y < viewport.rect.yMin ? bounds.min.y - viewport.rect.yMin : 0f;
            _listScroll.verticalNormalizedPosition = Mathf.Clamp01(_listScroll.verticalNormalizedPosition + offset / range);
            CaptureScroll();
        }

        private void ClearSlots()
        {
            foreach (UIItemSlot slot in _slots)
            {
                slot.Unbind();
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            _slots.Clear();
        }

        private void BindSlot(UIItemSlot slot, Entry entry, bool selected = false, string unknownLabel = "미발견")
        {
            bool known = IsDiscovered(entry);
            bool unread = known && !(entry.Category == ArchiveCategory.Unit && entry.IsAlly) &&
                _controller.IsNew(entry.Category, entry.Id);
            slot.Bind(known ? Icon(entry) : UnknownIcon(), known ? DisplayName(entry) : "?",
                known ? (unread ? "NEW" : string.Empty) : unknownLabel, selected, known,
                known ? () => Navigate(entry.Category, entry.Id, entry.IsAlly) : (Action)null);
        }

        private Sprite UnknownIcon() => _unknownIcon != null ? _unknownIcon : _placeholderIcon;

        private static string DisplayName(Entry entry)
        {
            string displayName = entry.Data is ArtifactData artifact ? artifact.DisplayName :
                entry.Data is BuildingData building ? building.DisplayName : ((UnitData)entry.Data).UnitName;
            return string.IsNullOrWhiteSpace(displayName) ? entry.Data.name : displayName;
        }

        private Sprite Icon(Entry entry)
        {
            Sprite icon = entry.Data is ArtifactData artifact ? artifact.Icon :
                entry.Data is BuildingData building ? building.Icon : null;
            if (icon != null) return icon;
            if (entry.Data is UnitData unit && entry.IsAlly)
                foreach (Entry candidate in _entries)
                    if (candidate.Data is BuildingData producer && producer.HasSpawn &&
                        producer.Spawn.unitType == unit.GetAllyType() && producer.Icon != null && IsDiscovered(candidate))
                        return producer.Icon;
            return _placeholderIcon;
        }

        private void ShowDetail(bool hasSelection, Entry selected)
        {
            _emptyDetail.SetActive(!hasSelection);
            SetDetailActive(_artifactDetail, hasSelection && selected.Category == ArchiveCategory.Artifact);
            SetDetailActive(_buildingDetail, hasSelection && selected.Category == ArchiveCategory.Building);
            SetDetailActive(_unitDetail, hasSelection && selected.Category == ArchiveCategory.Unit);
            ClearRelations();
            if (!hasSelection) return;
            if (selected.Data is ArtifactData artifact) _artifactDetail.Bind(artifact);
            else if (selected.Data is BuildingData building)
            {
                string cost = BuildingUiQuote.TryReadUpgrade(building.BuildCost, out int gold, out int gems)
                    ? $"건설 비용: 골드 {gold:N0} · 보석 {gems:N0}" : "건설 비용 설정 오류";
                _buildingDetail.ShowBuildingInfo(new BuildingInfoData("archive:" + building.BuildingId,
                    DisplayName(selected), BuildingUiQuote.Category(building.BuildingType), 1,
                    building.Description, BuildingUiQuote.Production(building), BuildingUiQuote.Effects(building),
                    building.Icon, costSummary: cost, buildingId: building.BuildingId));
                ShowBuildingRelations(building);
            }
            else if (selected.Data is UnitData unit)
            {
                _unitDetail.Bind(unit);
                if (selected.IsAlly) ShowUnitRelations(unit);
            }
            _relationsRoot.SetActive(_relationObjects.Count > 0);
        }

        private static void SetDetailActive(Component view, bool active)
        {
            view.gameObject.SetActive(active);
        }

        private void ClearRelations()
        {
            foreach (GameObject relation in _relationObjects)
            {
                UIItemSlot slot = relation.GetComponent<UIItemSlot>();
                if (slot != null) slot.Unbind();
                relation.SetActive(false);
                Destroy(relation);
            }
            _relationObjects.Clear();
            _relationsRoot.SetActive(false);
        }

        private void ShowBuildingRelations(BuildingData building)
        {
            var seenFamilies = new HashSet<string>(StringComparer.Ordinal);
            int condition = 0;
            foreach (string family in building.RequiredFamilyIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(family) || !seenFamilies.Add(family)) continue;
                AddHeader($"선행 조건 {++condition} · 아래 중 하나 (모든 조건 필요)");
                bool found = false;
                foreach (Entry entry in _entries)
                    if (entry.Data is BuildingData candidate && candidate.BuildingFamilyId == family)
                    {
                        AddRelation(entry);
                        found = true;
                    }
                if (!found) AddUnknownRelation();
            }

            bool hasPrevious = false;
            foreach (Entry entry in _entries)
            {
                if (!(entry.Data is BuildingData candidate) || candidate.Upgrades == null) continue;
                foreach (BuildingUpgradeOption option in candidate.Upgrades)
                {
                    if (option == null || option.nextBuilding == null || option.nextBuilding.BuildingId != building.BuildingId)
                        continue;
                    if (!hasPrevious) { AddHeader("이전 단계"); hasPrevious = true; }
                    AddRelation(entry);
                    break;
                }
            }

            var seenUpgrades = new HashSet<string>(StringComparer.Ordinal);
            bool hasUpgrade = false;
            foreach (BuildingUpgradeOption option in building.Upgrades ?? Array.Empty<BuildingUpgradeOption>())
            {
                if (option == null || option.nextBuilding == null || !seenUpgrades.Add(option.nextBuilding.BuildingId) ||
                    !_byKey.TryGetValue(Key(ArchiveCategory.Building, option.nextBuilding.BuildingId, false), out Entry entry))
                    continue;
                if (!hasUpgrade) { AddHeader("다음 업그레이드"); hasUpgrade = true; }
                AddRelation(entry, "업그레이드 경로 있음 · 미발견");
            }
            if (building.HasSpawn && _allyByType.TryGetValue(building.Spawn.unitType, out Entry produced))
            {
                AddHeader("생산 유닛");
                AddRelation(produced);
            }
        }

        private void ShowUnitRelations(UnitData unit)
        {
            bool hasProducer = false;
            foreach (Entry entry in _entries)
            {
                if (!(entry.Data is BuildingData building) || !building.HasSpawn ||
                    building.Spawn.unitType != unit.GetAllyType()) continue;
                if (!hasProducer) { AddHeader("생산 건물"); hasProducer = true; }
                AddRelation(entry);
            }
        }

        private void AddHeader(string title)
        {
            TMP_Text header = Instantiate(_relationHeaderTemplate, _relationsContent);
            header.text = title;
            header.gameObject.SetActive(true);
            _relationObjects.Add(header.gameObject);
        }

        private void AddRelation(Entry entry, string unknownLabel = "미발견")
        {
            UIItemSlot slot = Instantiate(_relationSlotTemplate, _relationsContent);
            BindSlot(slot, entry, unknownLabel: unknownLabel);
            slot.gameObject.SetActive(true);
            _relationObjects.Add(slot.gameObject);
        }

        private void AddUnknownRelation()
        {
            UIItemSlot slot = Instantiate(_relationSlotTemplate, _relationsContent);
            slot.Bind(UnknownIcon(), "?", "미발견", false, false, null);
            slot.gameObject.SetActive(true);
            _relationObjects.Add(slot.gameObject);
        }
    }
}
