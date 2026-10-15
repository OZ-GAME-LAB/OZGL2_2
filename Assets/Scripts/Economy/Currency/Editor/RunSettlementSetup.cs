#if UNITY_EDITOR
using System;
using System.Linq;
using Game.UI.InGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>기존 결산 화면의 점수 자리와 공용 점수표만 연결합니다.</summary>
public static class RunSettlementSetup
{
    public const string SettingsPath = "Assets/Data/Economy/S.O/RunScoreSettings.asset";

    public static RunScoreSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RunScoreSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RunScoreSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        return settings;
    }

    public static void Configure(RunResultView view, GameObject frame)
    {
        const string summary = "Window/SummaryBox/";
        Set(view, "_progressScore", TextAt(frame, summary + "ProgressColumn/ProgressScore"));
        Set(view, "_artifactScore", TextAt(frame, summary + "ProgressColumn/ArtifactTotalValue"));
        Set(view, "_totemMultiplier", TextAt(frame, summary + "TotemColumn/TotalMultiplierValue"));
        Set(view, "_totalScore", TextAt(frame, summary + "TotalColumn/TotalScoreValue"));
        ConfigureLabel(frame);
    }

    private static TMP_Text TextAt(GameObject root, string path)
    {
        var text = root.transform.Find(path)?.GetComponent<TMP_Text>();
        if (text == null) throw new InvalidOperationException("기존 점수 텍스트가 없습니다: " + path);
        return text;
    }

    private static void ConfigureLabel(GameObject frame)
    {
        var label = TextAt(frame, "Window/SummaryBox/TotalColumn/TotalScoreLabel");
        // 비활성 상태에서 직렬화 문자열만 갱신해 원본 동적 폰트 캐시를 건드리지 않습니다.
        var serialized = new SerializedObject(label);
        serialized.FindProperty("m_text").stringValue = "토템 적용 점수";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Record(label);
    }

    public static void ConfigureRoot(GameObject root, RunScoreSettings settings)
    {
        foreach (var manager in root.GetComponentsInChildren<RunRewardManager>(true))
            Set(manager, "_settings", settings);
        foreach (var view in root.GetComponentsInChildren<RunResultView>(true))
        {
            var frame = view.transform.Find("RunSettlementFrame");
            if (frame != null) Configure(view, frame.gameObject);
        }
        if (root.name == "RunSettlementFrame") ConfigureLabel(root);
    }

    // 명시한 프리팹만 수정하며 화면이나 계층을 다시 만들지 않습니다.
    public static void ConfigurePrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        bool active = root.activeSelf;
        root.SetActive(false);
        try
        {
            ConfigureRoot(root, GetOrCreateSettings());
            root.SetActive(active);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Game/UI/InGame/Connect Settlement Scores")]
    public static void ConfigureCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        var settings = GetOrCreateSettings();
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects()) ConfigureRoot(root, settings);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Record(target);
    }

    private static void Record(Object target)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
#endif
