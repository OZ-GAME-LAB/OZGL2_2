#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.InGame.Editor
{
    /// <summary>실제 게임 데이터와 연결하지 않는 공용 상세 창 예제 프리팹을 만든다.</summary>
    public static class InGameUIExtensionAssets
    {
        public static string CreateAndRegister()
        {
            const string folder = "Assets/Prefabs/UI/Test/";
            const string rootPath = folder + "InGameUIRoot.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(rootPath);
            if (source == null) throw new InvalidOperationException("Create InGameUIRoot first.");
            TMP_Text fontSource = source.GetComponentInChildren<TMP_Text>(true);
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("DetailPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(root, preview);
                root.SetActive(false);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 150;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                RectTransform overlay = Rect("Overlay", root.transform, Vector2.zero, Vector2.zero);
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                Image shade = overlay.gameObject.AddComponent<Image>();
                shade.color = new Color(0, 0, 0, .65f);
                RectTransform window = Rect("Window", overlay, new Vector2(620, 360), Vector2.zero);
                Image background = window.gameObject.AddComponent<Image>();
                background.color = new Color32(25, 35, 42, 255);
                TMP_Text title = Label("Title", window, new Vector2(480, 50), new Vector2(-20, 125), 28, fontSource);
                TMP_Text subtitle = Label("Subtitle", window, new Vector2(540, 36), new Vector2(0, 72), 20, fontSource);
                TMP_Text description = Label("Description", window, new Vector2(540, 190), new Vector2(0, -48), 22, fontSource);
                description.alignment = TextAlignmentOptions.TopLeft;
                RectTransform close = Rect("Close", window, new Vector2(46, 46), new Vector2(272, 147));
                Image closeBackground = close.gameObject.AddComponent<Image>();
                closeBackground.color = new Color32(55, 72, 81, 255);
                Button closeButton = close.gameObject.AddComponent<Button>();
                closeButton.targetGraphic = closeBackground;
                RectTransform icon = Rect("Icon", close, new Vector2(30, 30), Vector2.zero);
                UIIcon closeIcon = icon.gameObject.AddComponent<UIIcon>();
                closeIcon.Kind = UIIcon.Symbol.Close;
                closeIcon.raycastTarget = false;
                UIScreen screen = root.AddComponent<UIScreen>();
                Set(screen, "_root", overlay.gameObject);
                Set(screen, "_initialFocus", closeButton);
                // 상세 창에는 접기 기능이 없으므로 _window 높이 조절을 사용하지 않는다.
                var detail = root.AddComponent<DetailPopupView>();
                Set(detail, "_title", title);
                Set(detail, "_subtitle", subtitle);
                Set(detail, "_description", description);
                var utility = close.gameObject.AddComponent<CloseUtility>();
                Set(utility, "_button", closeButton);
                Set(utility, "_screen", screen);
                overlay.gameObject.SetActive(false);
                root.SetActive(true);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, folder + "DetailPopup.prefab");
                UIScreen detailPrefab = saved.GetComponent<UIScreen>();

                GameObject contents = PrefabUtility.LoadPrefabContents(rootPath);
                try
                {
                    var manager = contents.GetComponent<InGameUIManager>();
                    var serialized = new SerializedObject(manager);
                    SerializedProperty list = serialized.FindProperty("_screens");
                    int index = -1;
                    for (int i = 0; i < list.arraySize; i++)
                        if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Id").intValue == (int)UIId.Detail) index = i;
                    if (index < 0) { index = list.arraySize; list.arraySize++; }
                    SerializedProperty entry = list.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("Id").intValue = (int)UIId.Detail;
                    entry.FindPropertyRelative("Instance").objectReferenceValue = null;
                    entry.FindPropertyRelative("Prefab").objectReferenceValue = detailPrefab;
                    entry.FindPropertyRelative("Parent").objectReferenceValue = contents.transform;
                    entry.FindPropertyRelative("Layer").intValue = (int)UILayer.Popup;
                    entry.FindPropertyRelative("AllowUserClose").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, rootPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return folder + "DetailPopup.prefab";
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform));
            var rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, TMP_Text source)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
            text.fontSize = fontSize;
            text.color = source.color;
            text.text = string.Empty;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
