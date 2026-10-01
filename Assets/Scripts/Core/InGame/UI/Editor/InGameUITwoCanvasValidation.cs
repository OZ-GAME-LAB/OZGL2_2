#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>UIId를 기준으로 Canvas 이전 전후를 비교한다. 게임 상태는 만들지 않는 외형 검사다.</summary>
    [InitializeOnLoad]
    public static class InGameUITwoCanvasValidation
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const string PlayFontsKey = "InGameUI.TwoCanvas.IsolatePlayFonts";
        private static readonly List<Object> PlayFonts = new List<Object>();
        private static readonly UIId[] States = { UIId.Hud, UIId.BuildingCatalog, UIId.BuildingInfo,
            UIId.WaveReward, UIId.ArtifactReward, UIId.RunDecision, UIId.RunResult, UIId.Message };
        public static string LastResult { get; private set; }

        static InGameUITwoCanvasValidation()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                for (int i = PlayFonts.Count - 1; i >= 0; i--) if (PlayFonts[i] != null) Object.DestroyImmediate(PlayFonts[i]);
                PlayFonts.Clear();
            };
        }

        public static void ArmPlayFontIsolation(bool enabled = true) => SessionState.SetBool(PlayFontsKey, enabled);

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || !SessionState.GetBool(PlayFontsKey, false)) return;
            InGameUIValidation.PrepareFontsForRoots(scene.GetRootGameObjects(), PlayFonts);
        }

        public static string Capture(string label, GameObject source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture in Edit Mode.");
            if (label != "before" && label != "after") throw new ArgumentException("Use before or after.");
            Type owner = typeof(InGameUIValidation);
            Type pairType = owner.GetNestedType("PreviewPair", BindingFlags.NonPublic);
            object pair = Activator.CreateInstance(pairType, source, source);
            var report = new List<string> { "Source: " + source.scene.path + " / " + source.name,
                "Serialized UI states, isolated cloned fonts, fixed preview background. Gameplay/input tested separately." };
            try
            {
                var root = (GameObject)pairType.GetProperty("Old").GetValue(pair);
                var other = (GameObject)pairType.GetProperty("New").GetValue(pair);
                Scene scene = (Scene)pairType.GetProperty("Scene").GetValue(pair);
                Invoke("DisableGameplay", root); Invoke("DisableGameplay", other); Invoke("PrepareFonts", pair);
                other.SetActive(false);
                Dictionary<UIId, UIScreen> screens = FindScreens(root);
                var flags = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.activeSelf);
                var cameraObject = new GameObject("UI snapshot camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .07f, .09f, 1);
                camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                camera.allowHDR = false; camera.allowMSAA = false; camera.transform.position = new Vector3(0, 0, -10);
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    Invoke("ConfigureCanvases", root, camera, size.x, size.y);
                    foreach (UIId id in States)
                    {
                        root.SetActive(false);
                        foreach (var flag in flags) if (flag.Key != root.transform) flag.Key.gameObject.SetActive(flag.Value);
                        foreach (var screen in screens) if (screen.Key != UIId.Hud) screen.Value.Root.SetActive(false);
                        Activate(screens[UIId.Hud].Root.transform, root.transform);
                        if (id != UIId.Hud) Activate(screens[id].Root.transform, root.transform);
                        root.SetActive(true);
                        string stem = id + "-" + size.x + "x" + size.y;
                        camera.rect = new Rect(0, 0, 1, 1);
                        Color32[] pixels = (Color32[])Invoke("Capture", root, camera, size.x, size.y, Output(stem + "-" + label + ".png"));
                        // Capture restores the camera target. Keep the same viewport while projecting UI corners.
                        camera.pixelRect = new Rect(0, 0, size.x, size.y);
                        string[] snapshot = Snapshot(screens, camera);
                        File.WriteAllLines(Output(stem + "-" + label + ".txt"), snapshot);
                        if (label == "after")
                        {
                            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            try
                            {
                                texture.LoadImage(File.ReadAllBytes(Output(stem + "-before.png")));
                                Color32[] baseline = texture.GetPixels32();
                                if (baseline.Length != pixels.Length) throw new InvalidOperationException("Different snapshot sizes.");
                                int differences = pixels.Where((pixel, index) => !pixel.Equals(baseline[index])).Count();
                                string[] previous = File.ReadAllLines(Output(stem + "-before.txt"));
                                string[] removed = previous.Except(snapshot).Select(line => "BEFORE " + line).ToArray();
                                string[] added = snapshot.Except(previous).Select(line => "AFTER " + line).ToArray();
                                File.WriteAllLines(Output(stem + "-differences.txt"), removed.Concat(added));
                                report.Add(stem + ": " + differences + " pixels differ; " + removed.Length + " changed/removed, " + added.Length + " changed/added visual rows.");
                            }
                            finally { Object.DestroyImmediate(texture); }
                        }
                        else report.Add(stem + ": baseline captured, " + snapshot.Length + " visual rows.");
                    }
                }
                LastResult = string.Join("\n", report);
                File.WriteAllText(Output(label + "-visuals.txt"), LastResult);
                return LastResult;
            }
            finally { ((IDisposable)pair).Dispose(); }
        }

        private static Dictionary<UIId, UIScreen> FindScreens(GameObject root)
        {
            var result = new Dictionary<UIId, UIScreen>();
            var manager = root.GetComponentInChildren<InGameUIManager>(true);
            using (var data = new SerializedObject(manager))
            {
                SerializedProperty old = data.FindProperty("_screens");
                if (old != null)
                    for (int i = 0; i < old.arraySize; i++)
                    {
                        SerializedProperty row = old.GetArrayElementAtIndex(i);
                        var screen = row.FindPropertyRelative("Instance").objectReferenceValue as UIScreen;
                        if (screen != null) result.Add((UIId)row.FindPropertyRelative("Id").intValue, screen);
                    }
                else
                    foreach (UIScreen screen in root.GetComponentsInChildren<UIScreen>(true))
                    {
                        using (var screenData = new SerializedObject(screen))
                        {
                            SerializedProperty id = screenData.FindProperty("_id");
                            if (id != null) result.Add((UIId)id.intValue, screen);
                        }
                    }
            }
            return result;
        }

        private static string[] Snapshot(Dictionary<UIId, UIScreen> screens, Camera camera)
        {
            var rows = new List<string>();
            var roots = screens.Values.Select(s => s.Root.transform).ToHashSet();
            foreach (var entry in screens.OrderBy(pair => pair.Key))
            {
                if (!States.Contains(entry.Key)) continue;
                Transform root = entry.Value.Root.transform;
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    if (!graphic.gameObject.activeInHierarchy) continue;
                    Transform owner = graphic.transform;
                    while (owner != root && !roots.Contains(owner)) owner = owner.parent;
                    if (owner != root) continue;
                    string path = AnimationUtility.CalculateTransformPath(graphic.transform, root);
                    var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
                    string rect = string.Join(",", corners.Select(v => RectTransformUtility.WorldToScreenPoint(camera, v))
                        .Select(v => v.x.ToString("F2", CultureInfo.InvariantCulture) + ":" + v.y.ToString("F2", CultureInfo.InvariantCulture)));
                    string text = graphic is TMP_Text label ? "|text=" + (label.text ?? "").Replace("\r", "").Replace("\n", "\\n") +
                        "|size=" + label.fontSize.ToString("R", CultureInfo.InvariantCulture) + "|align=" + label.alignment : "";
                    string sprite = graphic is Image image ? "|sprite=" + AssetDatabase.GetAssetPath(image.sprite) + "|type=" + image.type : "";
                    rows.Add(entry.Key + "/" + path + "|" + graphic.GetType().Name + "|rect=" + rect + "|color=" +
                        ColorUtility.ToHtmlStringRGBA(graphic.color) + "|raycast=" + graphic.raycastTarget + text + sprite);
                }
            }
            return rows.OrderBy(row => row, StringComparer.Ordinal).ToArray();
        }

        private static void Activate(Transform target, Transform root)
        {
            while (target != root) { target.gameObject.SetActive(true); target = target.parent; }
        }
        private static object Invoke(string name, params object[] args) => typeof(InGameUIValidation).GetMethod(name, StaticPrivate).Invoke(null, args);
        private static string Output(string name)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PersonalDocs/UIValidation/TwoCanvas"));
            Directory.CreateDirectory(folder); return Path.Combine(folder, name);
        }
    }
}
#endif
