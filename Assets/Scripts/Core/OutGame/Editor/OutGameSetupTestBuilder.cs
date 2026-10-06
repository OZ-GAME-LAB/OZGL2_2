using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

// 에디터에서만 실행됩니다. Play 중 UI를 생성하는 스크립트가 아닙니다.
public static class OutGameSetupTestBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Core/OutGame/OutGameSetup.prefab";
    public const string ScenePath = "Assets/Scenes/Test/OutGameSetupTest.unity";
    public const string CatalogPath = "Assets/Data/OutGame/Altar/OutGameTestAltarCatalog.asset";
    private const string PrefabRoot = "Assets/Prefabs/Core/OutGame/";
    private static TMP_FontAsset _font;

    [MenuItem("Tools/OutGame/Create Setup Test Assets")]
    public static void Create()
    {
        // 재실행으로 사용자가 수정한 테스트 프리팹이나 씬을 덮어쓰지 않습니다.
        if (File.Exists(PrefabPath) || File.Exists(ScenePath) || File.Exists(CatalogPath))
            throw new InvalidOperationException("OutGame 테스트 에셋이 이미 있습니다. 기존 에셋을 사용하거나 먼저 별도 경로로 보관해주세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Data/UI/Tests/OutGameWireframeFont.asset");
        Require(_font != null, "OutGameWireframeFont가 없습니다.");
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AltarCatalog catalog = ScriptableObject.CreateInstance<AltarCatalog>();
        SetObjects(catalog, "_altars", new Object[] {
            Altar("Abundance"), Altar("Conquest"), Altar("Arcane"), Altar("Guardian") });
        AssetDatabase.CreateAsset(catalog, CatalogPath);

        GameObject root = new GameObject("OutGameSetup", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.layer = 5;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.UI.CanvasScaler scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 900);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;
        UnityEngine.UI.Image background = root.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color32(239, 239, 237, 255);
        background.raycastTarget = false;
        TMP_Text phase = Text(root.transform, "SetupPhaseTitle", "세팅 Phase", 28);
        Rect(phase.rectTransform, new Vector2(0, 417), new Vector2(1020, 48), true);

        GameObject altarPanel = Instance(PrefabRoot + "Altar/OutGameAltarPanel.prefab", root.transform);
        GameObject traitPanel = Instance(PrefabRoot + "Trait/OutGameTraitPanel.prefab", root.transform);
        GameObject totemPanel = Instance(PrefabRoot + "Totem/OutGameTotemPanel.prefab", root.transform);
        OutGameAltarView altarView = ConfigureAltar(altarPanel);
        OutGameTraitView traitView = ConfigureTraits(traitPanel);
        OutGameTotemView totemView = ConfigureTotems(totemPanel);

        GameObject systems = new GameObject("Systems");
        systems.transform.SetParent(root.transform, false);
        PersistentCurrencyManager wallet = systems.AddComponent<PersistentCurrencyManager>();
        SaveManager saveManager = systems.AddComponent<SaveManager>();
        OutGameSaveCoordinator persistentSaveCoordinator = systems.AddComponent<OutGameSaveCoordinator>();
        OutGameAltarController altar = systems.AddComponent<OutGameAltarController>();
        OutGameTraitController trait = systems.AddComponent<OutGameTraitController>();
        OutGameTotemController totem = systems.AddComponent<OutGameTotemController>();
        OutGameStartController startController = systems.AddComponent<OutGameStartController>();
        OutGameUIController uiController = systems.AddComponent<OutGameUIController>();
        OutGameBootstrap bootstrap = systems.AddComponent<OutGameBootstrap>();
        Set(altar, "_catalog", catalog);
        SetObjects(trait, "_traitDatas", Assets<TraitData>("Assets/Data/OutGame/Trait"));
        SetObjects(totem, "_totemDatas", Assets<TotemData>("Assets/Data/OutGame/Totem"));
        Set(uiController, "_altarView", altarView);
        Set(uiController, "_traitView", traitView);
        Set(uiController, "_totemView", totemView);
        Set(bootstrap, "_wallet", wallet);
        Set(bootstrap, "_saveManager", saveManager);
        Set(bootstrap, "_persistentSaveCoordinator", persistentSaveCoordinator);
        Set(bootstrap, "_altarSelector", altar);
        Set(bootstrap, "_traitSelector", trait);
        Set(bootstrap, "_totemSelector", totem);
        Set(bootstrap, "_controller", startController);
        Set(bootstrap, "_uiController", uiController);
        traitPanel.SetActive(false);
        totemPanel.SetActive(false);
        RecordOverrides(root);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
        Require(success && saved != null, "OutGameSetup 프리팹 저장 실패");
        Object.DestroyImmediate(root);
        PrefabUtility.InstantiatePrefab(saved);
        GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        Camera camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(239, 239, 237, 255);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("OUTGAME_SETUP_CREATED: " + ScenePath);
    }

    private static OutGameAltarView ConfigureAltar(GameObject panel)
    {
        OutGameAltarView view = panel.AddComponent<OutGameAltarView>();
        AltarId[] ids = { AltarId.Abundance, AltarId.Conquest, AltarId.Arcane, AltarId.Guardian };
        SerializedObject data = new SerializedObject(view);
        SerializedProperty slots = data.FindProperty("_slots");
        slots.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            Transform button = Find(panel, "AltarSelection/AltarButton0" + (i + 1));
            OutGameChoiceButton choice = Choice(button.gameObject);
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Id").intValue = (int)ids[i];
            slot.FindPropertyRelative("Button").objectReferenceValue = choice;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        Find(panel, "AltarSelection/AltarButton05").gameObject.SetActive(false);
        TMP_Text description = Label(panel, "AltarInfoPanel/AltarInfoText");
        Rect(description.rectTransform, new Vector2(24, -18), new Vector2(834, 168));
        description.fontSize = 25;
        description.textWrappingMode = TextWrappingModes.Normal;
        Set(view, "_description", description);
        UnityEngine.UI.Button previous = Button(panel, "Navigation/PreviousButton");
        previous.interactable = false;
        Set(view, "_previousButton", previous);
        Set(view, "_traitButton", Button(panel, "Navigation/TraitsButton"));
        Set(view, "_nextButton", Button(panel, "Navigation/NextButton"));
        return view;
    }

    private static OutGameTraitView ConfigureTraits(GameObject panel)
    {
        OutGameTraitView view = panel.AddComponent<OutGameTraitView>();
        string[] names = { "StartingGoldTraitButton", "ProductionTraitButton", "WaveTraitButton", "KillGoldTraitButton",
            "MiddleRootTraitButton", "MiddleTraitButton", "RightRootTraitButton", "RightTraitButton01", "RightTraitButton02" };
        TraitId[] ids = { TraitId.StartingGold, TraitId.Production, TraitId.WaveReward, TraitId.KillGold,
            TraitId.MaxHealth, TraitId.Defense, TraitId.AttackPower, TraitId.AttackSpeed, TraitId.BloodstoneReward };
        SerializedObject data = new SerializedObject(view);
        SerializedProperty slots = data.FindProperty("_slots");
        slots.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            OutGameChoiceButton choice = Choice(Find(panel, "TraitTree/" + names[i]).gameObject);
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Id").intValue = (int)ids[i];
            slot.FindPropertyRelative("Button").objectReferenceValue = choice;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        Transform details = Find(panel, "TraitDetailsPanel");
        TMP_Text title = Label(panel, "TraitDetailsPanel/TraitTitle");
        Rect(title.rectTransform, new Vector2(15, -108), new Vector2(237, 48));
        title.fontSize = 27;
        TMP_Text level = Text(details, "TraitLevel", "(0 / 5)", 24);
        Rect(level.rectTransform, new Vector2(15, -157), new Vector2(237, 38));
        TMP_Text description = Label(panel, "TraitDetailsPanel/TraitDescription");
        Rect(description.rectTransform, new Vector2(18, -197), new Vector2(231, 228));
        description.fontSize = 17;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        Transform placeholder = Find(panel, "TraitDetailsPanel/TraitIconPlaceholder");
        Rect((RectTransform)placeholder, new Vector2(94, -18), new Vector2(80, 80));
        GameObject iconObject = new GameObject("TraitIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        iconObject.layer = 5;
        iconObject.transform.SetParent(placeholder, false);
        Rect((RectTransform)iconObject.transform, new Vector2(4, -4), new Vector2(72, 72));
        UnityEngine.UI.Image icon = iconObject.GetComponent<UnityEngine.UI.Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        UnityEngine.UI.Button upgrade = Button(panel, "TraitDetailsPanel/UpgradeTraitButton");
        Rect((RectTransform)upgrade.transform, new Vector2(33, -429), new Vector2(201, 96));
        TMP_Text upgradeLabel = upgrade.transform.Find("Label").GetComponent<TMP_Text>();
        upgradeLabel.fontSize = 24;
        upgradeLabel.richText = true;
        TMP_Text wallet = Text(panel.transform, "BloodstoneBalance", "혈석: 500", 26);
        Rect(wallet.rectTransform, new Vector2(738, -26), new Vector2(258, 44));
        Set(view, "_title", title);
        Set(view, "_level", level);
        Set(view, "_description", description);
        Set(view, "_walletLabel", wallet);
        Set(view, "_upgradeLabel", upgradeLabel);
        Set(view, "_icon", icon);
        Set(view, "_upgradeButton", upgrade);
        Set(view, "_backButton", Button(panel, "OutGameNavigationButton"));
        return view;
    }

    private static OutGameTotemView ConfigureTotems(GameObject panel)
    {
        OutGameTotemView view = panel.AddComponent<OutGameTotemView>();
        ((RectTransform)panel.transform).sizeDelta = new Vector2(1020, 756);
        string[] names = { "TotemColumn01", "TotemColumn02Button", "TotemColumn03", "TotemColumn04", "TotemColumn05Button", "TotemColumn06" };
        Object[] columns = new Object[6];
        for (int i = 0; i < names.Length; i++)
        {
            Transform column = Find(panel, "TotemSelection/" + names[i]);
            if (i == 0)
            {
                ((RectTransform)column).sizeDelta = new Vector2(66, 279);
                for (int step = 1; step <= 3; step++)
                    ((RectTransform)column.Find("Level" + step + "Button")).sizeDelta = new Vector2(66, 93);
            }
            OutGameTotemColumn viewColumn = column.gameObject.AddComponent<OutGameTotemColumn>();
            List<Object> steps = new List<Object>();
            if (i == 1 || i == 4) steps.Add(Step(column.gameObject));
            else
            {
                // 기존 프리팹의 Level3Button이 아래에 있으므로 아래→위 순서로 연결합니다.
                steps.Add(Step(column.Find("Level3Button").gameObject));
                steps.Add(Step(column.Find("Level2Button").gameObject));
                steps.Add(Step(column.Find("Level1Button").gameObject));
            }
            SetObjects(viewColumn, "_steps", steps.ToArray());
            columns[i] = viewColumn;
        }
        SetObjects(view, "_columns", columns);
        TMP_Text description = Label(panel, "TotemInfoPanel/TotemInfoText");
        description.fontSize = 21;
        description.textWrappingMode = TextWrappingModes.Normal;
        Set(view, "_description", description);
        TMP_Text bonus = Label(panel, "TotemSelection/CumulativeBonus/BonusText");
        bonus.fontSize = 22;
        Set(view, "_bonusLabel", bonus);
        Set(view, "_leftButton", Button(panel, "TotemSelection/PreviousTotemsButton"));
        Set(view, "_rightButton", Button(panel, "TotemSelection/NextTotemsButton"));
        UnityEngine.UI.Button back = Navigation(panel.transform, "PreviousButton", "이전", new Vector2(0, -675), true);
        UnityEngine.UI.Button start = Navigation(panel.transform, "StartButton", "시작", new Vector2(876, -675), false);
        Set(view, "_backButton", back);
        Set(view, "_startButton", start);
        return view;
    }

    private static OutGameChoiceButton Choice(GameObject gameObject)
    {
        OutGameChoiceButton choice = gameObject.AddComponent<OutGameChoiceButton>();
        ConfigureChoice(gameObject, choice, 20);
        return choice;
    }

    private static OutGameTotemStepButton Step(GameObject gameObject)
    {
        OutGameTotemStepButton step = gameObject.AddComponent<OutGameTotemStepButton>();
        ConfigureChoice(gameObject, step, 17);
        return step;
    }

    private static void ConfigureChoice(GameObject gameObject, Object component, float fontSize)
    {
        UnityEngine.UI.Button button = gameObject.GetComponent<UnityEngine.UI.Button>();
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        TMP_Text label = gameObject.transform.Find("Label").GetComponent<TMP_Text>();
        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = false;
        Set(component, "_button", button);
        Set(component, "_background", gameObject.GetComponent<UnityEngine.UI.Image>());
        Set(component, "_label", label);
    }

    private static UnityEngine.UI.Button Navigation(Transform parent, string name, string label, Vector2 position, bool previous)
    {
        GameObject instance = Instance(PrefabRoot + "Common/OutGameNavigationButton.prefab", parent);
        instance.name = name;
        Rect((RectTransform)instance.transform, position, new Vector2(144, 81));
        TMP_Text text = instance.transform.Find("Label").GetComponent<TMP_Text>();
        text.text = label;
        if (previous)
        {
            Transform icon = instance.transform.Find("Icon");
            icon.GetComponent<UnityEngine.UI.Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/ExternalAssets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/Sprites/Component/Icon_PictoIcons/64/function_icon_arrow_back.png");
            RectTransform iconRect = (RectTransform)icon;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f);
            iconRect.anchoredPosition = new Vector2(24, 0);
            text.rectTransform.offsetMin = new Vector2(43, 3);
            text.rectTransform.offsetMax = new Vector2(-6, -3);
        }
        return instance.GetComponent<UnityEngine.UI.Button>();
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSharedMaterial = _font.material;
        text.text = value;
        text.fontSize = size;
        text.color = new Color32(43, 43, 43, 255);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Rect(RectTransform rect, Vector2 position, Vector2 size, bool center = false)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = center ? new Vector2(.5f, .5f) : new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static GameObject Instance(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(prefab != null, "프리팹이 없습니다: " + path);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    private static AltarData Altar(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AltarData>("Assets/Data/OutGame/Altar/Altar_" + name + ".asset");
    }

    private static Object[] Assets<T>(string path) where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { path });
        Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
        Object[] values = new Object[guids.Length];
        for (int i = 0; i < guids.Length; i++) values[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return values;
    }

    private static Transform Find(GameObject root, string path)
    {
        Transform found = root.transform.Find(path);
        Require(found != null, "UI 경로가 없습니다: " + path);
        return found;
    }

    private static TMP_Text Label(GameObject root, string path) { return Find(root, path).GetComponent<TMP_Text>(); }
    private static UnityEngine.UI.Button Button(GameObject root, string path) { return Find(root, path).GetComponent<UnityEngine.UI.Button>(); }

    public static void Set(Object target, string field, Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        Require(property != null, target.GetType().Name + "." + field + " 필드가 없습니다.");
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjects(Object target, string field, Object[] values)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        Require(property != null, target.GetType().Name + "." + field + " 필드가 없습니다.");
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RecordOverrides(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(child)) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            foreach (Component component in child.GetComponents<Component>())
                if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
