#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>기존 결산창의 행 모양을 보존하고 표시 영역 안에 동적 목록을 연결한다.</summary>
    public static class TotemRowSetup
    {
        public const string RowPath = "Assets/Prefabs/UI/InGame/TotemRow.prefab";
        private const string ColumnPath = "Window/SummaryBox/TotemColumn";

        [MenuItem("Game/UI/InGame/Connect Totem Rows")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit Mode에서 실행해주세요.");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var views = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RunResultView>(true)).ToArray();
            if (views.Length != 1) throw new InvalidOperationException("결산 View 하나가 필요합니다.");
            Transform frame = views[0].transform.Find("RunSettlementFrame");
            if (frame == null) throw new InvalidOperationException("기존 결산 프레임이 없습니다.");

            if (AssetDatabase.LoadAssetAtPath<TotemRowView>(RowPath) == null)
                CreateRowPrefab(frame);

            string[] paths =
            {
                "Assets/Prefabs/UI/InGame/RunSettlementFrame.prefab",
                "Assets/Prefabs/UI/InGame/RunResult.prefab",
                "Assets/Prefabs/UI/InGame/InGameUIRoot.prefab",
                "Assets/Prefabs/UI/InGameUIRoot_StoneBronze.prefab"
            };
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool active = root.activeSelf;
                root.SetActive(false);
                try
                {
                    if (root.name == "RunSettlementFrame") ConfigureFrame(root);
                    else
                        foreach (var view in root.GetComponentsInChildren<RunResultView>(true))
                        {
                            var child = view.transform.Find("RunSettlementFrame");
                            if (child != null) Configure(view, child.gameObject);
                        }
                    root.SetActive(active);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // 중첩 프레임이 갱신된 다음 씬 인스턴스의 참조를 연결한다.
            Configure(views[0], views[0].transform.Find("RunSettlementFrame").gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateRowPrefab(Transform frame)
        {
            var source = frame.Find(ColumnPath + "/TotemRows/TotemRow1");
            if (source == null) throw new InvalidOperationException("복제할 기존 토템 행이 없습니다.");
            var preview = EditorSceneManager.NewPreviewScene();
            var holder = new GameObject("TotemRowPrefabSource");
            holder.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, preview);
            try
            {
                var row = Object.Instantiate(source.gameObject, holder.transform, false);
                row.name = "TotemRow";
                if (PrefabUtility.IsPartOfPrefabInstance(row))
                    PrefabUtility.UnpackPrefabInstance(row, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var rect = row.GetComponent<RectTransform>();
                rect.anchoredPosition = Vector2.zero;
                var layout = GetOrAdd<HorizontalLayoutGroup>(row);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.spacing = 0;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                GetOrAdd<LayoutElement>(row).minHeight = 36;
                var name = row.transform.Find("Name").GetComponent<TMP_Text>();
                var bonus = row.transform.Find("Multiplier").GetComponent<TMP_Text>();
                name.enableWordWrapping = true;
                name.raycastTarget = false;
                bonus.raycastTarget = false;
                var nameLayout = GetOrAdd<LayoutElement>(name.gameObject);
                nameLayout.minWidth = 100;
                nameLayout.preferredWidth = 144;
                nameLayout.flexibleWidth = 1;
                var bonusLayout = GetOrAdd<LayoutElement>(bonus.gameObject);
                bonusLayout.minWidth = 70;
                bonusLayout.preferredWidth = 70;
                var view = GetOrAdd<TotemRowView>(row);
                Set(view, "_name", name);
                Set(view, "_bonus", bonus);
                PrefabUtility.SaveAsPrefabAsset(row, RowPath);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void Configure(RunResultView view, GameObject frame)
        {
            ConfigureFrame(frame);
            var rows = frame.transform.Find(ColumnPath + "/TotemRows");
            Set(view, "_totemRowPrefab", AssetDatabase.LoadAssetAtPath<TotemRowView>(RowPath));
            Set(view, "_totemContent", rows.Find("Viewport/Content"));
            Set(view, "_emptyTotems", rows.Find("EmptyTotems").gameObject);
            Set(view, "_totemScrollRect", rows.GetComponent<ScrollRect>());
        }

        private static void ConfigureFrame(GameObject frame)
        {
            bool active = frame.activeSelf;
            frame.SetActive(false);
            try
            {
                var rows = frame.transform.Find(ColumnPath + "/TotemRows") as RectTransform;
                if (rows == null) throw new InvalidOperationException("토템 표시 영역이 없습니다.");
                var viewport = rows.Find("Viewport") as RectTransform;
                if (viewport == null)
                {
                    // 행만 교체하며 바깥 열, 합계, 버튼의 위치는 그대로 둔다.
                    var oldRows = rows.Cast<Transform>().Where(t => t.name.StartsWith("TotemRow")).ToArray();
                    foreach (var old in oldRows) Object.DestroyImmediate(old.gameObject);
                    viewport = NewRect("Viewport", rows);
                    Stretch(viewport);
                    GetOrAdd<RectMask2D>(viewport.gameObject);
                    var hitArea = GetOrAdd<UnityEngine.UI.Image>(viewport.gameObject);
                    hitArea.color = Color.clear;
                    hitArea.raycastTarget = true;
                    var content = NewRect("Content", viewport);
                    content.anchorMin = new Vector2(0, 1);
                    content.anchorMax = Vector2.one;
                    content.pivot = new Vector2(0, 1);
                    content.sizeDelta = Vector2.zero;
                    content.anchoredPosition = Vector2.zero;
                    var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
                    list.spacing = 6;
                    list.childAlignment = TextAnchor.UpperLeft;
                    list.childControlWidth = true;
                    list.childControlHeight = true;
                    list.childForceExpandWidth = true;
                    list.childForceExpandHeight = false;
                    content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    var empty = NewRect("EmptyTotems", rows);
                    Stretch(empty);
                    var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPath);
                    var template = rowPrefab.transform.Find("Name").GetComponent<TMP_Text>();
                    var label = empty.gameObject.AddComponent<TextMeshProUGUI>();
                    label.font = template.font;
                    label.fontSharedMaterial = template.fontSharedMaterial;
                    label.fontSize = template.fontSize;
                    label.fontStyle = template.fontStyle;
                    label.color = template.color;
                    label.enableWordWrapping = true;
                    label.text = "선택한 토템 없음";
                    label.alignment = TextAlignmentOptions.TopLeft;
                    label.raycastTarget = false;
                    empty.gameObject.SetActive(false);
                }
                var scroll = GetOrAdd<ScrollRect>(rows.gameObject);
                scroll.viewport = viewport;
                scroll.content = viewport.Find("Content") as RectTransform;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 24;
                scroll.inertia = false;
                ConfigureScrollbar(rows, scroll);
                Record(rows.gameObject);
                Record(scroll);
            }
            finally { frame.SetActive(active); }
        }

        private static void ConfigureScrollbar(RectTransform rows, ScrollRect scroll)
        {
            var bar = rows.Find("Scrollbar") as RectTransform;
            if (bar == null)
            {
                // 기존 텍스트 폭은 유지하고 토템 열의 오른쪽 여백에 막대를 둔다.
                bar = NewRect("Scrollbar", rows);
                bar.anchorMin = new Vector2(1, 0);
                bar.anchorMax = Vector2.one;
                bar.offsetMin = new Vector2(4, 0);
                bar.offsetMax = new Vector2(12, 0);
                var background = bar.gameObject.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(0.2f, 0.2f, 0.2f, 0.35f);
                var handle = NewRect("Handle", bar);
                Stretch(handle);
                var thumb = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
                thumb.color = new Color(0.7f, 0.65f, 0.5f, 1f);
                var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
                scrollbar.handleRect = handle;
                scrollbar.targetGraphic = thumb;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            scroll.verticalScrollbar = bar.GetComponent<Scrollbar>();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component => host.GetComponent<T>() ?? host.AddComponent<T>();
        private static void Set(Object target, string field, Object value)
        {
            if (value == null) throw new InvalidOperationException("연결할 참조가 없습니다: " + field);
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            Record(target);
        }
        private static void Record(Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
#endif
