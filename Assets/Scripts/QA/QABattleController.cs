using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Units;
using Units.Effects;

namespace Game.QA
{
    [DefaultExecutionOrder(100)]
    public sealed class QABattleController : MonoBehaviour
    {
        [SerializeField] private RuntimeUnitManager _runtime;
        [SerializeField] private UnitStatModifierManager _modifiers;
        [SerializeField] private AllyUnitSpawnDatabaseSO _allyDatabase;
        [SerializeField] private EnemyUnitSpawnDatabaseSO _enemyDatabase;
        [SerializeField] private GameObject _groupPrefab;
        [SerializeField] private Transform _sessionRoot;
        [SerializeField] private Camera _battleCamera;
        [SerializeField] private QABattlePanel _panel;
        private readonly List<QAUnitOption> _options = new();
        private readonly List<QAUnitEntry> _entries = new();
        private readonly List<Unit_GroupAI> _groups = new();
        private int _nextId = 1, _generation;
        private bool _finishRequested, _replayWhenReady;
        private float _originalTimeScale;
        private bool _originalRunInBackground;
        public QABattlePhase Phase { get; private set; } = QABattlePhase.Ready;
        public string Result { get; private set; } = "—";
        public string Message { get; private set; } = "아군과 적군을 생성하세요.";
        public int SelectedId { get; private set; }
        public IReadOnlyList<QAUnitEntry> Entries => _entries;
        public IReadOnlyList<QAUnitOption> Options => _options;
        public QABattleStatistics Statistics { get; } = new();
        public Camera BattleCamera => _battleCamera;
        public RuntimeUnitManager Runtime => _runtime;
        public bool CanEdit => Phase == QABattlePhase.Ready;
        public bool CanStart => CanEdit && _runtime.IsPreparationCompleted &&
            _entries.Any(e => e.Team == UnitTeam.Ally) && _entries.Any(e => e.Team == UnitTeam.Enemy);
        public event Action Changed;

        public void Configure(RuntimeUnitManager runtime, UnitStatModifierManager modifiers,
            AllyUnitSpawnDatabaseSO allies, EnemyUnitSpawnDatabaseSO enemies, GameObject group,
            Transform session, Camera camera, QABattlePanel panel)
        { _runtime = runtime; _modifiers = modifiers; _allyDatabase = allies; _enemyDatabase = enemies;
            _groupPrefab = group; _sessionRoot = session; _battleCamera = camera; _panel = panel; }

        private void Awake()
        {
            _originalTimeScale = Time.timeScale;
            _originalRunInBackground = Application.runInBackground;
            // QA observation and editor-driven tests must keep advancing when focus moves away.
            Application.runInBackground = true;
            var allies = new Dictionary<AllyUnitType, AllySpawnEntry>();
            var enemies = new Dictionary<EnemyUnitType, EnemySpawnEntry>();
            new UnitPrefabMapper(_allyDatabase, _enemyDatabase, allies, enemies).Initialize();
            foreach (AllyUnitType type in Enum.GetValues(typeof(AllyUnitType)))
                _options.Add(Option(UnitTeam.Ally, type.ToString(), allies.TryGetValue(type, out var a) ? a.Prefab : null));
            foreach (EnemyUnitType type in Enum.GetValues(typeof(EnemyUnitType)))
                _options.Add(Option(UnitTeam.Enemy, type.ToString(), enemies.TryGetValue(type, out var e) ? e.Prefab : null));
            _runtime.TeamWiped += OnTeamWiped;
        }
        private static QAUnitOption Option(UnitTeam team, string label, GameObject prefab)
        {
            string reason = prefab == null ? "DB에 등록된 프리팹 없음" : null;
            if (prefab != null && (prefab.GetComponent<Unit_Core>() == null || prefab.GetComponent<Unit_Gateway>() == null ||
                prefab.GetComponent<Unit_RuntimeStatus>()?.UnitData == null || prefab.GetComponent<Unit_Life>() == null)) reason = "유닛 필수 컴포넌트/데이터 없음";
            return new QAUnitOption { Team = team, Label = label, Prefab = prefab, UnavailableReason = reason };
        }
        private void Update()
        {
            if (Phase == QABattlePhase.Fighting) Statistics.Tick(Time.deltaTime);
            else if (Phase == QABattlePhase.Ready) Statistics.Refresh();
        }
        private void LateUpdate()
        {
            // Die() publishes TeamWiped before definitive killer/skill notifications finish.
            // Freeze after that event stack, retaining the last kill and interruption results.
            if (_finishRequested)
            {
                _finishRequested = false;
                int a = _entries.Count(e => e.Unit != null && e.Unit.IsAlive && e.Team == UnitTeam.Ally);
                int b = _entries.Count(e => e.Unit != null && e.Unit.IsAlive && e.Team == UnitTeam.Enemy);
                Finish(a == 0 && b == 0 ? "무승부" : a == 0 ? "적군 승리" : "아군 승리");
            }
        }
        private void OnTeamWiped(UnitTeam team) { if (Phase == QABattlePhase.Fighting) _finishRequested = true; }
        public void Select(int id) { SelectedId = id; Changed?.Invoke(); }

        public bool Spawn(QAUnitOption option, int count)
        {
            if (!CanEdit || option == null || !option.Available) { Message = option?.UnavailableReason ?? "준비 상태에서만 생성할 수 있습니다."; Changed?.Invoke(); return false; }
            count = Mathf.Clamp(count, 1, 5);
            int teamCount = _entries.Count(e => e.Team == option.Team);
            for (int i = 0; i < count; i++)
            {
                int index = teamCount + i;
                _entries.Add(new QAUnitEntry { Id = _nextId++, Team = option.Team, Prefab = option.Prefab,
                    Label = option.Label, Position = new Vector2((option.Team == UnitTeam.Ally ? -1 : 1) * (8 + index / 8 * 1.6f), (index % 8 - 3.5f) * 1.5f) });
            }
            Rebuild(false); return true;
        }
        public void Remove(int id)
        {
            if (!CanEdit) return;
            _entries.RemoveAll(e => e.Id == id); Rebuild(false);
        }
        public bool PlaceSelected(Vector2 point)
        {
            if (!CanEdit) return false;
            var entry = _entries.Find(e => e.Id == SelectedId);
            if (entry == null) return false;
            entry.Position = new Vector2(Mathf.Clamp(point.x, -17, 17), Mathf.Clamp(point.y, -10, 10));
            Rebuild(false); return true;
        }
        public void StartBattle()
        {
            if (!CanStart) { Message = "양쪽 병력과 집결 준비가 완료되어야 시작할 수 있습니다."; Changed?.Invoke(); return; }
            Statistics.Begin(); Result = "진행 중"; Phase = QABattlePhase.Fighting;
            _runtime.StartBattlePhase(); Message = "전투 중 · 병력 구성과 배치 잠김"; Changed?.Invoke();
        }
        public void TogglePause()
        {
            if (Phase == QABattlePhase.Fighting)
            { _runtime.PauseBattle(); Time.timeScale = 0; Phase = QABattlePhase.Paused; Message = "일시정지 · 유닛/투사체/지속 효과/시간 정지"; }
            else if (Phase == QABattlePhase.Paused)
            { Time.timeScale = _originalTimeScale > 0 ? _originalTimeScale : 1; _runtime.ResumeBattle(); Phase = QABattlePhase.Fighting; Message = "전투 재개"; }
            Changed?.Invoke();
        }
        public void StopBattle() { if (Phase == QABattlePhase.Fighting || Phase == QABattlePhase.Paused) Finish("중단"); }
        private void Finish(string result)
        {
            Statistics.Freeze(); _runtime.PauseBattle(); Time.timeScale = 0;
            Phase = QABattlePhase.Finished; Result = result; Message = result + " · 결과 고정 / 초기화 시 현재 결과 지움"; Changed?.Invoke();
        }
        public void ResetBattle() => Rebuild(false);
        public void Replay() => Rebuild(true);
        public void ClearComposition()
        {
            if (!CanEdit) return;
            _entries.Clear(); _nextId = 1; SelectedId = 0; Rebuild(false);
        }

        private void Rebuild(bool replay)
        {
            _generation++; StopAllCoroutines(); _replayWhenReady = replay; _finishRequested = false;
            Phase = QABattlePhase.Preparing; Result = "—";
            Time.timeScale = _originalTimeScale > 0 ? _originalTimeScale : 1;
            Cleanup(); Message = "병력 생성 및 집결 준비 중"; Changed?.Invoke();
            StartCoroutine(BuildSession(_generation));
        }
        private IEnumerator BuildSession(int generation)
        {
            // Old objects are deactivated synchronously; wait for deferred Destroy before rebuilding.
            yield return null;
            foreach (var chunk in _entries.GroupBy(e => new { e.Team, e.Prefab }).SelectMany(g => g.Select((entry, i) => new { entry, i }).GroupBy(x => x.i / 5).Select(c => c.Select(x => x.entry).ToList())))
            {
                var group = Instantiate(_groupPrefab, _sessionRoot).GetComponent<Unit_GroupAI>();
                group.name = "QAGroup_" + chunk[0].Team + "_" + chunk[0].Id; group.Initialize(chunk[0].Team); _groups.Add(group);
                foreach (var entry in chunk)
                {
                    var go = Instantiate(entry.Prefab, entry.Position, Quaternion.identity, group.transform);
                    go.name = "QA_" + entry.Id + "_" + entry.Label;
                    var core = go.GetComponent<Unit_Core>(); var status = go.GetComponent<Unit_RuntimeStatus>(); var data = status.UnitData;
                    if (entry.Team == UnitTeam.Ally) core.Initialize(_modifiers.GetAllyFinalModifier(data.GetAllyClass(), data.GetAllyType(), data.GetAllyTier()), _modifiers.GetAllyPassiveSkills(data.GetAllyClass(), data.GetAllyType(), data.GetAllyTier()));
                    else core.Initialize(_modifiers.GetEnemyFinalModifier(data.GetEnemyClass(), data.GetEnemyType(), data.GetEnemyFaction()), _modifiers.GetEnemyPassiveSkills(data.GetEnemyClass(), data.GetEnemyType(), data.GetEnemyFaction()));
                    entry.Unit = go.GetComponent<Unit_Gateway>();
                    if (!group.AddMember(entry.Unit)) throw new InvalidOperationException("QA 유닛 그룹 등록 실패");
                    Statistics.Track(entry);
                }
                if (!_runtime.RegisterGroup(group)) throw new InvalidOperationException("QA 전투 그룹 등록 실패");
                foreach (var entry in chunk) entry.Unit.MoveToRally(entry.Position);
                group.NotifySpawnCompleted();
            }
            _runtime.NotifyAllySpawnCompleted(); _runtime.NotifyEnemySpawnCompleted();
            while (generation == _generation && !_runtime.IsPreparationCompleted) yield return null;
            if (generation != _generation) yield break;
            Phase = QABattlePhase.Ready;
            if (!_entries.Any(e => e.Id == SelectedId)) SelectedId = _entries.FirstOrDefault()?.Id ?? 0;
            Statistics.Refresh(); Message = CanStart ? "준비 완료 · 유닛 선택 후 전장을 클릭해 배치" : "아군과 적군을 생성하세요.";
            Changed?.Invoke();
            if (_replayWhenReady) { _replayWhenReady = false; StartBattle(); }
        }

        private void Cleanup()
        {
            Statistics.Dispose();
            if (RuntimeEffectManager.Instance != null && RuntimeEffectManager.Instance.gameObject.scene == gameObject.scene) RuntimeEffectManager.Instance.ClearAllEffects();
            if (ProjectileManager.Instance != null && ProjectileManager.Instance.gameObject.scene == gameObject.scene)
                foreach (var projectile in ProjectileManager.Instance.GetComponentsInChildren<Projectile_Controller>(true))
                    if (projectile.gameObject.activeSelf) ProjectileManager.Instance.Release(projectile);
            foreach (var entry in _entries) entry.Unit = null;
            // Runtime lists omit corpses. The session root owns every lifetime, including dead units.
            if (_sessionRoot != null) foreach (Transform child in _sessionRoot) child.gameObject.SetActive(false);
            if (_runtime != null) _runtime.ClearRuntime();
            if (_sessionRoot != null) foreach (Transform child in _sessionRoot) Destroy(child.gameObject);
            _groups.Clear();
        }
        private void OnDestroy()
        {
            _generation++; StopAllCoroutines();
            if (_runtime != null) _runtime.TeamWiped -= OnTeamWiped;
            Cleanup(); Time.timeScale = _originalTimeScale;
            Application.runInBackground = _originalRunInBackground;
        }
        // TODO(QA-unconnected): artifact unit effects need EffectManager -> UnitStatModifierManager integration.
        // Altar needs validated effect data; traits need direct apply/clear entry points.
        // Keep these controls disabled rather than changing shared gameplay APIs for this QA scene.
    }
}
