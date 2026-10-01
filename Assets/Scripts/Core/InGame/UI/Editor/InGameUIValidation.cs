#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>원본 씬을 변경하지 않고 임시 Preview Scene에서 공통 동작과 직렬화된 외형을 검사한다.</summary>
    public static class InGameUIValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static string LastResult { get; private set; } = "Not run";

        [MenuItem("Tools/InGame UI/Validate Common Screen Behaviour")]
        public static void RunCommonTests()
        {
            RequireEditMode();
            Scene preview = EditorSceneManager.NewPreviewScene();
            EventSystem previousEvents = EventSystem.current;
            EventSystem validationEvents = null;
            var checks = new List<string>();
            UIItemSlot slot = null;
            CloseUtility ownedClose = null;
            try
            {
                GameObject host = CreateObject("InGameUI common tests", null, preview);
                host.SetActive(false);
                validationEvents = CreateObject("Test EventSystem", host.transform, preview).AddComponent<EventSystem>();
                var manager = host.AddComponent<InGameUIManager>();
                UIScreen hud = CreateScreen("Hud", host.transform, preview);
                UIScreen alternateHud = CreateScreen("Alternate HUD", host.transform, preview);
                UIScreen parent = CreateScreen("Catalog", host.transform, preview);
                UIScreen other = CreateScreen("Info", host.transform, preview);
                UIScreen child = CreateScreen("Detail", host.transform, preview);
                UIScreen required = CreateScreen("Required reward", host.transform, preview);
                SetField(manager, "_screens", new[]
                {
                    Registration(UIId.Hud, hud, UILayer.Hud),
                    Registration(UIId.RunResult, alternateHud, UILayer.Hud),
                    Registration(UIId.BuildingCatalog, parent, UILayer.Popup),
                    Registration(UIId.BuildingInfo, other, UILayer.Popup),
                    Registration(UIId.Detail, child, UILayer.Popup),
                    Registration(UIId.ArtifactReward, required, UILayer.Modal, false)
                });
                host.SetActive(true);
                RebindLifecycle(validationEvents);
                EventSystem.current = validationEvents;
                Check(manager.InitializeScreens(), "screen registry initializes without game services", checks);
                Check(manager.ShowHud().IsValid && hud.IsVisible, "HUD opens independently", checks);

                UIHandle stale = manager.Open(UIId.BuildingCatalog);
                Check(stale.IsValid && parent.IsVisible, "optional popup opens", checks);
                Check(manager.Close(stale, UICloseReason.Completed), "current handle closes its screen", checks);
                UIHandle current = manager.Open(UIId.BuildingCatalog);
                Check(!current.Equals(stale) && !manager.Close(stale, UICloseReason.Completed) && parent.IsVisible,
                    "stale handle cannot close a later opening of the same screen", checks);
                UICloseReason? replacedReason = null;
                parent.Closed += (_, reason) => replacedReason = reason;
                manager.Replace(UIId.BuildingInfo);
                Check(!parent.IsVisible && other.IsVisible && replacedReason == UICloseReason.Replaced,
                    "Replace closes the previous popup with the replacement reason", checks);

                current = manager.Replace(UIId.BuildingCatalog);
                UIHandle childHandle = manager.Push(UIId.Detail);
                Check(parent.IsVisible && child.IsVisible && !InputEnabled(parent) && InputEnabled(child),
                    "Push preserves the parent and gives input to its child", checks);
                Check(manager.Close(childHandle, UICloseReason.UserCancel) && parent.IsVisible &&
                      parent.Handle.Equals(current) && !child.IsVisible && InputEnabled(parent),
                    "closing a pushed child restores its unchanged parent", checks);

                // Edit Mode에서는 일반 MonoBehaviour의 이벤트 연결을 명시적으로 재현한다.
                var closeObject = CreateObject("Owned close", parent.Root.transform, preview, typeof(RectTransform), typeof(Button));
                ownedClose = closeObject.AddComponent<CloseUtility>();
                SetField(ownedClose, "_button", closeObject.GetComponent<Button>());
                SetField(ownedClose, "_screen", parent);
                RebindLifecycle(ownedClose);
                childHandle = manager.Push(UIId.Detail);
                ownedClose.Close();
                Check(parent.IsVisible && child.IsVisible && child.Handle.Equals(childHandle),
                    "a covered owner's CloseUtility cannot close an unrelated top screen", checks);
                manager.Close(childHandle, UICloseReason.Completed);
                closeObject.GetComponent<Button>().onClick.Invoke();
                Check(!parent.IsVisible, "CloseUtility closes its own current screen", checks);

                UIHandle requiredHandle = manager.Open(UIId.ArtifactReward);
                Check(required.IsVisible && !manager.CloseTop() && required.IsVisible,
                    "CloseTop cannot dismiss a mandatory modal", checks);
                Check(!manager.Open(UIId.BuildingCatalog).IsValid && !parent.IsVisible,
                    "a mandatory modal blocks an optional Open request", checks);
                Check(manager.Close(requiredHandle, UICloseReason.Completed) && !required.IsVisible,
                    "the owning presenter can complete a mandatory modal", checks);

                var slotObject = CreateObject("Reusable slot", host.transform, preview, typeof(RectTransform), typeof(Button));
                slot = slotObject.AddComponent<UIItemSlot>();
                Button button = slotObject.GetComponent<Button>();
                SetField(slot, "_button", button);
                RebindLifecycle(slot);
                int first = 0, second = 0;
                slot.Bind(null, "First", "", false, true, () => first++);
                button.onClick.Invoke();
                slot.Bind(null, "Second", "", false, true, () => second++);
                button.onClick.Invoke();
                Check(first == 1 && second == 1, "slot rebind replaces the previous data callback", checks);
                RebindLifecycle(slot);
                button.onClick.Invoke();
                Check(second == 2, "slot disable/enable does not duplicate its click callback", checks);
                slot.Unbind();
                button.onClick.Invoke();
                Check(second == 2, "unbound slot no longer dispatches a stale selection", checks);
                slot.Bind(null, "Disabled", "", false, false, () => second++);
                button.onClick.Invoke();
                Check(second == 2, "a non-interactable slot does not dispatch selection", checks);
                Check(hud.IsVisible, "popup transitions preserve the HUD", checks);
                manager.Open(UIId.BuildingCatalog);
                manager.ShowHud(UIId.RunResult);
                Check(alternateHud.IsVisible && !hud.IsVisible && !parent.IsVisible && !manager.TopHandle.IsValid,
                    "replacing the HUD closes popups from the previous UI context", checks);
                manager.ShowHud();
                Check(hud.IsVisible && !alternateHud.IsVisible,
                    "the previous HUD can be shown again without creating another instance", checks);

                LastResult = "PASS: " + checks.Count + " isolated common API checks.\n" + string.Join("\n", checks) +
                    "\nEdit Mode callbacks were invoked explicitly. Physical keyboard, pointer routing and gameplay are not covered.\n";
                File.WriteAllText(Output("common-tests.txt"), LastResult);
                Debug.Log("[InGameUIValidation] " + LastResult);
            }
            catch (Exception error)
            {
                LastResult = "FAIL after " + checks.Count + " checks.\n" + error;
                File.WriteAllText(Output("common-tests.txt"), LastResult);
                throw;
            }
            finally
            {
                InvokeLifecycle(slot, "OnDisable");
                InvokeLifecycle(ownedClose, "OnDisable");
                InvokeLifecycle(validationEvents, "OnDisable");
                EditorSceneManager.ClosePreviewScene(preview);
                if (previousEvents != null) EventSystem.current = previousEvents;
            }
        }

        /// <summary>oldMainUI에는 Test 씬의 실제 인스턴스를 전달해야 씬 override까지 비교할 수 있다.</summary>
        public static string CompareVisuals(GameObject oldMainUI, GameObject newPrefab)
        {
            RequireEditMode();
            using (var pair = new PreviewPair(oldMainUI, newPrefab))
            {
                var oldObjects = IndexHierarchy(pair.Old.transform);
                var newObjects = IndexHierarchy(pair.New.transform);
                var differences = new List<string>();
                int properties = 0;
                foreach (var entry in oldObjects)
                {
                    if (!newObjects.TryGetValue(entry.Key, out Transform target))
                    {
                        differences.Add(entry.Key + ": missing hierarchy object");
                        continue;
                    }
                    if (entry.Key != "." && entry.Value.gameObject.activeSelf != target.gameObject.activeSelf)
                        differences.Add(entry.Key + ": serialized activeSelf differs");
                    Dictionary<string, Component> oldComponents = VisualComponents(entry.Value);
                    Dictionary<string, Component> newComponents = VisualComponents(target);
                    foreach (var component in oldComponents)
                    {
                        string location = entry.Key + " / " + component.Key;
                        if (!newComponents.TryGetValue(component.Key, out Component other))
                        {
                            differences.Add(location + ": missing visual component");
                            continue;
                        }
                        CompareProperties(component.Value, other, pair.Old.transform, pair.New.transform,
                            location, differences, ref properties);
                    }
                    foreach (string key in newComponents.Keys.Except(oldComponents.Keys))
                        differences.Add(entry.Key + " / " + key + ": added visual component");
                }
                foreach (var entry in newObjects.Where(item => !oldObjects.ContainsKey(item.Key)))
                    differences.Add(entry.Key + ": added hierarchy object");

                LastResult = (differences.Count == 0 ? "PASS" : "DIFFERENCES: " + differences.Count) +
                    " — serialized visual comparison (" + properties + " properties).\n" +
                    "Source: " + SourceDescription(oldMainUI) + "\nCandidate: " + SourceDescription(newPrefab) + "\n" +
                    "Compares normalized relative paths, RectTransform, Canvas/Scaler, Graphic/TMP, layout, masks and Button style/navigation.\n" +
                    "Ignores added nonvisual behaviours, UnityEvent listeners, runtime caches, identity screen wrappers and the top-level active flag.\n" +
                    string.Join("\n", differences) + "\n";
                string path = Output("serialized-visuals.txt");
                File.WriteAllText(path, LastResult);
                Debug.Log("[InGameUIValidation] " + LastResult);
                return path;
            }
        }

        /// <summary>동일한 직렬화 데이터와 화면 활성 상태를 촬영한다. 실제 게임 진행 결과를 만들어내지 않는다.</summary>
        public static string CaptureVisualPairs(GameObject oldMainUI, GameObject newPrefab)
        {
            RequireEditMode();
            using (var pair = new PreviewPair(oldMainUI, newPrefab))
            {
                var oldObjects = IndexHierarchy(pair.Old.transform);
                var newObjects = IndexHierarchy(pair.New.transform);
                Dictionary<string, GameObject> roots = FindOldScreenRoots(pair.Old);
                var screenPaths = roots.ToDictionary(item => item.Key, item => RelativePath(item.Value.transform, pair.Old.transform));
                foreach (var entry in screenPaths)
                    if (!newObjects.ContainsKey(entry.Value))
                        throw new InvalidOperationException("Cannot match screen root: " + entry.Key + " / " + entry.Value);

                DisableGameplay(pair.Old);
                DisableGameplay(pair.New);
                PrepareFonts(pair);
                var baselineOld = oldObjects.ToDictionary(item => item.Key, item => item.Value.gameObject.activeSelf);
                var baselineNew = newObjects.ToDictionary(item => item.Key, item => item.Value.gameObject.activeSelf);
                GameObject cameraObject = CreateObject("UI snapshot camera", null, pair.Scene, typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = pair.Scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(pair.Scene);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.06f, .07f, .09f, 1);
                camera.orthographic = true;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.transform.position = new Vector3(0, 0, -10);

                var report = new StringBuilder();
                report.AppendLine("Serialized-state layout snapshots. No game services, reward grant, settlement formula or UI presenter was executed.");
                report.AppendLine("Source: " + SourceDescription(oldMainUI));
                report.AppendLine("Candidate: " + SourceDescription(newPrefab));
                report.AppendLine("Source text, sprites and flags are preserved. Only screen roots and their ancestors are activated for each named state.");
                report.AppendLine("Both snapshots share temporary copies of the source fonts, materials and atlas textures. Missing glyphs are generated only in those copies using the source font settings.");
                report.AppendLine("Background is a fixed preview color; gameplay camera, input, transition timing and runtime-populated content require separate validation.");
                string[] states = { "hud", "catalog", "building-info", "wave-reward", "artifact-reward", "decision", "result", "message" };
                foreach (Vector2Int resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    ConfigureCanvases(pair.Old, camera, resolution.x, resolution.y);
                    ConfigureCanvases(pair.New, camera, resolution.x, resolution.y);
                    foreach (string state in states)
                    {
                        ApplyVisualState(pair.Old, oldObjects, baselineOld, screenPaths, state);
                        pair.New.SetActive(false);
                        string stem = state + "-" + resolution.x + "x" + resolution.y;
                        Color32[] before = Capture(pair.Old, camera, resolution.x, resolution.y, Output(stem + "-old.png"));
                        pair.Old.SetActive(false);
                        ApplyVisualState(pair.New, newObjects, baselineNew, screenPaths, state);
                        Color32[] after = Capture(pair.New, camera, resolution.x, resolution.y, Output(stem + "-new.png"));
                        pair.New.SetActive(false);
                        int changed = 0;
                        for (int i = 0; i < before.Length; i++)
                            if (!before[i].Equals(after[i])) changed++;
                        report.AppendLine(stem + ": " + changed + " / " + before.Length + " pixels differ (exact RGBA comparison).");
                    }
                }
                LastResult = report.ToString();
                string path = Output("screenshots.txt");
                File.WriteAllText(path, LastResult);
                Debug.Log("[InGameUIValidation] " + LastResult);
                return path;
            }
        }

        private sealed class PreviewPair : IDisposable
        {
            public Scene Scene { get; }
            public GameObject Old { get; }
            public GameObject New { get; }
            public readonly List<Object> Assets = new List<Object>();
            public PreviewPair(GameObject oldMainUI, GameObject newPrefab)
            {
                if (oldMainUI == null || newPrefab == null) throw new ArgumentNullException("Both UI roots are required.");
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var host = CreateObject("Inactive UI comparison host", null, Scene);
                    host.SetActive(false);
                    Old = Object.Instantiate(oldMainUI, host.transform, false);
                    New = Object.Instantiate(newPrefab, host.transform, false);
                    Old.SetActive(false);
                    New.SetActive(false);
                    host.SetActive(true);
                }
                catch { EditorSceneManager.ClosePreviewScene(Scene); throw; }
            }
            public void Dispose()
            {
                // Only the preview scene and assets created by this helper are disposable.
                EditorSceneManager.ClosePreviewScene(Scene);
                for (int i = Assets.Count - 1; i >= 0; i--) if (Assets[i] != null) Object.DestroyImmediate(Assets[i]);
            }
        }

        private static void CompareProperties(Component before, Component after, Transform oldRoot, Transform newRoot,
            string location, List<string> differences, ref int count)
        {
            using (var oldData = new SerializedObject(before))
            using (var newData = new SerializedObject(after))
            {
                SerializedProperty property = oldData.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    // Object references are compared by normalized target, not by the clone's internal file/instance ID.
                    enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                    string path = property.propertyPath;
                    if (SkipProperty(path) || property.propertyType == SerializedPropertyType.Generic) continue;
                    SerializedProperty candidate = newData.FindProperty(path);
                    if (candidate == null)
                    {
                        differences.Add(location + "." + path + ": missing serialized property");
                        continue;
                    }
                    string left = PropertyValue(property, oldRoot), right = PropertyValue(candidate, newRoot);
                    count++;
                    if (left != right) differences.Add(location + "." + path + ": " + left + " -> " + right);
                }
            }
        }

        private static bool SkipProperty(string path)
        {
            string field = path.Split('.')[0];
            switch (field)
            {
                case "m_Script": case "m_GameObject": case "m_ObjectHideFlags": case "m_EditorHideFlags":
                case "m_CorrespondingSourceObject": case "m_PrefabInstance": case "m_PrefabAsset":
                case "m_Name": case "m_EditorClassIdentifier": case "m_Father": case "m_Children":
                case "m_RootOrder": case "m_LocalEulerAnglesHint": case "m_OnClick": case "m_OnCullStateChanged":
                case "m_OnValueChanged": case "m_fontMaterial": case "m_fontMaterials":
                case "m_hasFontAssetChanged": case "checkPaddingRequired": case "m_baseMaterial":
                    return true;
                default: return false;
            }
        }

        private static string PropertyValue(SerializedProperty property, Transform root)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference: return ReferenceKey(property.objectReferenceValue, root);
                case SerializedPropertyType.Integer: case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character: case SerializedPropertyType.ArraySize:
                    return property.longValue.ToString(Invariant);
                case SerializedPropertyType.Boolean: return property.boolValue.ToString();
                case SerializedPropertyType.Float: return property.doubleValue.ToString("R", Invariant);
                case SerializedPropertyType.String: return property.stringValue.Replace("\n", "\\n").Replace("\r", "\\r");
                case SerializedPropertyType.Enum: return property.intValue.ToString(Invariant);
                case SerializedPropertyType.Color: return property.colorValue.ToString("R");
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString("R");
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString("R");
                case SerializedPropertyType.Vector4: return property.vector4Value.ToString("R");
                case SerializedPropertyType.Rect: return property.rectValue.ToString("R");
                case SerializedPropertyType.Bounds: return property.boundsValue.ToString("R");
                case SerializedPropertyType.Quaternion: return property.quaternionValue.ToString("R");
                case SerializedPropertyType.Vector2Int: return property.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int: return property.vector3IntValue.ToString();
                default: return property.propertyType + ":" + Convert.ToString(property.boxedValue, Invariant);
            }
        }

        private static string ReferenceKey(Object value, Transform root)
        {
            if (value == null) return "null";
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId) && !string.IsNullOrEmpty(guid))
                return "asset:" + guid + ":" + localId;
            Transform target = value is GameObject go ? go.transform : value is Component component ? component.transform : null;
            if (target != null && (target == root || target.IsChildOf(root)))
                return RelativePath(target, root) + ":" + (value is Component part ? VisualKind(part) : "GameObject");
            return value.GetType().FullName + ":" + value.name + ":" + value.GetInstanceID();
        }

        private static Dictionary<string, Component> VisualComponents(Transform transform)
        {
            var result = new Dictionary<string, Component>();
            foreach (Component component in transform.GetComponents<Component>())
            {
                if (component == null || !(component is RectTransform || component is Canvas || component is CanvasRenderer ||
                    component is CanvasScaler || component is Graphic || component is Selectable || component is LayoutGroup ||
                    component is LayoutElement || component is ContentSizeFitter || component is AspectRatioFitter ||
                    component is Mask || component is RectMask2D || component is ScrollRect)) continue;
                string kind = VisualKind(component), key = kind;
                int index = 1;
                while (result.ContainsKey(key)) key = kind + "#" + index++;
                result.Add(key, component);
            }
            return result;
        }

        private static string VisualKind(Component component)
        {
            if (component is TMP_Text) return "TMP_Text";
            // A migrated procedural icon keeps its serialized symbol/color even if its class name changes.
            if (component is MaskableGraphic && !(component is Image) && !(component is RawImage) && !(component is Text))
                return "ProceduralGraphic";
            return component.GetType().Name;
        }

        private static Dictionary<string, Transform> IndexHierarchy(Transform root)
        {
            var result = new Dictionary<string, Transform>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform != root && IsScreenWrapper(transform)) continue;
                string path = RelativePath(transform, root);
                if (result.ContainsKey(path)) throw new InvalidOperationException("Ambiguous normalized UI path: " + path);
                result.Add(path, transform);
            }
            return result;
        }

        private static string RelativePath(Transform transform, Transform root)
        {
            if (transform == root) return ".";
            var names = new List<string>();
            while (transform != null && transform != root)
            {
                if (!IsScreenWrapper(transform))
                {
                    int sameNameIndex = 0, sameNameCount = 0;
                    if (transform.parent != null)
                        foreach (Transform sibling in transform.parent)
                            if (sibling.name == transform.name)
                            {
                                if (sibling.GetSiblingIndex() < transform.GetSiblingIndex()) sameNameIndex++;
                                sameNameCount++;
                            }
                    names.Add(transform.name + (sameNameCount > 1 ? "[" + sameNameIndex + "]" : ""));
                }
                transform = transform.parent;
            }
            if (transform != root) throw new InvalidOperationException("Reference is outside the compared UI root.");
            names.Reverse();
            return string.Join("/", names);
        }

        private static bool IsScreenWrapper(Transform transform)
        {
            UIScreen screen = transform.GetComponent<UIScreen>();
            if (screen == null || screen.Root == null || screen.Root.transform.parent != transform || transform.childCount != 1) return false;
            if (!(transform is RectTransform rect) || transform.GetComponents<Component>().Any(component =>
                !(component is Transform) && !(component is UIScreen))) return false;
            return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && rect.offsetMin == Vector2.zero &&
                   rect.offsetMax == Vector2.zero && rect.anchoredPosition3D.z == 0 && rect.localScale == Vector3.one &&
                   rect.localRotation == Quaternion.identity && rect.gameObject.activeSelf &&
                   (!(rect.parent is RectTransform parent) || rect.pivot == parent.pivot);
        }

        private static Dictionary<string, GameObject> FindOldScreenRoots(GameObject root)
        {
            Component hud = FindComponent(root, "GameUIController");
            Component catalog = FindComponent(root, "BuildingCatalogPanel");
            Component info = FindComponent(root, "BuildingInfoPanel");
            var result = new Dictionary<string, GameObject>
            {
                { "hud", hud.gameObject },
                { "catalog", ReadObject<GameObject>(ReadObject<Component>(catalog, "_popup"), "_root") },
                { "building-info", ReadObject<GameObject>(ReadObject<Component>(info, "_playerPopup"), "_root") },
                { "wave-reward", ReadObject<GameObject>(hud, "_waveRewardPanel") },
                { "artifact-reward", ReadObject<GameObject>(FindComponent(root, "ArtifactRewardPanel"), "_panelRoot") },
                { "decision", ReadObject<GameObject>(FindComponent(root, "CoreRunDecisionBinding"), "_panelRoot") },
                { "result", ReadObject<GameObject>(hud, "_runResultPanel") },
                { "message", ReadObject<GameObject>(hud, "_messagePanel") }
            };
            foreach (var item in result)
                if (item.Value == null) throw new InvalidOperationException("Missing source screen root: " + item.Key);
            return result;
        }

        private static void DisableGameplay(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is Graphic) continue;
                string space = behaviour.GetType().Namespace ?? "";
                if (!space.StartsWith("UnityEngine.UI", StringComparison.Ordinal) && !space.StartsWith("TMPro", StringComparison.Ordinal))
                    behaviour.enabled = false;
            }
        }

        private static void ApplyVisualState(GameObject root, Dictionary<string, Transform> objects,
            Dictionary<string, bool> baseline, Dictionary<string, string> screenPaths, string state)
        {
            root.SetActive(false);
            foreach (var item in baseline) if (item.Key != ".") objects[item.Key].gameObject.SetActive(item.Value);
            foreach (var item in screenPaths) if (item.Key != "hud") objects[item.Value].gameObject.SetActive(false);
            ActivateAncestors(objects[screenPaths["hud"]], root.transform);
            if (state != "hud") ActivateAncestors(objects[screenPaths[state]], root.transform);
            root.SetActive(true);
        }

        private static void ActivateAncestors(Transform transform, Transform root)
        {
            while (transform != root)
            {
                transform.gameObject.SetActive(true);
                transform = transform.parent;
                if (transform == null) throw new InvalidOperationException("Screen root is outside its UI clone.");
            }
        }

        private static void ConfigureCanvases(GameObject root, Camera camera, int width, int height)
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                var scaler = canvas.GetComponent<CanvasScaler>();
                float scale = scaler != null ? scaler.scaleFactor : canvas.scaleFactor;
                if (scaler != null)
                {
                    scaler.enabled = false;
                    if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    {
                        float x = width / scaler.referenceResolution.x, y = height / scaler.referenceResolution.y;
                        scale = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(x, y) :
                            scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(x, y) :
                            Mathf.Pow(2, Mathf.Lerp(Mathf.Log(x, 2), Mathf.Log(y, 2), scaler.matchWidthOrHeight));
                    }
                    else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPhysicalSize)
                        throw new NotSupportedException("Physical-size UI needs an explicit device DPI for comparable screenshots.");
                }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.scaleFactor = scale;
            }
        }

        private static Color32[] Capture(GameObject root, Camera camera, int width, int height, string path)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void PrepareFonts(PreviewPair pair)
        {
            var fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
            var textures = new Dictionary<Texture, Texture>();
            foreach (TMP_Text label in pair.Old.GetComponentsInChildren<TMP_Text>(true).Concat(pair.New.GetComponentsInChildren<TMP_Text>(true)))
            {
                TMP_FontAsset font = CopyFont(label.font != null ? label.font : TMP_Settings.defaultFontAsset, fonts, textures, pair.Assets);
                if (font == null) throw new InvalidOperationException("Snapshot text has no font: " + label.name);
                var fallbacks = new List<TMP_FontAsset>(font.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
                if (TMP_Settings.fallbackFontAssets != null)
                    foreach (TMP_FontAsset fallback in TMP_Settings.fallbackFontAssets)
                        if (fallback != null) fallbacks.Add(CopyFont(fallback, fonts, textures, pair.Assets));
                TMP_FontAsset defaultFont = CopyFont(TMP_Settings.defaultFontAsset, fonts, textures, pair.Assets);
                if (defaultFont != null && defaultFont != font) fallbacks.Add(defaultFont);
                font.fallbackFontAssetTable = fallbacks.Distinct().ToList();

                string content = label.richText ? Regex.Replace(label.text ?? "", "<[^>]*>", "") : label.text ?? "";
                for (int i = 0; i < content.Length; i++)
                {
                    if (char.IsWhiteSpace(content[i])) continue;
                    uint unicode = char.IsSurrogatePair(content, i) ? (uint)char.ConvertToUtf32(content, i++) : content[i];
                    if (!PrepareCharacter(font, unicode, new HashSet<TMP_FontAsset>()))
                        throw new InvalidOperationException("Source font and its copied fallbacks cannot render " + label.name +
                            " (U+" + unicode.ToString("X4") + "). Original fonts and atlases were not changed.");
                }

                Material material = label.fontSharedMaterial;
                label.font = font;
                if (material != null) label.fontSharedMaterial = CopyMaterial(material, textures, pair.Assets);
            }
        }

        private static TMP_FontAsset CopyFont(TMP_FontAsset source, Dictionary<TMP_FontAsset, TMP_FontAsset> copies,
            Dictionary<Texture, Texture> textures, List<Object> assets)
        {
            if (source == null) return null;
            if (copies.TryGetValue(source, out TMP_FontAsset existing)) return existing;
            TMP_FontAsset copy = Object.Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            // TMP destroys owned textures/material on teardown. Detach original references before any fallible work.
            Texture2D[] sourceAtlases = source.atlasTextures ?? Array.Empty<Texture2D>();
            copy.atlasTextures = new Texture2D[sourceAtlases.Length];
            SetField(copy, "m_AtlasTexture", null);
            copy.material = null;
            copies.Add(source, copy);
            assets.Add(copy);
            for (int i = 0; i < sourceAtlases.Length; i++)
            {
                Texture2D atlas = sourceAtlases[i];
                if (atlas == null) continue;
                if (!textures.TryGetValue(atlas, out Texture texture))
                {
                    texture = CopyAtlas(atlas);
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    textures.Add(atlas, texture);
                    assets.Add(texture);
                }
                copy.atlasTextures[i] = (Texture2D)texture;
            }
            if (source.material != null) copy.material = CopyMaterial(source.material, textures, assets);
            // The serialized glyph/packing tables retain the original atlas positions and rendering settings.
            // A dynamic clone adds missing characters exclusively to its detached texture copies.
            copy.atlasPopulationMode = source.atlasPopulationMode == AtlasPopulationMode.DynamicOS
                ? AtlasPopulationMode.DynamicOS : AtlasPopulationMode.Dynamic;
            if (copy.atlasPopulationMode == AtlasPopulationMode.Dynamic && copy.sourceFontFile == null)
            {
                if (source.sourceFontFile != null) SetField(copy, "m_SourceFontFile", source.sourceFontFile);
                else copy.atlasPopulationMode = AtlasPopulationMode.Static;
            }
            copy.fallbackFontAssetTable = source.fallbackFontAssetTable == null ? new List<TMP_FontAsset>() :
                source.fallbackFontAssetTable.Where(item => item != null).Select(item => CopyFont(item, copies, textures, assets)).ToList();
            TMP_FontWeightPair[] weights = source.fontWeightTable;
            if (weights != null)
            {
                var clonedWeights = (TMP_FontWeightPair[])weights.Clone();
                for (int i = 0; i < clonedWeights.Length; i++)
                {
                    clonedWeights[i].regularTypeface = CopyFont(weights[i].regularTypeface, copies, textures, assets);
                    clonedWeights[i].italicTypeface = CopyFont(weights[i].italicTypeface, copies, textures, assets);
                }
                SetField(copy, "m_FontWeightTable", clonedWeights);
            }
            return copy;
        }

        private static Material CopyMaterial(Material source, Dictionary<Texture, Texture> textures, List<Object> assets)
        {
            Material copy = Object.Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            assets.Add(copy);
            foreach (string property in source.GetTexturePropertyNames())
            {
                Texture texture = source.GetTexture(property);
                if (texture != null && textures.TryGetValue(texture, out Texture replacement)) copy.SetTexture(property, replacement);
            }
            return copy;
        }

        private static Texture2D CopyAtlas(Texture2D source)
        {
            if (source.isReadable) return Object.Instantiate(source);
            // 기본 TMP fallback atlas는 읽기 불가일 수 있다. importer 대신 GPU 사본에서 읽는다.
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readback.Apply();
                Color32[] pixels = readback.GetPixels32();
                TextureFormat format = source.format == TextureFormat.Alpha8 ? TextureFormat.Alpha8 : TextureFormat.RGBA32;
                var copy = new Texture2D(source.width, source.height, format, false, true);
                if (format == TextureFormat.Alpha8)
                {
                    var alpha = new byte[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;
                    copy.SetPixelData(alpha, 0);
                }
                else copy.SetPixels32(pixels);
                copy.Apply();
                copy.name = source.name + " Validation Copy";
                copy.filterMode = source.filterMode;
                copy.wrapMode = source.wrapMode;
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback);
            }
        }

        private static bool PrepareCharacter(TMP_FontAsset font, uint unicode, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font)) return false;
            if (font.HasCharacter((int)unicode)) return true;
            if (font.atlasPopulationMode != AtlasPopulationMode.Static &&
                font.TryAddCharacters(new[] { unicode }, out uint[] _, font.getFontFeatures)) return true;
            // Deliberately avoid TMP's global fallback search: only cloned font objects may receive glyph writes.
            return font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Any(item => PrepareCharacter(item, unicode, visited));
        }

        private static UIScreen CreateScreen(string name, Transform parent, Scene scene)
        {
            GameObject owner = CreateObject(name, parent, scene, typeof(RectTransform));
            GameObject panel = CreateObject("Root", owner.transform, scene, typeof(RectTransform));
            UIScreen screen = owner.AddComponent<UIScreen>();
            SetField(screen, "_root", panel);
            SetField(screen, "_window", panel.GetComponent<RectTransform>());
            SetField(screen, "_inputRoots", new[] { panel.GetComponent<RectTransform>() });
            return screen;
        }

        private static UIRegistration Registration(UIId id, UIScreen screen, UILayer layer, bool allowClose = true) =>
            new UIRegistration { Id = id, Instance = screen, Layer = layer, AllowUserClose = allowClose };
        private static bool InputEnabled(UIScreen screen)
        {
            CanvasGroup group = screen.Root.GetComponent<CanvasGroup>();
            return group != null && group.interactable && group.blocksRaycasts;
        }
        private static GameObject CreateObject(string name, Transform parent, Scene scene, params Type[] components)
        {
            var result = new GameObject(name, components);
            SceneManager.MoveGameObjectToScene(result, scene);
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }
        private static Component FindComponent(GameObject root, string typeName) =>
            root.GetComponentsInChildren<Component>(true).FirstOrDefault(item => item != null && item.GetType().Name == typeName) ??
            throw new InvalidOperationException("Source UI has no " + typeName + ". Pass the original MainUI instance.");
        private static T ReadObject<T>(Object target, string field) where T : Object
        {
            if (target == null) return null;
            using (var data = new SerializedObject(target)) return data.FindProperty(field)?.objectReferenceValue as T;
        }
        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        private static void RebindLifecycle(MonoBehaviour behaviour)
        {
            InvokeLifecycle(behaviour, "OnDisable");
            InvokeLifecycle(behaviour, "OnEnable");
        }
        private static void InvokeLifecycle(MonoBehaviour behaviour, string name)
        {
            if (behaviour != null) behaviour.GetType().GetMethod(name, PrivateInstance)?.Invoke(behaviour, null);
        }
        private static void Check(bool passed, string description, List<string> checks)
        {
            if (!passed) throw new InvalidOperationException(description);
            checks.Add("PASS: " + description);
        }
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run UI validation in Edit Mode.");
        }
        private static string SourceDescription(GameObject root) =>
            EditorUtility.IsPersistent(root) ? AssetDatabase.GetAssetPath(root) : root.scene.path + " / " + root.name + " (scene instance overrides included)";
        private static string Output(string file)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../PersonalDocs/UIValidation"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, file);
        }
    }
}
#endif
