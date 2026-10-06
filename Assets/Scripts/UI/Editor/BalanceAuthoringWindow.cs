using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    /// <summary>Editor-only numeric authoring. Source assets are never edited by opening this window.</summary>
    [InitializeOnLoad]
    public sealed class BalanceAuthoringWindow : EditorWindow
    {
        public const string Root = "Assets/BalanceWorkspace/Editor";
        public const string SourceScene = "Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity";

        [Serializable] public sealed class Entry
        {
            public string source, working, hash, type, group, structure;
            public bool connected;
        }
        [Serializable] public sealed class Workspace
        {
            public string scene;
            public List<Entry> entries = new List<Entry>();
        }

        [SerializeField] private string _manifestPath;
        [SerializeField] private string _search = "";
        [SerializeField] private int _category;
        [SerializeField] private int _selected;
        [SerializeField] private SceneAsset _sourceScene;
        [SerializeField] private int _unitFilter;
        [SerializeField] private bool _showPaths;
        [SerializeField] private bool _showSceneTools;
        private readonly HashSet<string> _expandedRelated = new HashSet<string>();
        private readonly Dictionary<string, string> _unitLabels = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _unitBuckets = new Dictionary<string, string>();
        private string[] _unitFilters = { "전체" };
        private Workspace _workspace;
        private Vector2 _navigationScroll, _detailScroll;
        private static readonly string[] Categories = { "전체", "유닛", "건물", "웨이브", "재화", "아티팩트", "상점" };
        private static readonly Regex GuidReference = new Regex(@"\bguid: ([0-9a-f]{32})\b", RegexOptions.Compiled);

        static BalanceAuthoringWindow()
        {
            SceneManager.sceneLoaded += HandleTestSceneLoaded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode && IsWorkspacePath(SceneManager.GetActiveScene().path) &&
                    EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
                {
                    EditorApplication.isPlaying = false;
                    Debug.LogError("[Balance] 저장 격리를 위해 테스트 시 Scene Reload를 켜주세요. 프로젝트 설정은 자동 변경하지 않습니다.");
                }
            };
        }

        private static void HandleTestSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!EditorApplication.isPlaying || !IsWorkspacePath(scene.path)) return;
            // sceneLoaded runs before Start, where the current team bootstrap loads saves.
            string directory = Path.GetFullPath("Library/BalanceSaves/" + Path.GetFileName(Path.GetDirectoryName(scene.path)));
            foreach (var manager in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)))
                manager.ConfigureDirectory(directory);
        }

        [MenuItem("Game/Balance/밸런스 편집기")]
        public static void OpenWindow()
        {
            var window = GetWindow<BalanceAuthoringWindow>("게임 밸런스");
            window.minSize = new Vector2(920, 620);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_manifestPath)) _manifestPath = EditorPrefs.GetString(PreferenceKey, "");
            LoadWorkspace();
        }

        private static string PreferenceKey => "DND.BalanceWorkspace." + Application.dataPath;
        private static bool Busy => EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("게임 밸런스 작업 공간", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("씬 설정 없이 유닛·건물·보상·아티팩트 데이터를 조정합니다. 데이터 불러오기 → 수치 수정 → 검증 → 확정값 반영. ID·참조·목록 구조는 보호됩니다.", MessageType.Info);
            EditorGUILayout.HelpBox("건설·업그레이드 비용과 보상부터 정한 뒤 유닛·스킬·유물을 조정하세요. 기본 공격 수치만으로 실제 DPS를 판정하지 않습니다. 플레이 중 수정은 지원하지 않습니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(Busy))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("프로젝트 밸런스 데이터 불러오기")) Run(() =>
                    {
                        if (_workspace != null) SaveWorkingAssets();
                        _manifestPath = CreateWorkspace();
                        EditorPrefs.SetString(PreferenceKey, _manifestPath);
                        LoadWorkspace();
                    });
                    if (GUILayout.Button("기존 작업 공간 선택"))
                    {
                        var path = EditorUtility.OpenFilePanel("manifest.json 선택", Path.GetFullPath(Root), "json");
                        if (!string.IsNullOrEmpty(path)) Run(() =>
                        {
                            _manifestPath = FileUtil.GetProjectRelativePath(path);
                            LoadWorkspace();
                            EditorPrefs.SetString(PreferenceKey, _manifestPath);
                        });
                    }
                    using (new EditorGUI.DisabledScope(_workspace == null))
                    {
                        if (GUILayout.Button("복사본 저장")) Run(SaveWorkingAssets);
                        if (GUILayout.Button("검증")) Run(() =>
                        {
                            var issues = Validate(_workspace);
                            EditorUtility.DisplayDialog("밸런스 검증", issues.Count == 0 ? "구조·수치·참조 검사 통과 (게임 밸런스 적정성은 플레이 테스트 필요)" : string.Join("\n", issues.Take(20)), "확인");
                        });
                    }
                }
                _showSceneTools = EditorGUILayout.Foldout(_showSceneTools, "선택 기능: 별도 플레이 테스트", true);
                if (_showSceneTools) DrawSceneTools();
                if (_workspace == null) return;
                using (new EditorGUI.DisabledScope(_selected < 0 || _selected >= _workspace.entries.Count || !IsVisible(_workspace.entries[_selected])))
                    if (GUILayout.Button("선택 항목 적용 — 실제 게임 데이터에 저장", GUILayout.Height(32)))
                        ConfirmAndPublish(_workspace.entries[_selected]);
                EditorGUILayout.LabelField("프로젝트 데이터 편집 / 수정값은 확인 후 실제 데이터에 반영", EditorStyles.miniLabel);
                _search = EditorGUILayout.TextField("검색", _search);
                _showPaths = EditorGUILayout.Toggle("고급 정보 (파일 경로)", _showPaths);
                if (_category == 0 || Categories[_category] == "유닛")
                    _unitFilter = EditorGUILayout.Popup("유닛 팀 / 티어 / 팩션", _unitFilter, _unitFilters);
                var visible = Enumerable.Range(0, _workspace.entries.Count).Where(i => IsVisible(_workspace.entries[i])).ToList();
                if (!visible.Contains(_selected)) _selected = visible.Count == 0 ? -1 : visible[0];
                EditorGUILayout.LabelField("표시 데이터: " + visible.Count, EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_navigationScroll, GUILayout.Width(300)))
                    {
                        _navigationScroll = scroll.scrollPosition;
                        EditorGUILayout.LabelField("분류", EditorStyles.boldLabel);
                        for (int category = 0; category < Categories.Length; category++)
                        {
                            if (!GUILayout.Toggle(_category == category, Categories[category], "Button") || _category == category) continue;
                            _category = category;
                            _unitFilter = 0;
                            _selected = -1;
                            _detailScroll = Vector2.zero;
                            Repaint();
                        }
                        EditorGUILayout.Space(12);
                        EditorGUILayout.LabelField("조정할 항목", EditorStyles.boldLabel);
                        foreach (int i in visible)
                        {
                            var item = _workspace.entries[i];
                            string label = _unitLabels.TryGetValue(item.source, out string unitLabel) ? unitLabel : Path.GetFileNameWithoutExtension(item.source);
                            var style = new GUIStyle(EditorStyles.miniButton) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
                            if (GUILayout.Toggle(_selected == i, new GUIContent(label, item.source), style) && _selected != i)
                            {
                                _selected = i;
                                _detailScroll = Vector2.zero;
                            }
                        }
                    }
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
                    {
                        _detailScroll = scroll.scrollPosition;
                        if (_selected >= 0 && _selected < _workspace.entries.Count) DrawEntry(_workspace.entries[_selected]);
                        else EditorGUILayout.HelpBox("왼쪽에서 조정할 항목을 선택하세요. 검색 결과가 없다면 검색어나 유닛 필터를 해제하세요.", MessageType.Info);
                        if (_category == 0 || Categories[_category] == "재화") DrawStartingCurrency();
                    }
                }
            }
        }

        private void DrawSceneTools()
        {
            EditorGUILayout.HelpBox("밸런스 데이터 편집에는 씬이 필요하지 않습니다. 복사본으로 플레이까지 확인하려는 경우에만 별도의 테스트 작업 공간을 만드세요. 현재 작업의 수정값을 자동 이전하지 않습니다.", MessageType.Info);
            _sourceScene = (SceneAsset)EditorGUILayout.ObjectField("테스트할 씬 (선택)", _sourceScene, typeof(SceneAsset), false);
            using (new EditorGUI.DisabledScope(_sourceScene == null))
                if (GUILayout.Button("선택한 씬으로 별도 테스트 작업 공간 생성")) Run(() =>
                {
                    if (_workspace != null) SaveWorkingAssets();
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    _manifestPath = CreateWorkspace(AssetDatabase.GetAssetPath(_sourceScene));
                    EditorPrefs.SetString(PreferenceKey, _manifestPath);
                    LoadWorkspace();
                });
            using (new EditorGUI.DisabledScope(_workspace == null || string.IsNullOrEmpty(_workspace.scene)))
                if (GUILayout.Button("작업 공간의 테스트 씬 열기")) Run(() =>
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    SaveWorkingAssets();
                    EditorSceneManager.OpenScene(_workspace.scene);
                });
        }

        private bool IsVisible(Entry entry)
        {
            if (string.IsNullOrEmpty(entry.group) || (_category > 0 && entry.group != Categories[_category])) return false;
            string text = entry.source + " " + (_unitLabels.TryGetValue(entry.source, out string label) ? label : "");
            if (text.IndexOf(_search ?? "", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if ((_category == 0 || Categories[_category] == "유닛") && _unitFilter > 0)
                return _unitBuckets.TryGetValue(entry.source, out string bucket) && bucket == _unitFilters[_unitFilter];
            return true;
        }

        public static string UnitBucket(Units.UnitDatas.UnitData unit)
        {
            return unit.Team.ToString() == "Ally" ? "아군 / " + unit.GetAllyTier() : "적군 / " + unit.GetEnemyFaction();
        }

        private void DrawRelatedData(Entry entry)
        {
            if (entry.type != "UnitData" && entry.type != "BuildingData") return;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("함께 조정할 연결 설정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("펼치면 이 화면에서 연결된 복사본도 편집할 수 있습니다. 공유된 스킬의 수치는 연결된 다른 유닛에도 영향을 줍니다. 원본 반영은 각 항목에서 별도로 확인합니다.", MessageType.Info);
            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(entry.working, true));
            foreach (var related in _workspace.entries.Where(e => e.working != entry.working && dependencies.Contains(e.working) && !string.IsNullOrEmpty(e.group)))
            {
                bool expanded = EditorGUILayout.Foldout(_expandedRelated.Contains(related.working), related.group + " / " + Path.GetFileNameWithoutExtension(related.source), true);
                if (expanded) _expandedRelated.Add(related.working); else _expandedRelated.Remove(related.working);
                if (expanded)
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawEntry(related, false);
            }
        }

        private void DrawEntry(Entry entry, bool includeRelated = true)
        {
            if (string.IsNullOrEmpty(entry.group)) return;
            EditorGUILayout.LabelField(entry.group + " / " + Path.GetFileNameWithoutExtension(entry.source), EditorStyles.boldLabel);
            if (_showPaths) EditorGUILayout.SelectableLabel(entry.source, GUILayout.Height(34));
            EditorGUILayout.HelpBox(GroupHelp(entry.group), MessageType.None);
            if (!string.IsNullOrEmpty(_workspace.scene) && _showSceneTools)
                EditorGUILayout.HelpBox(entry.connected ? "선택한 테스트 씬에서 사용하는 데이터" : "선택한 테스트 씬에 연결되지 않은 데이터 (편집 가능)", MessageType.Info);
            var working = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working);
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.source);
            if (working == null || source == null) { EditorGUILayout.HelpBox("원본 또는 복사본을 찾을 수 없습니다.", MessageType.Error); return; }
            var serialized = new SerializedObject(working);
            var original = new SerializedObject(source);
            if (_unitLabels.TryGetValue(entry.source, out string identity)) EditorGUILayout.LabelField(identity, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("조정 항목                              작업값 / 원본값", EditorStyles.miniLabel);
            foreach (string path in NumericPaths(working))
            {
                var property = serialized.FindProperty(path);
                string label = Describe(serialized, property);
                var old = original.FindProperty(path);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(new GUIContent(label, path), EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(property, GUIContent.none);
                        if (old != null) GUILayout.Label("원본 " + Number(old), GUILayout.Width(110));
                    }
                }
            }
            if (serialized.ApplyModifiedProperties()) Repaint();
            if (GUILayout.Button("이 항목 적용 — 실제 게임 데이터에 저장", GUILayout.Height(28))) ConfirmAndPublish(entry);
            if (includeRelated) DrawRelatedData(entry);
        }

        private void ConfirmAndPublish(Entry entry)
        {
            Run(() =>
            {
                var changes = GetChanges(entry);
                if (changes.Count == 0) { EditorUtility.DisplayDialog("원본 반영", "변경된 수치가 없습니다.", "확인"); return; }
                string description = entry.source + "\n\n" + string.Join("\n", changes) + "\n\n팀 공유 원본을 변경합니다. 팀원과 합의한 값만 반영하세요. 씬·프리팹·ID는 반영하지 않습니다.";
                if (!EditorUtility.DisplayDialog("선택 데이터만 원본 반영", description, "원본 반영", "취소")) return;
                Publish(entry);
                var working = AssetDatabase.LoadAssetAtPath<Object>(entry.working);
                if (working != null) AssetDatabase.SaveAssetIfDirty(working);
                WriteManifest(_manifestPath, _workspace);
                EditorUtility.DisplayDialog("적용 완료", Path.GetFileNameWithoutExtension(entry.source) + "의 수정값을 실제 게임 데이터에 저장했습니다.\n\n해당 원본 데이터를 사용하는 게임을 다시 실행해 확인하세요.\n연결된 스킬 등 다른 항목의 수정값은 각각 적용해야 합니다. 작업용 테스트 씬은 복사본을 사용합니다.", "확인");
                Repaint();
            });
        }

        private static string GroupHelp(string group)
        {
            switch (group)
            {
                case "건물": return "건설 비용과 업그레이드 비용을 먼저 정하세요. 생산 수를 늘리면 한 웨이브의 아군 전체 전투력이 함께 증가합니다. 연결된 유닛 설정은 아래에서 펼칠 수 있습니다.";
                case "재화": return "분기별 획득 골드가 건설·업그레이드를 얼마나 허용하는지 확인하세요. 시작 재화는 작업용 테스트 씬을 연 상태에서 아래에 표시됩니다.";
                case "유닛": return "체력·방어·공격뿐 아니라 스킬 주기, 광역 대상 수, 패시브와 유물까지 함께 테스트하세요. 기본 공격 수치만으로 실제 DPS를 판단하지 않습니다.";
                case "아티팩트": return "등급별 효과와 최대 중첩을 확인하세요. 일반 4~5개와 신화 1개 등 실제 획득 조합의 누적 성장을 다음 분기 난이도와 함께 비교하세요. 이 도구가 성장 목표를 자동 산출하지는 않습니다.";
                case "웨이브": return "병종 가중치는 고정 생성 수가 아닌 상대 등장 비중입니다. 분기에서 얻은 골드·유물로 성장한 아군을 기준으로 실제 전투를 확인하세요.";
                case "상점": return "구매 가격·판매 가격·등장 가중치를 조정합니다. 최소 구매가가 최대 구매가보다 크지 않은지 확인하세요.";
                default: return "작업용 복사본의 수치를 조정합니다. 저장과 실제 데이터 반영은 별개입니다.";
            }
        }

        private void DrawStartingCurrency()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(_workspace.scene) || scene.path != _workspace.scene) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("테스트 씬 시작 재화 (원본 반영 대상 아님)", EditorStyles.boldLabel);
            foreach (var wallet in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RunCurrencyManager>(true)))
            {
                var serialized = new SerializedObject(wallet);
                var values = serialized.FindProperty("_baseStartingCurrencies");
                if (values == null) continue;
                for (int i = 0; i < values.arraySize; i++)
                {
                    var element = values.GetArrayElementAtIndex(i);
                    var amount = element.FindPropertyRelative("_amount");
                    var currency = element.FindPropertyRelative("_currency");
                    if (amount == null) continue;
                    EditorGUILayout.PropertyField(amount, new GUIContent(currency?.objectReferenceValue != null ? currency.objectReferenceValue.name : "재화 " + i));
                    amount.longValue = Math.Max(0, amount.longValue);
                }
                if (serialized.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(scene);
            }
            EditorGUILayout.HelpBox("시작 재화 변경은 테스트 씬에서 Ctrl+S로 저장하세요. 다른 씬에는 적용되지 않습니다.", MessageType.None);
        }

        private void LoadWorkspace()
        {
            _workspace = null;
            if (!string.IsNullOrEmpty(_manifestPath) && IsWorkspacePath(_manifestPath) && File.Exists(_manifestPath))
            {
                var candidate = JsonUtility.FromJson<Workspace>(File.ReadAllText(_manifestPath));
                if (candidate != null && (string.IsNullOrEmpty(candidate.scene) || IsWorkspacePath(candidate.scene)) && candidate.entries != null && candidate.entries.All(e => IsWorkspacePath(e.working) && IsSourcePath(e.source)))
                {
                    _workspace = candidate;
                    _unitLabels.Clear(); _unitBuckets.Clear();
                    foreach (var entry in candidate.entries.Where(e => e.type == "UnitData"))
                    {
                        var unit = AssetDatabase.LoadAssetAtPath<Units.UnitDatas.UnitData>(entry.working);
                        if (unit == null) continue;
                        string bucket = UnitBucket(unit);
                        string type = unit.Team.ToString() == "Ally" ? unit.GetAllyType().ToString() : unit.GetEnemyType().ToString();
                        _unitBuckets[entry.source] = bucket;
                        _unitLabels[entry.source] = bucket + " / " + unit.UnitName + " / " + type;
                    }
                    _unitFilters = new[] { "전체" }.Concat(_unitBuckets.Values.Distinct().OrderBy(v => v)).ToArray();
                    _unitFilter = 0;
                    if (_selected < 0 || _selected >= candidate.entries.Count || string.IsNullOrEmpty(candidate.entries[_selected].group)) _selected = candidate.entries.FindIndex(e => !string.IsNullOrEmpty(e.group));
                }
            }
        }

        private void SaveWorkingAssets()
        {
            foreach (var entry in _workspace.entries)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.working);
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private static void Run(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("밸런스 도구", exception.Message, "확인"); }
        }

        public static bool IsWorkspacePath(string path) => !string.IsNullOrEmpty(path) && path.StartsWith(Root + "/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');
        private static bool IsSourcePath(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Assets/BalanceWorkspace/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');

        public static string CreateWorkspace() => CreateWorkspace(null);

        public static string CreateWorkspace(string sourceScene)
        {
            try { return CreateWorkspaceCore(sourceScene); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static string CreateWorkspaceCore(string sourceScene)
        {
            if (Busy) throw new InvalidOperationException("플레이/컴파일 종료 후 생성하세요.");
            bool withScene = !string.IsNullOrEmpty(sourceScene);
            if (withScene && !IsSourcePath(sourceScene)) throw new InvalidOperationException("Assets 내부의 원본 씬을 선택하세요. 작업용 복사본을 다시 복사할 수 없습니다.");
            if (withScene && AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScene) == null) throw new FileNotFoundException(sourceScene);
            var connected = withScene ? new HashSet<string>(AssetDatabase.GetDependencies(sourceScene, true)) : new HashSet<string>();
            var candidates = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsSourcePath).Where(p => !string.IsNullOrEmpty(Group(AssetDatabase.LoadAssetAtPath<ScriptableObject>(p)?.GetType().Name))).ToArray();
            var roots = withScene ? candidates.Concat(new[] { sourceScene }).ToArray() : candidates;
            var paths = AssetDatabase.GetDependencies(roots, true)
                .Where(IsSourcePath).Where(p => (withScene && (p == sourceScene || p.EndsWith(".prefab", StringComparison.Ordinal))) || IsFirstPartyData(p)).Distinct().OrderBy(p => p).ToArray();
            // Preflight all inputs before writing anything. Only text-serialized copies are rewritten;
            // GUID substitution preserves file IDs, prefab inheritance and nested references exactly.
            foreach (string path in paths)
            {
                if (!File.ReadAllText(path).StartsWith("%YAML", StringComparison.Ordinal)) throw new InvalidOperationException("텍스트 직렬화 에셋만 지원합니다: " + path);
                if (AssetDatabase.LoadAllAssetsAtPath(path).Any(EditorUtility.IsDirty)) throw new InvalidOperationException("먼저 원본의 미저장 변경을 저장/취소하세요: " + path);
            }
            string folder = Root + "/Session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var workspace = new Workspace();
            var guidMap = new Dictionary<string, string>();
            foreach (string source in paths)
            {
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("밸런스 복사본 생성", source, (float)workspace.entries.Count / paths.Length))
                    throw new OperationCanceledException("생성을 취소했습니다. 원본은 변경하지 않았습니다. 미완성 복사본은 " + folder + "에 남아 있습니다.");
                string guid = AssetDatabase.AssetPathToGUID(source);
                string working = folder + "/" + Path.GetFileNameWithoutExtension(source) + "_" + guid.Substring(0, 8) + Path.GetExtension(source);
                if (!AssetDatabase.CopyAsset(source, working)) throw new IOException("복사 실패: " + source);
                guidMap.Add(guid, AssetDatabase.AssetPathToGUID(working));
                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(source);
                workspace.entries.Add(new Entry { source = source, working = working, hash = Hash(source), type = data?.GetType().Name, group = Group(data?.GetType().Name), connected = connected.Contains(source) });
                if (source == sourceScene) workspace.scene = working;
            }
            foreach (var entry in workspace.entries)
            {
                string text = File.ReadAllText(entry.working);
                string remapped = GuidReference.Replace(text, m => guidMap.TryGetValue(m.Groups[1].Value, out string replacement) ? "guid: " + replacement : m.Value);
                if (remapped != text) File.WriteAllText(entry.working, remapped, new UTF8Encoding(false));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var entry in workspace.entries.Where(e => !string.IsNullOrEmpty(e.group)))
                entry.structure = Structure(AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working));
            string manifest = folder + "/manifest.json";
            WriteManifest(manifest, workspace);
            Debug.Log($"[Balance] 작업 공간 생성: {workspace.scene}, 복사본 {workspace.entries.Count}개. 원본 미변경.");
            return manifest;
        }

        private static bool IsFirstPartyData(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) return false;
            var script = MonoScript.FromScriptableObject(asset);
            return script != null && AssetDatabase.GetAssetPath(script).StartsWith("Assets/Scripts/", StringComparison.Ordinal);
        }

        public static string Group(string type)
        {
            switch (type)
            {
                case "UnitData": case "BasicAttackData": case "ActiveSkillData": case "PassiveSkillData": return "유닛";
                case "BuildingData": return "건물";
                case "WaveSO": return "웨이브";
                case "WaveRewardTable": return "재화";
                case "ArtifactData": case "ArtifactRewardTable": return "아티팩트";
                case "ShopTable": case "ShopArtifactTable": case "ShopConsumableTable": return "상점";
                default: return null;
            }
        }

        public static List<string> NumericPaths(Object target)
        {
            var result = new List<string>();
            var iterator = new SerializedObject(target).GetIterator();
            while (iterator.NextVisible(true))
                if ((iterator.propertyType == SerializedPropertyType.Integer || iterator.propertyType == SerializedPropertyType.Float) && Allowed(target.GetType().Name, iterator.propertyPath)) result.Add(iterator.propertyPath);
            return result;
        }

        private static bool Allowed(string type, string path)
        {
            string normalized = Regex.Replace(path, @"\.Array\.data\[\d+\]", "[]");
            switch (type)
            {
                case "UnitData": return normalized == "_stats[]._value";
                case "PassiveSkillData": return normalized == "_tickInterval" || normalized == "_conditionCheckInterval";
                case "BuildingData": return new[] { "buildCost[].amount", "production.amount", "spawn.countPerWave", "spawn.maxAlive", "requiredCoreLevel", "upgrades[].requiredCoreLevel", "upgrades[].cost[].amount" }.Contains(normalized);
                case "WaveSO": return normalized.StartsWith("Weights.", StringComparison.Ordinal);
                case "WaveRewardTable": return normalized == "_quarters[]._waves[]._rewards[]._amount";
                case "ArtifactData": return normalized == "_maxStacks" || new[] { "_unitStatEffects[]._value", "_currencyEffects[]._value", "_consumableSlotEffects[]._additionalSlots" }.Contains(normalized);
                case "ArtifactRewardTable": return new[] { "_candidateCount", "_commonWeight", "_rareWeight", "_legendaryWeight" }.Contains(normalized);
                case "ShopTable": return normalized == "_artifactSlotCount";
                case "ShopConsumableTable": return new[] { "_slotCount", "_minPrice", "_maxPrice" }.Contains(normalized);
                case "ShopArtifactTable": return new[] { "_raritySettings[]._weight", "_raritySettings[]._minPrice", "_raritySettings[]._maxPrice", "_raritySettings[]._sellPrice" }.Contains(normalized);
                case "BasicAttackData": case "ActiveSkillData": return new[] { "_basicAttackRange", "_basicAttackDelay", "_skillRange", "_skillCooldown", "_castTime", "_maxTargetCount", "_maxDamageableCount", "_maxEffectTargetCount", "_areaRadius", "_areaAngle", "_projectileSpeed", "_dashDistance", "_dashSpeed" }.Contains(normalized);
                default: return false;
            }
        }

        private static string Describe(SerializedObject owner, SerializedProperty property)
        {
            string path = property.propertyPath;
            if (owner.targetObject is WaveRewardTable)
            {
                string rewardRoot = path.Substring(0, path.LastIndexOf('.'));
                string waveRoot = rewardRoot.Substring(0, rewardRoot.IndexOf("._rewards", StringComparison.Ordinal));
                string quarterRoot = waveRoot.Substring(0, waveRoot.IndexOf("._waves", StringComparison.Ordinal));
                var currency = owner.FindProperty(rewardRoot + "._currency").objectReferenceValue;
                return owner.FindProperty(quarterRoot + "._quarterNumber").intValue + "분기 / " + owner.FindProperty(waveRoot + "._waveNumber").intValue + "노드 / " + (currency != null ? currency.name : "재화") + " 보상";
            }
            if (owner.targetObject.GetType().Name == "UnitData")
            {
                var stat = owner.FindProperty(path.Replace("._value", "._statType"));
                if (stat != null && stat.enumValueIndex >= 0 && stat.enumValueIndex < stat.enumDisplayNames.Length) return Korean(stat.enumNames[stat.enumValueIndex]);
            }
            string parent = "";
            if (path.Contains("."))
            {
                string parentPath = path.Substring(0, path.LastIndexOf('.'));
                string readable = Regex.Replace(parentPath, @"\.Array\.data\[(\d+)\]", m => " (" + (int.Parse(m.Groups[1].Value) + 1) + ")");
                parent = string.Join(" / ", readable.Split('.').Select(part =>
                {
                    int suffix = part.IndexOf(" (", StringComparison.Ordinal);
                    return suffix < 0 ? Korean(part) : Korean(part.Substring(0, suffix)) + part.Substring(suffix);
                })) + " / ";
                var context = owner.FindProperty(parentPath);
                if (context != null)
                {
                    foreach (string field in new[] { "_statType", "_rarity", "_operation", "_currency", "currency" })
                    {
                        var sibling = context.FindPropertyRelative(field);
                        if (sibling == null) continue;
                        if (sibling.propertyType == SerializedPropertyType.Enum && sibling.enumValueIndex >= 0 && sibling.enumValueIndex < sibling.enumNames.Length)
                            parent += Korean(sibling.enumNames[sibling.enumValueIndex]) + " / ";
                        else if (sibling.propertyType == SerializedPropertyType.ObjectReference && sibling.objectReferenceValue != null)
                            parent += sibling.objectReferenceValue.name + " / ";
                    }
                }
            }
            return parent + Korean(property.name);
        }

        private static string Korean(string name)
        {
            switch (name.TrimStart('_'))
            {
                case "MaxHp": return "최대 체력";
                case "buildCost": return "건설 비용";
                case "upgrades": return "업그레이드";
                case "cost": return "비용";
                case "production": return "자원 생산";
                case "spawn": return "유닛 생산";
                case "unitStatEffects": return "유닛 능력치 효과";
                case "currencyEffects": return "재화 효과";
                case "consumableSlotEffects": return "소모품 슬롯 효과";
                case "raritySettings": return "등급별 설정";
                case "Weights": return "병종 등장 비중";
                case "Common": return "일반";
                case "Rare": return "희귀";
                case "Legendary": return "전설";
                case "Mythic": return "신화";
                case "AttackPower": return "공격력";
                case "Defense": return "방어력";
                case "MoveSpeed": return "이동 속도";
                case "AttackSpeed": return "공격 속도";
                case "amount": return "수량 / 비용";
                case "value": return "효과 수치";
                case "countPerWave": return "웨이브당 생산 수";
                case "maxAlive": return "최대 생존 수";
                case "requiredCoreLevel": return "필요 코어 레벨";
                case "basicAttackRange": case "skillRange": return "사거리";
                case "basicAttackDelay": return "일반 공격 지연";
                case "skillCooldown": return "스킬 재사용 시간";
                case "tickInterval": return "패시브 주기 (초 / 주기형 트리거에서 사용)";
                case "conditionCheckInterval": return "패시브 조건 검사 간격 (초)";
                case "castTime": return "시전 시간";
                case "maxTargetCount": return "최대 대상 수";
                case "maxDamageableCount": case "maxEffectTargetCount": return "최대 효과 대상 수";
                case "areaRadius": return "범위 반경";
                case "areaAngle": return "범위 각도";
                case "projectileSpeed": return "투사체 속도";
                case "dashDistance": return "돌진 거리";
                case "dashSpeed": return "돌진 속도";
                case "maxStacks": return "최대 중첩 수";
                case "candidateCount": return "선택 후보 수";
                case "commonWeight": return "일반 등급 가중치";
                case "rareWeight": return "희귀 등급 가중치";
                case "legendaryWeight": return "전설 등급 가중치";
                case "weight": return "등장 가중치";
                case "minPrice": return "최소 구매 가격";
                case "maxPrice": return "최대 구매 가격";
                case "sellPrice": return "판매 가격";
                case "artifactSlotCount": case "slotCount": return "상점 슬롯 수";
                case "additionalSlots": return "추가 소모품 슬롯 수";
                case "Tanker": return "탱커 가중치";
                case "Bruiser": return "브루저 가중치";
                case "Assassin": return "암살자 가중치";
                case "RangedPhysical": return "물리 원거리 가중치";
                case "RangedMagic": return "마법 원거리 가중치";
                case "Supporter": return "서포터 가중치";
                default: return ObjectNames.NicifyVariableName(name);
            }
        }

        public static string Hash(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        public static string Structure(Object target)
        {
            var text = new StringBuilder();
            var iterator = new SerializedObject(target).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.Generic || Allowed(target.GetType().Name, iterator.propertyPath)) continue;
                text.Append(iterator.propertyPath).Append(':').Append(iterator.propertyType).Append('=');
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (iterator.objectReferenceValue != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(iterator.objectReferenceValue, out string guid, out long id)) text.Append(guid).Append('/').Append(id);
                    else text.Append(iterator.objectReferenceInstanceIDValue);
                }
                else
                {
                    switch (iterator.propertyType)
                    {
                        case SerializedPropertyType.String: text.Append(iterator.stringValue); break;
                        case SerializedPropertyType.Integer: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Character: text.Append(iterator.longValue); break;
                        case SerializedPropertyType.Enum: text.Append(iterator.intValue); break;
                        case SerializedPropertyType.Boolean: text.Append(iterator.boolValue); break;
                        case SerializedPropertyType.Float: text.Append(iterator.doubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); break;
                        case SerializedPropertyType.ManagedReference: text.Append(iterator.managedReferenceFullTypename); break;
                        default: text.Append(iterator.contentHash); break;
                    }
                }
                text.AppendLine();
            }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        private static string Number(SerializedProperty property) => property.propertyType == SerializedPropertyType.Integer ? property.longValue.ToString() : property.doubleValue.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);

        public static List<string> GetChanges(Entry entry)
        {
            if (!IsWorkspacePath(entry.working) || !IsSourcePath(entry.source)) throw new InvalidOperationException("허용되지 않은 경로");
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.source);
            var working = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working);
            if (source == null || working == null || source.GetType() != working.GetType()) throw new InvalidOperationException("데이터 타입 불일치");
            if (Structure(working) != entry.structure) throw new InvalidOperationException("ID·참조·목록 구조가 변경되었습니다. 수치만 편집할 수 있습니다: " + entry.source);
            var original = new SerializedObject(source);
            var copy = new SerializedObject(working);
            var paths = NumericPaths(source);
            if (!paths.SequenceEqual(NumericPaths(working))) throw new InvalidOperationException("목록 구조 변경 감지: 새로운 작업 공간을 만드세요.");
            return paths.Where(p => Number(original.FindProperty(p)) != Number(copy.FindProperty(p))).Select(p => p + ": " + Number(original.FindProperty(p)) + " → " + Number(copy.FindProperty(p))).ToList();
        }

        public static List<string> Validate(Workspace workspace)
        {
            var issues = new List<string>();
            if (workspace == null || workspace.entries == null || (!string.IsNullOrEmpty(workspace.scene) && !IsWorkspacePath(workspace.scene))) { issues.Add("작업 공간이 유효하지 않습니다."); return issues; }
            var originals = new HashSet<string>(workspace.entries.Select(e => AssetDatabase.AssetPathToGUID(e.source)));
            foreach (var entry in workspace.entries)
            {
                if (!IsWorkspacePath(entry.working) || !File.Exists(entry.working)) { issues.Add("복사본 누락: " + entry.working); continue; }
                if (GuidReference.Matches(File.ReadAllText(entry.working)).Cast<Match>().Any(m => originals.Contains(m.Groups[1].Value))) issues.Add("원본 참조 잔존: " + entry.working);
                if (string.IsNullOrEmpty(entry.group)) continue;
                try
                {
                    GetChanges(entry);
                    var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working);
                    var serialized = new SerializedObject(data);
                    foreach (string path in NumericPaths(data))
                    {
                        var property = serialized.FindProperty(path);
                        double value = property.propertyType == SerializedPropertyType.Integer ? property.longValue : property.doubleValue;
                        bool signedEffect = entry.type == "ArtifactData" && (path.EndsWith("._value", StringComparison.Ordinal) || path.EndsWith("._additionalSlots", StringComparison.Ordinal));
                        if (double.IsNaN(value) || double.IsInfinity(value) || (!signedEffect && value < 0)) issues.Add(data.name + ": 수치 범위 오류 " + path);
                    }
                    if (data is ArtifactRewardTable rewards && !rewards.IsValid) issues.Add(data.name + ": 유물 후보/가중치 오류");
                    if (entry.type == "PassiveSkillData" && (serialized.FindProperty("_tickInterval").floatValue < 0.1f || serialized.FindProperty("_conditionCheckInterval").floatValue < 0.05f))
                        issues.Add(data.name + ": 패시브 주기는 0.1초, 조건 검사 간격은 0.05초 이상이어야 합니다.");
                    if (data is ArtifactData artifact && artifact.MaxStacks < 1) issues.Add(data.name + ": 최대 중첩은 1 이상이어야 합니다.");
                    if (data is Units.UnitDatas.UnitData unit)
                        foreach (var stat in unit.Stats)
                        {
                            var definition = Units.UnitDatas.UnitStatDefinitions.Get(stat.StatType);
                            if (stat.Value < definition.MinValue || stat.Value > definition.MaxValue) issues.Add(data.name + ": " + stat.StatType + " 팀 능력치 허용 범위 초과");
                        }
                    if (data is ShopTable shop && shop.ArtifactSlotCount < 1) issues.Add(data.name + ": 상점 슬롯은 1 이상이어야 합니다.");
                    if (data is ShopArtifactTable prices && prices.RaritySettings.Any(p => p.MinPrice > p.MaxPrice || p.SellPrice > p.MinPrice)) issues.Add(data.name + ": 최소/최대 구매가 및 판매가 관계 오류");
                    if (data is ShopConsumableTable consumables && consumables.MinPrice > consumables.MaxPrice) issues.Add(data.name + ": 최소 구매가가 최대 구매가보다 큽니다.");
                    if (data is WaveSO wave && wave.Weights.Tanker + wave.Weights.Bruiser + wave.Weights.Assassin + wave.Weights.RangedPhysical + wave.Weights.RangedMagic + wave.Weights.Supporter <= 0) issues.Add(data.name + ": 가중치 합은 양수여야 합니다.");
                    if (data is OZGL.KDH.BuildingData building && building.HasSpawn && (building.Spawn.countPerWave <= 0 || building.Spawn.maxAlive < building.Spawn.countPerWave)) issues.Add(data.name + ": 생산 수/최대 생존 수 확인");
                }
                catch (Exception exception) { issues.Add(entry.source + ": " + exception.Message); }
            }
            return issues;
        }

        public static void Publish(Entry entry)
        {
            if (Busy) throw new InvalidOperationException("플레이/컴파일 중에는 반영할 수 없습니다.");
            GetChanges(entry);
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.source);
            var working = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entry.working);
            if (EditorUtility.IsDirty(source) || Hash(entry.source) != entry.hash) throw new InvalidOperationException("복사 이후 원본이 변경됐습니다. 팀원 변경 보호를 위해 반영을 중단합니다. 새 작업 공간을 만드세요.");
            // Validate without graph checks; this selected asset alone will be published.
            var validation = Validate(new Workspace { scene = Root + "/validation.unity", entries = new List<Entry> { entry } });
            if (validation.Count != 0) throw new InvalidOperationException(string.Join("\n", validation));
            string backup = "Library/BalanceBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(backup);
            File.Copy(entry.source, backup + "/" + Path.GetFileName(entry.source));
            var original = new SerializedObject(source);
            var copy = new SerializedObject(working);
            Undo.RecordObject(source, "밸런스 수치 반영");
            foreach (string path in NumericPaths(source))
            {
                var a = original.FindProperty(path); var b = copy.FindProperty(path);
                if (a.propertyType == SerializedPropertyType.Integer) a.longValue = b.longValue;
                else a.doubleValue = b.doubleValue;
            }
            original.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(source);
            entry.hash = Hash(entry.source);
            Debug.Log("[Balance] 선택 데이터 수치 반영: " + entry.source + ", 백업: " + backup);
        }

        private static void WriteManifest(string path, Workspace workspace)
        {
            if (!IsWorkspacePath(path)) throw new InvalidOperationException("작업 공간 경로 오류");
            File.WriteAllText(path, JsonUtility.ToJson(workspace, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
        }
    }
}
