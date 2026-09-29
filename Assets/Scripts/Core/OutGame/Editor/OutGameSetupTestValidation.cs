using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

// 저장된 테스트 씬을 별도로 열어 실제 버튼 이벤트와 Selector 결과를 검사합니다.
// Edit Mode에서 실행하며, 검사 중 바꾼 상태를 씬/프리팹에 저장하지 않습니다.
public static class OutGameSetupTestValidation
{
    private static GameObject _root;
    private static OutGameBootstrap _bootstrap;
    private static OutGameStartController _startController;
    private static OutGameUIController _uiController;
    private static PersistentCurrencyManager _wallet;
    private static SaveManager _saveManager;
    private static OutGameTraitController _traits;
    private static OutGameTotemController _totems;
    private static Camera _camera;
    private static RenderTexture _target;
    private static EventSystem _events;
    private static int _assertions;
    private static readonly List<string> Report = new List<string>();
    private static readonly List<string> LayoutIssues = new List<string>();

    [MenuItem("Tools/OutGame/Validate Setup Test")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 후 실행해주세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        _assertions = 0;
        Report.Clear();
        LayoutIssues.Clear();
        Directory.CreateDirectory(".utmp/OutGameSetupValidation");
        try
        {
            EditorSceneManager.OpenScene(OutGameSetupTestBuilder.ScenePath);
            _bootstrap = Object.FindFirstObjectByType<OutGameBootstrap>();
            _root = _bootstrap.GetComponentInParent<Canvas>().gameObject;
            _startController = _root.GetComponentInChildren<OutGameStartController>(true);
            _uiController = _root.GetComponentInChildren<OutGameUIController>(true);
            _wallet = _root.GetComponentInChildren<PersistentCurrencyManager>(true);
            _saveManager = _root.GetComponentInChildren<SaveManager>(true);
            Require(_saveManager != null, "Persistent SaveManager connected");
            // 실제 플레이어의 영구 저장 파일을 읽거나 덮어쓰지 않습니다.
            _saveManager.ConfigureDirectory(Path.GetFullPath(".utmp/OutGameSetupValidation/Saves-" + Guid.NewGuid().ToString("N")));
            _traits = _root.GetComponentInChildren<OutGameTraitController>(true);
            _totems = _root.GetComponentInChildren<OutGameTotemController>(true);
            _events = Object.FindFirstObjectByType<EventSystem>();
            Require(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "EventSystem exactly one");
            foreach (Component component in _root.GetComponentsInChildren<Component>(true)) Require(component != null, "No missing scripts");
            Require(_bootstrap.Initialize(), "Bootstrap references and initialize");
            PreparePreview();
            ValidateCoreUISeparation();
            ValidateInitialAndAltars();
            ValidateTraits();
            ValidateTotemsAndSnapshot();
            ValidateResetAndSubscription();
            Require(LayoutIssues.Count == 0, string.Join("; ", LayoutIssues));
            Report.Add("PASS: " + _assertions + " assertions");
            Report.Add("Actual uGUI pointer events and GraphicRaycaster tested in Editor; no interactive Play session driven.");
            File.WriteAllLines(".utmp/OutGameSetupValidation/report.txt", Report);
            Debug.Log("OUTGAME_SETUP_VALIDATED\n" + string.Join("\n", Report));
        }
        finally
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static void ValidateCoreUISeparation()
    {
        _uiController.ShowTraits();
        _startController.Initialize(_root.GetComponentInChildren<OutGameAltarController>(true), _traits, _totems);
        Require(Panel("Trait").activeSelf && !Panel("Altar").activeSelf, "Core initialize leaves UI screen unchanged");
        _uiController.ShowAltar();
        Require(Panel("Altar").activeSelf && !Panel("Trait").activeSelf, "UI controller owns screen switching");
        Report.Add("Separation: core initialize preserves current screen; UI controller changes screens.");
    }

    private static void ValidateInitialAndAltars()
    {
        Require(Panel("Altar").activeSelf && !Panel("Trait").activeSelf && !Panel("Totem").activeSelf, "Only altar initially visible");
        Require(_wallet.Balance == 500, "Initial bloodstone 500");
        OutGameStartContext initial = _startController.CreateStartContext();
        Require(initial.SelectedAltar == AltarId.Abundance && initial.Traits.Count == 9 && initial.Totems.Count == 8, "Initial context includes all entries");
        foreach (TraitLevelEntry item in initial.Traits) Require(item.Level == 0, "Initial trait zero");
        foreach (TotemLevelEntry item in initial.Totems) Require(item.Level == 0, "Initial totem zero");
        Require(initial.RewardBonusPercent == 0, "Initial bonus zero");
        Require(!UI("Altar", "Navigation/PreviousButton").GetComponent<UnityEngine.UI.Button>().interactable, "Initial previous disabled");
        Render("01-altar");
        string[] conditions = { "5분기", "500마리", "무한모드" };
        for (int i = 0; i < conditions.Length; i++)
        {
            Click("Altar", "AltarSelection/AltarButton0" + (i + 2));
            Require(Text("Altar", "AltarInfoPanel/AltarInfoText").Contains(conditions[i]), "Locked condition displayed " + i);
            Require(_startController.CreateStartContext().SelectedAltar == AltarId.Abundance, "Locked click preserves selection");
        }
        Render("02-altar-locked");
        Click("Altar", "AltarSelection/AltarButton01");
        Report.Add("Altars: default selection, three conditions, locked preview, previous disabled passed.");
    }

    private static void ValidateTraits()
    {
        OutGameStartContext beforePurchase = _startController.CreateStartContext();
        Click("Altar", "Navigation/TraitsButton");
        Require(Panel("Trait").activeSelf, "Traits navigation");
        Click("Trait", "TraitTree/ProductionTraitButton");
        Require(!Upgrade().interactable, "Prerequisite blocks purchase");
        Require(Text("Trait", "TraitDetailsPanel/UpgradeTraitButton/Label").Contains("#D32F2F"), "Prerequisite cost red");
        Click("Trait", "TraitDetailsPanel/UpgradeTraitButton");
        Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.Production) == 0, "Disabled purchase unchanged");
        Render("03-trait-prerequisite");
        Purchase("StartingGoldTraitButton");
        Require(_wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1, "Single purchase costs 100 once");
        foreach (TraitLevelEntry entry in beforePurchase.Traits)
            if (entry.Id == TraitId.StartingGold) Require(entry.Level == 0, "Trait snapshot entry detached");
        Color owned = UI("Trait", "TraitTree/StartingGoldTraitButton").GetComponent<UnityEngine.UI.Image>().color;
        Require(owned.b > owned.r && owned.g > owned.r, "Owned trait light blue");
        Purchase("WaveTraitButton");
        Purchase("KillGoldTraitButton");
        Require(_wallet.Balance == 100 && _traits.GetLevel(TraitId.WaveReward) == 1 && _traits.GetLevel(TraitId.KillGold) == 1, "Two-level prerequisite chain");
        Click("Trait", "TraitTree/MiddleRootTraitButton");
        Require(!Upgrade().interactable && Text("Trait", "TraitDetailsPanel/UpgradeTraitButton/Label").Contains("#D32F2F"), "Insufficient balance red and disabled");
        Render("04-trait-insufficient");
        Purchase("StartingGoldTraitButton");
        Require(_wallet.Balance == 0 && _traits.GetLevel(TraitId.StartingGold) == 2, "Exact-price purchase accepted");
        Click("Trait", "TraitDetailsPanel/UpgradeTraitButton");
        Require(_wallet.Balance == 0 && _traits.GetLevel(TraitId.StartingGold) == 2, "Insufficient purchase makes no changes");
        Require(Text("Trait", "TraitDetailsPanel/TraitLevel") == "(2 / 5)", "Current/max label");
        Click("Trait", "OutGameNavigationButton");
        Require(Panel("Altar").activeSelf, "Trait back navigation");
        Report.Add("Traits: prerequisite chain, disabled clicks, single spend, exact balance, insufficient balance, owned color passed.");
    }

    private static void ValidateTotemsAndSnapshot()
    {
        Click("Altar", "Navigation/NextButton");
        Require(Panel("Totem").activeSelf, "Totem navigation");
        string first = "TotemSelection/TotemColumn01/Level3Button";
        Click("Totem", first, PointerEventData.InputButton.Right);
        Require(_totems.GetLevel(TotemId.EnemyDamage) == 0, "Lower clamp");
        for (int i = 0; i < 4; i++)
        {
            Click("Totem", first);
            Require(_totems.GetLevel(TotemId.EnemyDamage) == Mathf.Min(i + 1, 3), "Left click increments exactly once");
        }
        Require(_totems.GetLevel(TotemId.EnemyDamage) == 3, "Upper clamp at three");
        Click("Totem", "TotemSelection/TotemColumn02Button");
        Click("Totem", "TotemSelection/TotemColumn02Button");
        Require(_totems.GetLevel(TotemId.EchoAttack) == 1 && _totems.GetRewardBonusPercent() == 80, "Toggle max one and sum bonus 80");
        Render("05-totems-80");
        Click("Totem", "StartButton");
        OutGameStartContext snapshot = _startController.LastStartContext;
        Require(snapshot != null && snapshot.Traits.Count == 9 && snapshot.Totems.Count == 8 && snapshot.RewardBonusPercent == 80, "Start stores full context");
        Require(Panel("Totem").activeSelf, "Start stays on totem panel");
        Click("Totem", first, PointerEventData.InputButton.Right);
        Require(_totems.GetLevel(TotemId.EnemyDamage) == 2 && snapshot.RewardBonusPercent == 80, "Snapshot detached from later changes");
        foreach (TotemLevelEntry item in snapshot.Totems)
            if (item.Id == TotemId.EnemyDamage) Require(item.Level == 3, "Snapshot entry detached");
        Click("Totem", "TotemSelection/NextTotemsButton");
        Require(!UI("Totem", "TotemSelection/TotemColumn01").activeSelf && UI("Totem", "TotemSelection/TotemColumn02Button").activeSelf, "Second page hides unused columns");
        Click("Totem", "TotemSelection/TotemColumn02Button");
        Click("Totem", "TotemSelection/TotemColumn05Button");
        Require(_totems.GetLevel(TotemId.DeathBurst) == 1 && _totems.GetLevel(TotemId.Pursuer) == 1, "Second page toggles independent");
        Render("06-totems-page-two");
        Click("Totem", "TotemSelection/PreviousTotemsButton");
        Require(_totems.GetLevel(TotemId.EchoAttack) == 1 && _totems.GetLevel(TotemId.EnemyDamage) == 2, "Page state retained");
        Click("Totem", "PreviousButton");
        Click("Altar", "Navigation/NextButton");
        Require(_totems.GetLevel(TotemId.EnemyDamage) == 2 && _wallet.Balance == 0, "Screen state retained");
        foreach (TotemData data in _totems.Data)
            for (int i = 0; i < data.MaxLevel; i++) _totems.TryChangeLevel(data.Id, 1);
        Require(_totems.GetRewardBonusPercent() == 320, "All totems max bonus 320");
        Click("Totem", "StartButton");
        Require(!ReferenceEquals(snapshot, _startController.LastStartContext), "Each Start creates new context");
        Render("07-totems-320");
        Report.Add("Totems: left/right pointer events, level/toggle bounds, page retention, bonus 0/80/320, detached all-entry context passed.");
    }

    private static void ValidateResetAndSubscription()
    {
        Require(_bootstrap.Initialize(), "Reinitialize");
        Require(_wallet.Balance == 0 && _traits.GetLevel(TraitId.StartingGold) == 2 &&
            _traits.GetLevel(TraitId.WaveReward) == 1 && _traits.GetLevel(TraitId.KillGold) == 1,
            "Reinitialize restores persisted wallet and traits");
        Require(_totems.GetRewardBonusPercent() == 0 && _startController.CreateStartContext().SelectedAltar == AltarId.Abundance,
            "Run selections reset independently of permanent progression");
        Click("Altar", "Navigation/TraitsButton");
        Click("Trait", "TraitTree/StartingGoldTraitButton");
        Require(Text("Trait", "TraitDetailsPanel/TraitLevel") == "(2 / 5)" && !Upgrade().interactable,
            "Restored trait and wallet displayed by UI");

        // 최대 레벨·중복 구독 검사는 별도의 새 저장 슬롯에서 진행합니다.
        _saveManager.ConfigureDirectory(Path.GetFullPath(".utmp/OutGameSetupValidation/Saves-" + Guid.NewGuid().ToString("N")));
        Require(_bootstrap.Initialize(), "Initialize fresh isolated save");
        Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.StartingGold) == 0, "New save starts from defaults");
        Click("Altar", "Navigation/TraitsButton");
        Purchase("StartingGoldTraitButton");
        Require(_wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1, "Reinitialize has no duplicate listeners");
        for (int i = 0; i < 4; i++) Click("Trait", "TraitDetailsPanel/UpgradeTraitButton");
        Require(_traits.GetLevel(TraitId.StartingGold) == 5 && _wallet.Balance == 0, "Max trait level five");
        Require(!Upgrade().interactable && Text("Trait", "TraitDetailsPanel/UpgradeTraitButton/Label") == "최대 레벨", "Max level label and disabled button");
        Click("Trait", "TraitDetailsPanel/UpgradeTraitButton");
        Require(_traits.GetLevel(TraitId.StartingGold) == 5, "Cannot purchase above max");
        Render("08-trait-max");
        Report.Add("Persistence: UI purchases survive reinitialize; run selections reset; isolated new save has no doubled listeners; max trait label passed.");
    }

    private static void Purchase(string node)
    {
        Click("Trait", "TraitTree/" + node);
        Require(Upgrade().interactable, "Purchase button enabled for " + node);
        Click("Trait", "TraitDetailsPanel/UpgradeTraitButton");
    }

    private static UnityEngine.UI.Button Upgrade() { return UI("Trait", "TraitDetailsPanel/UpgradeTraitButton").GetComponent<UnityEngine.UI.Button>(); }
    private static GameObject Panel(string name) { return _root.transform.Find("OutGame" + name + "Panel").gameObject; }
    private static GameObject UI(string panel, string path) { return Panel(panel).transform.Find(path).gameObject; }
    private static string Text(string panel, string path) { return UI(panel, path).GetComponent<TMP_Text>().text; }

    private static void Click(string panel, string path, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
    {
        GameObject go = UI(panel, path);
        Canvas.ForceUpdateCanvases();
        _camera.Render();
        RectTransform rect = go.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(_camera, rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(_events) { button = button, position = point };
        List<RaycastResult> hits = new List<RaycastResult>();
        _root.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject == go, "Clickable raycast: " + path);
        ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void PreparePreview()
    {
        Require(Camera.main != null, "Saved scene has Main Camera");
        Camera.main.enabled = false;
        UnityEngine.UI.CanvasScaler scaler = _root.GetComponent<UnityEngine.UI.CanvasScaler>();
        Require(scaler.screenMatchMode == UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand, "Canvas fits narrow and wide screens");
        Vector2[] sizes = { new Vector2(1280, 720), new Vector2(1920, 1080) };
        for (int i = 0; i < sizes.Length; i++)
        {
            float scale = Mathf.Min(sizes[i].x / scaler.referenceResolution.x, sizes[i].y / scaler.referenceResolution.y);
            Require(1020 * scale <= sizes[i].x && 882 * scale <= sizes[i].y, "Wireframe fits reference screen " + sizes[i]);
        }
        _camera = new GameObject("ValidationCamera", typeof(Camera)).GetComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 450;
        _camera.transform.position = new Vector3(0, 0, -10);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.white;
        _target = new RenderTexture(1280, 900, 24);
        _target.Create();
        _camera.targetTexture = _target;
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _camera;
        canvas.planeDistance = 1;
        _root.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
    }

    private static void Render(string name)
    {
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in _root.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            if (text.isTextOverflowing)
                LayoutIssues.Add(name + ": " + text.transform.parent.name + "/" + text.name + " preferred=" + text.preferredWidth + "x" + text.preferredHeight + " rect=" + text.rectTransform.rect.size);
            string glyphText = text.text.Replace("\n", "").Replace("\r", "");
            Require(text.font.HasCharacters(glyphText, out uint[] missingCharacters, true, true), "Korean glyphs available: " + text.name);
        }
        Canvas.ForceUpdateCanvases();
        _camera.Render();
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = _target;
        Texture2D screenshot = new Texture2D(1280, 900, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 900), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(".utmp/OutGameSetupValidation/" + name + ".png", screenshot.EncodeToPNG());
        RenderTexture.active = old;
        Object.DestroyImmediate(screenshot);
    }

    private static void Require(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException("Validation failed: " + message);
    }
}
