using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Units;

namespace Game.QA
{
    public sealed class QABattlePanel : MonoBehaviour
    {
        [SerializeField] private QABattleController _controller;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private TMP_Text _phase, _clock, _message, _counts, _info, _result, _restriction;
        [SerializeField] private UnityEngine.UI.Button _start, _pause, _stop, _reset, _replay, _spawn, _clear;
        [SerializeField] private TMP_Dropdown _team, _kind;
        [SerializeField] private TMP_InputField _quantity;
        [SerializeField] private RectTransform _roster, _table;
        [SerializeField] private GameObject _unitSettings, _restrictedSettings;
        [SerializeField] private UnityEngine.UI.RawImage _arena;
        [SerializeField] private UnityEngine.UI.Button[] _configTabs, _statsTabs, _infoTabs;
        private readonly List<QAUnitOption> _shownOptions = new();
        private readonly List<TMP_Text> _tableValues = new();
        private RenderTexture _renderTexture;
        private int _configTab, _statsTab, _infoTab;
        private float _nextRefresh;
        private static readonly Color Background = new(.10f, .12f, .15f);
        private static readonly Color Surface = new(.16f, .18f, .22f);
        private static readonly Color Ink = new(.90f, .93f, .96f);
        private static readonly Color Muted = new(.65f, .71f, .78f);

        public void Construct(QABattleController controller, TMP_FontAsset font)
        {
            _controller = controller; _font = font;
            var canvasObject = Rect("Canvas", transform);
            canvasObject.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            canvasObject.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Fill(canvasObject, Background);

            var header = Region("Header", canvasObject, 0, 0, 0, 64);
            Label("Title", header, "QA / 전투 밸런스 테스트", 28, 18, 12, 760, 38);
            Label("ScenePath", header, "Scenes/Test/QA · 유닛 전투 QA", 18, 790, 18, 600, 28, Muted);
            var toolbar = Region("BattleControls", canvasObject, 12, 0, 68, 58);
            Fill(toolbar, Surface);
            _phase = Label("Phase", toolbar, "준비", 21, 12, 9, 120, 40);
            _clock = Label("Clock", toolbar, "00:00.0", 22, 140, 9, 170, 40);
            _start = Button("StartBattle", toolbar, "전투 시작", 490, 8, 180, 42);
            _pause = Button("PauseResume", toolbar, "일시정지", 678, 8, 180, 42);
            _stop = Button("StopBattle", toolbar, "전투 중단", 866, 8, 180, 42);
            _reset = Button("ResetBattle", toolbar, "전투 초기화", 1054, 8, 205, 42);
            _replay = Button("ReplayBattle", toolbar, "같은 조건 재전투", 1267, 8, 240, 42);

            var left = Rect("Configuration", canvasObject);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
            left.offsetMin = new Vector2(12, 320); left.offsetMax = new Vector2(342, -138); Fill(left, Surface);
            Label("Heading", left, "병력 구성", 22, 14, 9, 250, 34);
            _configTabs = Tabs(left, "ConfigTabs", new[] { "유닛", "아티팩트", "제단", "특성" }, 49, 306, 16);
            _unitSettings = Rect("UnitSettings", left).gameObject;
            Stretch((RectTransform)_unitSettings.transform, 12, 12, 98, 12);
            Label("TeamLabel", _unitSettings.transform, "진영", 18, 0, 0, 285, 26, Muted);
            _team = Dropdown("TeamDropdown", _unitSettings.transform, 0, 29, 306, 40);
            _team.AddOptions(new List<string> { "아군", "적군" });
            Label("KindLabel", _unitSettings.transform, "유닛 종류", 18, 0, 80, 300, 26, Muted);
            _kind = Dropdown("UnitDropdown", _unitSettings.transform, 0, 109, 306, 44);
            Label("CountLabel", _unitSettings.transform, "수량 (1~5)", 18, 0, 165, 110, 25, Muted);
            _quantity = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources()).GetComponent<TMP_InputField>();
            _quantity.name = "SpawnCount"; _quantity.transform.SetParent(_unitSettings.transform, false);
            Position((RectTransform)_quantity.transform, 0, 194, 95, 42); _quantity.contentType = TMP_InputField.ContentType.IntegerNumber;
            _quantity.text = "1"; _quantity.characterLimit = 2; ApplyFont(_quantity.gameObject); _quantity.GetComponent<UnityEngine.UI.Image>().color = Background;
            _spawn = Button("SpawnUnits", _unitSettings.transform, "유닛 생성", 105, 194, 201, 42);
            Label("RosterLabel", _unitSettings.transform, "현재 병력 · 선택 / 삭제", 18, 0, 249, 300, 26, Muted);
            var rosterScroll = Rect("RosterScroll", _unitSettings.transform); Stretch(rosterScroll, 0, 0, 282, 83);
            _roster = ScrollContent(rosterScroll);
            _clear = Button("ClearComposition", _unitSettings.transform, "구성 비우기", 0, 0, 306, 40);
            var clearRect = (RectTransform)_clear.transform; clearRect.anchorMin = clearRect.anchorMax = new Vector2(0, 0);
            clearRect.pivot = Vector2.zero; clearRect.anchoredPosition = new Vector2(0, 33);
            var editNote = Label("ConfigurationNote", _unitSettings.transform, "초기화 후 구성 변경 가능", 15, 0, 0, 305, 27, Muted);
            var noteRect = (RectTransform)editNote.transform; noteRect.anchorMin = noteRect.anchorMax = Vector2.zero; noteRect.pivot = Vector2.zero;
            _restrictedSettings = Rect("RestrictedSettings", left).gameObject; Stretch((RectTransform)_restrictedSettings.transform, 14, 14, 104, 14);
            _restriction = Label("RestrictionReason", _restrictedSettings.transform, "", 20, 0, 0, 296, 400, Muted);
            _restrictedSettings.SetActive(false);

            var center = Rect("Battlefield", canvasObject); Stretch(center, 356, 366, 138, 320); Fill(center, Surface);
            Label("Heading", center, "전장", 22, 14, 8, 220, 34);
            _counts = Label("TeamCounts", center, "아군 0 / 적군 0", 19, 245, 10, 600, 34, Muted);
            var arenaRect = Rect("ArenaView", center); Stretch(arenaRect, 12, 12, 54, 78);
            _arena = arenaRect.gameObject.AddComponent<UnityEngine.UI.RawImage>(); _arena.color = Color.white;
            arenaRect.gameObject.AddComponent<QAArenaInput>().Configure(controller, _arena);
            _message = Label("BattleMessage", center, "아군과 적군을 생성하세요.", 19, 14, 0, 0, 58, Muted);
            var msgRect = (RectTransform)_message.transform; msgRect.anchorMin = new Vector2(0, 0); msgRect.anchorMax = new Vector2(1, 0);
            msgRect.pivot = Vector2.zero; msgRect.offsetMin = new Vector2(14, 10); msgRect.offsetMax = new Vector2(-14, 68);

            var right = Rect("UnitInspector", canvasObject); right.anchorMin = new Vector2(1, 0); right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(-354, 320); right.offsetMax = new Vector2(-12, -138); Fill(right, Surface);
            Label("Heading", right, "선택 유닛", 22, 14, 9, 310, 34);
            _infoTabs = Tabs(right, "InspectorTabs", new[] { "상태", "전투 통계" }, 49, 318, 19);
            var infoScroll = Rect("InfoScroll", right); Stretch(infoScroll, 14, 14, 106, 14);
            var infoContent = ScrollContent(infoScroll);
            _info = Label("UnitInformation", infoContent, "전장의 유닛을 선택하세요.", 19, 0, 0, 310, 0);
            var infoLayout = _info.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); infoLayout.minHeight = 440; infoLayout.flexibleWidth = 1;

            var conditions = Rect("AppliedConditions", canvasObject); conditions.anchorMin = new Vector2(0, 0); conditions.anchorMax = new Vector2(1, 0);
            conditions.pivot = Vector2.zero; conditions.offsetMin = new Vector2(12, 273); conditions.offsetMax = new Vector2(-12, 310); Fill(conditions, Surface);
            Label("Conditions", conditions, "적용 조건    아티팩트 없음 · 제단 없음 · 특성 없음    /    미연결 기능은 설정 탭에서 확인", 19, 14, 3, 1840, 31, Muted);
            var stats = Rect("Statistics", canvasObject); stats.anchorMin = new Vector2(0, 0); stats.anchorMax = new Vector2(1, 0);
            stats.pivot = Vector2.zero; stats.offsetMin = new Vector2(12, 12); stats.offsetMax = new Vector2(-12, 261); Fill(stats, Surface);
            Label("Heading", stats, "전투 통계", 22, 14, 7, 180, 32);
            _result = Label("Result", stats, "결과 —", 18, 230, 8, 1500, 30, Muted);
            _statsTabs = Tabs(stats, "StatisticsTabs", new[] { "팀 요약", "유닛별", "스킬 결과", "가한 피해 / DPS · 미지원" }, 45, 1060, 18);
            _statsTabs[3].interactable = false;
            var statsScroll = Rect("StatisticsScroll", stats); Stretch(statsScroll, 14, 14, 92, 31);
            _table = ScrollContent(statsScroll);
            var statNote = Label("StatisticsNote", stats, "받은 HP 피해 / 보호막 흡수 분리 · 실제 회복량 기준 · 초기화: 병력·배치 유지, 현재 통계 지움", 15, 14, 0, 1840, 24, Muted);
            var statNoteRect = (RectTransform)statNote.transform; statNoteRect.anchorMin = statNoteRect.anchorMax = Vector2.zero; statNoteRect.pivot = Vector2.zero; statNoteRect.anchoredPosition = new Vector2(14, 3);
        }

        private void Start()
        {
            _controller.Changed += OnChanged;
            _team.onValueChanged.AddListener(_ => RefreshOptions());
            _kind.onValueChanged.AddListener(_ => RefreshControls());
            _start.onClick.AddListener(_controller.StartBattle); _pause.onClick.AddListener(_controller.TogglePause);
            _stop.onClick.AddListener(_controller.StopBattle); _reset.onClick.AddListener(_controller.ResetBattle);
            _replay.onClick.AddListener(_controller.Replay); _clear.onClick.AddListener(_controller.ClearComposition);
            _spawn.onClick.AddListener(() => { int.TryParse(_quantity.text, out int count); if (_shownOptions.Count > 0) _controller.Spawn(_shownOptions[_kind.value], count); });
            for (int i = 0; i < _configTabs.Length; i++) { int index = i; _configTabs[i].onClick.AddListener(() => { _configTab = index; RefreshControls(); }); }
            for (int i = 0; i < 3; i++) { int index = i; _statsTabs[i].onClick.AddListener(() => { _statsTab = index; RebuildTable(); RefreshData(); }); }
            for (int i = 0; i < _infoTabs.Length; i++) { int index = i; _infoTabs[i].onClick.AddListener(() => { _infoTab = index; RefreshData(); }); }
            RefreshOptions(); OnChanged();
        }
        private void Update()
        {
            ResizeArena();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .1f; RefreshControls(); RefreshData();
        }
        private void ResizeArena()
        {
            int width = Mathf.Clamp(Mathf.RoundToInt(_arena.rectTransform.rect.width), 128, 1920);
            int height = Mathf.Clamp(Mathf.RoundToInt(_arena.rectTransform.rect.height), 128, 1080);
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return;
            if (_renderTexture != null) { _controller.BattleCamera.targetTexture = null; _renderTexture.Release(); Destroy(_renderTexture); }
            _renderTexture = new RenderTexture(width, height, 24) { name = "QA_Battlefield" }; _renderTexture.Create();
            _controller.BattleCamera.targetTexture = _renderTexture; _arena.texture = _renderTexture;
        }
        private void OnChanged() { RebuildRoster(); RebuildTable(); RefreshControls(); RefreshData(); }
        private void RefreshOptions()
        {
            _shownOptions.Clear(); _shownOptions.AddRange(_controller.Options.Where(o => o.Team == (_team.value == 0 ? UnitTeam.Ally : UnitTeam.Enemy)).OrderByDescending(o => o.Available).ThenBy(o => o.Label));
            _kind.ClearOptions(); _kind.AddOptions(_shownOptions.Select(o => o.Label + (o.Available ? "" : " · 생성 불가")).ToList());
            RefreshControls();
        }
        private void RefreshControls()
        {
            bool ready = _controller.CanEdit, battle = _controller.Phase == QABattlePhase.Fighting, paused = _controller.Phase == QABattlePhase.Paused;
            _start.interactable = _controller.CanStart; _pause.interactable = battle || paused;
            _pause.GetComponentInChildren<TMP_Text>().text = paused ? "재개" : "일시정지";
            _stop.interactable = battle || paused; _replay.interactable = _controller.Phase == QABattlePhase.Finished;
            _reset.interactable = true; _team.interactable = ready; _kind.interactable = ready; _quantity.interactable = ready;
            _spawn.interactable = ready && _shownOptions.Count > 0 && _shownOptions[_kind.value].Available; _clear.interactable = ready;
            _unitSettings.SetActive(_configTab == 0); _restrictedSettings.SetActive(_configTab != 0);
            _restriction.text = _configTab switch {
                1 => "아티팩트 · 사용 제한\n\n유닛 능력치 효과\n미연결: 유닛 적용 연결 필요\n\n외부 패시브 효과\n미연결: 패시브 전달 연결 필요\n\n현재 전투에는 적용되지 않습니다.",
                2 => "제단 · 사용 제한\n\n기존 직접 적용 경로는 존재합니다.\nQA 진입 구성과 효과 데이터 검증 후 개방합니다.\n\n현재 전투에는 적용되지 않습니다.",
                3 => "특성 · 사용 제한\n\n레벨 적용·초기화\n미연결: QA 직접 적용·해제 경로와 효과 데이터 확인 필요\n\n현재 전투에는 적용되지 않습니다.", _ => "" };
            if (_shownOptions.Count > 0 && !_shownOptions[_kind.value].Available && _configTab == 0)
                _message.text = "생성 불가: " + _shownOptions[_kind.value].UnavailableReason;
            TintTabs(_configTabs, _configTab); TintTabs(_statsTabs, _statsTab); TintTabs(_infoTabs, _infoTab);
        }
        private void RefreshData()
        {
            _phase.text = _controller.Phase switch { QABattlePhase.Preparing => "생성 중", QABattlePhase.Ready => "준비", QABattlePhase.Fighting => "전투 중", QABattlePhase.Paused => "일시정지", _ => "종료" };
            _clock.text = TimeSpan.FromSeconds(_controller.Statistics.Elapsed).ToString(@"mm\:ss\.f");
            _message.text = _shownOptions.Count > 0 && !_shownOptions[_kind.value].Available && _configTab == 0 ? "생성 불가: " + _shownOptions[_kind.value].UnavailableReason : _controller.Message;
            var records = _controller.Statistics.Records;
            _counts.text = $"아군 {records.Count(r => r.Team == UnitTeam.Ally && !r.Dead)} / {records.Count(r => r.Team == UnitTeam.Ally)}     적군 {records.Count(r => r.Team == UnitTeam.Enemy && !r.Dead)} / {records.Count(r => r.Team == UnitTeam.Enemy)}";
            _result.text = $"결과 {_controller.Result}    시간 {_clock.text}    {(_controller.Phase == QABattlePhase.Finished ? "종료 시점 결과 고정" : "현재 전투 집계")}";
            RefreshInspector(records.FirstOrDefault(r => r.Id == _controller.SelectedId));
            var values = TableRows();
            int index = 0; foreach (var row in values) foreach (string value in row) { if (index < _tableValues.Count) _tableValues[index].text = value; index++; }
        }
        private void RefreshInspector(QAUnitStatistics record)
        {
            if (record == null) { _info.text = "전장의 유닛을 선택하세요."; return; }
            var text = new StringBuilder(); text.AppendLine($"#{record.Id}  {record.Label}"); text.AppendLine($"{(record.Team == UnitTeam.Ally ? "아군" : "적군")} · {(record.Dead ? "사망" : "생존")}\n");
            if (_infoTab == 1)
            {
                text.AppendLine($"처치 수     {record.Kills}\n생존 시간     {record.SurvivalTime:F1}초\n받은 HP 피해     {record.HpDamage:F1}\n보호막 흡수     {record.ShieldAbsorbed:F1}\n실제 회복량     {record.Healing:F1}\n보호막 부여     {record.ShieldGranted:F1}\n\n스킬 성공 / 실패 / 중단\n{record.SkillSuccess} / {record.SkillFailure} / {record.SkillInterrupted}\n\n마지막 실행 사유\n{record.LastSkillReason ?? "—"}\n\n가한 피해·DPS: 집계 미지원");
            }
            else
            {
                text.AppendLine($"HP     {record.Hp:F1} / {record.MaxHp:F1}\n보호막     {record.Shield:F1}");
                if (record.Unit != null)
                {
                    var status = record.Unit.RuntimeStatus; var ai = record.Unit.GetComponent<Unit_AI>();
                    text.AppendLine($"공격력     {status.AttackPower:F1}\n공격 속도     {status.AttackSpeed:F2}\n방어력     {status.Defense:F1}\n이동 속도     {status.MoveSpeed:F1}\n치명타 확률     {status.CriticalChance:P0}");
                    text.AppendLine($"현재 행동     {(record.Dead ? "사망" : _controller.Phase == QABattlePhase.Finished ? "전투 종료" : ai?.CurrentActionType.ToString() ?? "—")}\n현재 대상     {(ai?.CurrentTarget as Unit_Gateway)?.name ?? "—"}\n\n상태 효과");
                    if (status.ActiveEffects.Count == 0) text.AppendLine("없음");
                    foreach (var effect in status.ActiveEffects) text.AppendLine($"{effect.Data.name} ×{effect.StackCount} · {(effect.IsInfinite ? "무한" : Mathf.Max(0, effect.ExpireTime - Time.time).ToString("F1") + "초")}");
                    text.AppendLine($"\n추가 최종 능력치\n치명타 피해     ×{status.CriticalDamage:F2}\n쿨다운 감소     {status.CooldownReduction:P0}\n감지 범위     {status.DetectionRange:F1}\n방어 무시     {status.DefenseIgnore:P0}\n흡혈     {status.LifeSteal:P0}\n전체 피해 배율     ×{status.DamageMultiplier:F2}\n기본 공격 배율     ×{status.BasicAttackMultiplier:F2}\n스킬 피해 배율     ×{status.SkillDamageMultiplier:F2}\n받는 피해 배율     ×{status.DamageTakenMultiplier:F2}\n회복 배율     ×{status.HealingMultiplier:F2}\n받는 회복 배율     ×{status.HealingTakenMultiplier:F2}");
                }
                else text.AppendLine("사망한 유닛 · 마지막 HP/보호막 유지");
            }
            _info.text = text.ToString();
        }
        private void RebuildRoster()
        {
            ClearChildren(_roster);
            foreach (var entry in _controller.Entries)
            {
                var row = Rect("Unit_" + entry.Id, _roster); row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44;
                var choose = Button("Select", row, $"#{entry.Id} {(entry.Team == UnitTeam.Ally ? "아" : "적")} {entry.Label}", 0, 0, 247, 40, 16);
                int id = entry.Id; choose.onClick.AddListener(() => _controller.Select(id));
                var delete = Button("Remove", row, "삭제", 251, 0, 49, 40, 16); delete.interactable = _controller.CanEdit;
                delete.onClick.AddListener(() => _controller.Remove(id));
            }
        }
        private IEnumerable<string[]> TableRows()
        {
            var records = _controller.Statistics.Records;
            if (_statsTab == 0)
            {
                yield return new[] { "진영", "생존 / 사망 / 전체", "처치", "잔여 HP", "잔여 보호막", "받은 HP 피해", "보호막 흡수", "실제 회복", "보호막 부여" };
                foreach (var team in new[] { UnitTeam.Ally, UnitTeam.Enemy })
                { var rows = records.Where(r => r.Team == team).ToList(); yield return new[] { team == UnitTeam.Ally ? "아군" : "적군", $"{rows.Count(r => !r.Dead)} / {rows.Count(r => r.Dead)} / {rows.Count}", rows.Sum(r => r.Kills).ToString(), rows.Sum(r => r.Hp).ToString("F1"), rows.Sum(r => r.Shield).ToString("F1"), rows.Sum(r => r.HpDamage).ToString("F1"), rows.Sum(r => r.ShieldAbsorbed).ToString("F1"), rows.Sum(r => r.Healing).ToString("F1"), rows.Sum(r => r.ShieldGranted).ToString("F1") }; }
            }
            else
            {
                yield return _statsTab == 1 ? new[] { "유닛", "상태", "처치", "생존 시간", "잔여 HP", "받은 HP 피해", "보호막 흡수", "실제 회복", "보호막 부여" } : new[] { "유닛", "성공", "실패", "중단", "마지막 실행 사유" };
                foreach (var r in records) yield return _statsTab == 1 ? new[] { $"#{r.Id} {r.Label}", r.Dead ? "사망" : "생존", r.Kills.ToString(), r.SurvivalTime.ToString("F1") + "초", r.Hp.ToString("F1"), r.HpDamage.ToString("F1"), r.ShieldAbsorbed.ToString("F1"), r.Healing.ToString("F1"), r.ShieldGranted.ToString("F1") } : new[] { $"#{r.Id} {r.Label}", r.SkillSuccess.ToString(), r.SkillFailure.ToString(), r.SkillInterrupted.ToString(), r.LastSkillReason ?? "—" };
            }
        }
        private void RebuildTable()
        {
            ClearChildren(_table); _tableValues.Clear(); int rowIndex = 0;
            foreach (var values in TableRows())
            {
                var row = Rect("Row_" + rowIndex, _table); row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 35;
                var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 10;
                foreach (string value in values)
                { var label = Label("Cell", row, value, 17, 0, 0, 160, 30, rowIndex == 0 ? Muted : Ink); label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1; _tableValues.Add(label); }
                rowIndex++;
            }
        }
        private void OnDestroy()
        {
            if (_controller != null) { _controller.Changed -= OnChanged; if (_controller.BattleCamera != null) _controller.BattleCamera.targetTexture = null; }
            if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); }
        }
        private void TintTabs(UnityEngine.UI.Button[] tabs, int selected)
        { for (int i = 0; i < tabs.Length; i++) tabs[i].GetComponent<UnityEngine.UI.Image>().color = i == selected ? new Color(.27f, .34f, .43f) : Surface; }
        private void ApplyFont(GameObject target)
        { foreach (var label in target.GetComponentsInChildren<TMP_Text>(true)) { label.font = _font; label.fontSize = 19; label.color = Ink; label.raycastTarget = false; } }
        private TMP_Dropdown Dropdown(string name, Transform parent, float x, float y, float width, float height)
        { var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false); Position((RectTransform)go.transform, x, y, width, height); ApplyFont(go); var dropdown = go.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); foreach (var image in go.GetComponentsInChildren<UnityEngine.UI.Image>(true)) image.color = image.name.Contains("Checkmark") ? Ink : Background; return dropdown; }
        private UnityEngine.UI.Button[] Tabs(Transform parent, string name, string[] labels, float y, float width, int fontSize)
        { var row = Rect(name, parent); Position(row, 12, y, width, 40); return labels.Select((label, i) => Button(label, row, label, i * width / labels.Length, 0, width / labels.Length - 4, 39, fontSize)).ToArray(); }
        private UnityEngine.UI.Button Button(string name, Transform parent, string caption, float x, float y, float width, float height, int size = 20)
        { var rect = Rect(name, parent); Position(rect, x, y, width, height); var image = Fill(rect, new Color(.22f, .26f, .32f), true); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; var label = Label("Label", rect, caption, size, 0, 0, width, height); label.alignment = TextAlignmentOptions.Center; Stretch((RectTransform)label.transform, 4, 4, 0, 0); return button; }
        private TMP_Text Label(string name, Transform parent, string caption, int size, float x, float y, float width, float height, Color? color = null)
        { var rect = Rect(name, parent); Position(rect, x, y, width, height); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = _font; text.fontSize = size; text.color = color ?? Ink; text.text = caption; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; return text; }
        private static RectTransform Rect(string name, Transform parent)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static void Position(RectTransform rect, float x, float y, float width, float height)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static RectTransform Region(string name, Transform parent, float left, float right, float top, float height)
        { var rect = Rect(name, parent); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0, 1); rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top); return rect; }
        private static UnityEngine.UI.Image Fill(RectTransform rect, Color color, bool raycast = false)
        { var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = raycast; return image; }
        private static RectTransform ScrollContent(RectTransform scrollRoot)
        {
            var viewport = Rect("Viewport", scrollRoot); Stretch(viewport, 0, 0, 0, 0);
            Fill(viewport, Color.white, true); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 4;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; return content;
        }
        private static void ClearChildren(Transform parent)
        { foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
    }
}
