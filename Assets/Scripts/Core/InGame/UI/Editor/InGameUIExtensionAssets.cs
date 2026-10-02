#if UNITY_EDITOR
using System;
using System.Linq;
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
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "DetailPopup.prefab");
                if (existing != null)
                {
                    if (existing.GetComponentsInChildren<Canvas>(true).Length != 0)
                        throw new InvalidOperationException("기존 상세 창은 두 Canvas 이전 도구로 먼저 변환하세요.");
                    RegisterExisting(rootPath, existing);
                    return folder + "DetailPopup.prefab";
                }
                root = new GameObject("DetailPopup", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, preview);
                root.SetActive(false);
                var host = (RectTransform)root.transform;
                host.anchorMin = Vector2.zero;
                host.anchorMax = Vector2.one;
                host.offsetMin = host.offsetMax = Vector2.zero;
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
                Set(screen, "_inputGroup", overlay.gameObject.AddComponent<CanvasGroup>());
                var screenData = new SerializedObject(screen);
                screenData.FindProperty("_id").intValue = (int)UIId.Detail;
                screenData.FindProperty("_isHud").boolValue = false;
                screenData.FindProperty("_canCloseByUser").boolValue = true;
                screenData.FindProperty("_blocksHudInput").boolValue = false;
                screenData.ApplyModifiedPropertiesWithoutUndo();
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
                RegisterExisting(rootPath, saved);
                return folder + "DetailPopup.prefab";
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void RegisterExisting(string rootPath, GameObject detailPrefab)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(rootPath);
            try
            {
                Transform popupRoot = contents.transform.Find("PopupCanvas/PopupRoot");
                if (popupRoot == null) throw new InvalidOperationException("두 Canvas 구조로 먼저 이전하세요.");
                UIScreen[] existing = popupRoot.GetComponentsInChildren<UIScreen>(true)
                    .Where(screen => new SerializedObject(screen).FindProperty("_id").intValue == (int)UIId.Detail).ToArray();
                if (existing.Length > 1) throw new InvalidOperationException("상세 화면이 중복 배치되어 있습니다.");
                UIScreen detail;
                if (existing.Length == 1) detail = existing[0];
                else
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(detailPrefab, popupRoot);
                    instance.SetActive(true);
                    detail = instance.GetComponent<UIScreen>();
                }
                var manager = new SerializedObject(contents.GetComponent<InGameUIManager>());
                SerializedProperty list = manager.FindProperty("_screenInstances");
                if (list == null) throw new InvalidOperationException("새 UI 화면 등록 필드가 없습니다.");
                bool registered = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == detail) registered = true;
                if (!registered)
                {
                    int index = list.arraySize++;
                    list.GetArrayElementAtIndex(index).objectReferenceValue = detail;
                }
                manager.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, rootPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
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
