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

namespace Game.Archive.Editor
{
    // 기존 정보 표시 프리팹을 복사해 도감용 프레임에 연결하는 일회성 설치 도구입니다.
    public static class ArchiveSetup
    {
        private const string Folder = "Assets/Prefabs/Archive/";
        private const string Source = "Assets/Prefabs/UI/InGame/";
        private static readonly Color Ink = new Color32(226, 231, 231, 255);
        private static readonly Color Muted = new Color32(151, 167, 175, 255);
        private static readonly Color Panel = new Color32(28, 39, 47, 255);
        private static readonly Color Accent = new Color32(207, 175, 111, 255);
        private static TMP_FontAsset _font;

        [MenuItem("Game/Archive/Install Archive")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit Mode에서 실행하세요.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("열린 씬의 변경 사항을 먼저 저장하세요.");
            _font = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "ArtifactPopup.prefab")
                .GetComponentInChildren<TMP_Text>(true).font;
            CreateIcons();
            CreateSlot(false);
            CreateSlot(true);
            CreateDetail("ArtifactPopup.prefab", "ArchiveArtifactContent.prefab", typeof(ArtifactPopupView));
            CreateDetail("UnitPopup.prefab", "ArchiveUnitContent.prefab", typeof(UnitPopupView));
            CreateBuildingDetail();
            CreateWindow();
            RegisterWindow();
            ConnectBootstrapPrefab();
            Debug.Log("[Archive] 도감 프리팹과 인게임 HUD 연결을 저장했습니다.");
        }

        private static void CreateIcons()
        {
            if (AssetDatabase.LoadMainAssetAtPath(Folder + "ArchiveIcons.asset") != null) return;
            var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false) { name = "ArchiveIcons", filterMode = FilterMode.Point };
            var pixels = new Color[64 * 32];
            // 공통 유닛 실루엣과 미발견 물음표. 에디터 전용 리소스에 의존하지 않습니다.
            string[] question = { "01110", "11011", "00011", "00110", "00100", "00000", "00100" };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool head = (x - 16) * (x - 16) + (y - 22) * (y - 22) < 30;
                    bool body = y >= 5 && y <= 15 && Mathf.Abs(x - 16) <= 10 - Mathf.Abs(y - 8) / 2;
                    if (head || body) pixels[y * 64 + x] = Ink;
                    int qx = (x - 6) / 4, qy = (29 - y) / 4;
                    if (x >= 6 && x < 26 && y >= 2 && y < 30 && qy < 7 && question[qy][qx] == '1')
                        pixels[y * 64 + x + 32] = Muted;
                }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, Folder + "ArchiveIcons.asset");
            var unit = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
            unit.name = "KnownUnit";
            var unknown = Sprite.Create(texture, new Rect(32, 0, 32, 32), new Vector2(.5f, .5f), 32);
            unknown.name = "Unknown";
            AssetDatabase.AddObjectToAsset(unit, texture);
            AssetDatabase.AddObjectToAsset(unknown, texture);
            AssetDatabase.SaveAssetIfDirty(texture);
        }

        private static void CreateSlot(bool relation)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Source + "ArtifactInventorySlot.prefab");
            try
            {
                root.name = relation ? "ArchiveRelationSlot" : "ArchiveEntrySlot";
                root.SetActive(false);
                var slot = root.GetComponent<UIItemSlot>();
                var icon = Read<UnityEngine.UI.Image>(slot, "_icon");
                var title = Read<TMP_Text>(slot, "_title");
                var value = Read<TMP_Text>(slot, "_value");
                root.GetComponent<UnityEngine.UI.Image>().color = Panel;
                root.GetComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.ColorTint;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                title.color = Ink;
                title.enableAutoSizing = true;
                title.fontSizeMin = 15;
                title.fontSizeMax = relation ? 21 : 19;
                title.alignment = relation ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
                title.overflowMode = TextOverflowModes.Ellipsis;
                value.fontSize = relation ? 16 : 14;
                value.color = Accent;
                value.alignment = relation ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
                title.text = "?";
                value.text = string.Empty;
                if (relation)
                {
                    Place(root.transform, 0, 0, 668, 76);
                    Place(icon.transform, 16, 12, 48, 48);
                    Place(title.transform, 80, 8, 564, 30);
                    Place(value.transform, 80, 39, 564, 26);
                    root.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 76;
                }
                else
                {
                    Place(root.transform, 0, 0, 132, 146);
                    Place(icon.transform, 32, 14, 68, 68);
                    Place(title.transform, 5, 88, 122, 32);
                    Place(value.transform, 5, 119, 122, 22);
                }
                RectTransform selection = Rect("Selection", root.transform);
                Stretch(selection);
                var border = selection.gameObject.AddComponent<UnityEngine.UI.Image>();
                border.color = new Color(Accent.r, Accent.g, Accent.b, .1f);
                border.raycastTarget = false;
                var outline = selection.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = Accent;
                outline.effectDistance = new Vector2(2, -2);
                Set(slot, "_selection", selection.gameObject);
                selection.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + root.name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CreateDetail(string source, string destination, Type viewType)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Source + source);
            try
            {
                root.SetActive(false);
                var view = root.GetComponent(viewType);
                Transform panel = root.transform.Find("Root/Panel");
                panel.SetParent(root.transform, false);
                foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                    if (child != panel) Object.DestroyImmediate(child.gameObject);
                Object.DestroyImmediate(panel.Find("Close").gameObject);
                Object.DestroyImmediate(root.GetComponent<UIScreen>());
                Set(view, "_screen", null);
                Stretch(panel.GetComponent<RectTransform>());
                panel.GetComponent<UnityEngine.UI.Image>().color = Panel;
                Stretch(panel.Find("TopAccent").GetComponent<RectTransform>(), 0, 0, 0, 477);
                if (view is ArtifactPopupView)
                {
                    Place(panel.Find("Header"), 22, 22, 648, 114);
                    Place(panel.Find("Header/IconFrame"), 0, 0, 90, 90);
                    Place(panel.Find("Header/Name"), 112, 0, 510, 64);
                    Place(Read<TMP_Text>(view, "_rarityText").transform, 112, 72, 510, 32);
                    Place(panel.Find("Effect"), 24, 160, 636, 278);
                    panel.Find("Effect").GetComponent<TMP_Text>().fontSize = 23;
                    panel.Find("Lore").gameObject.SetActive(false);
                }
                else
                {
                    Place(panel.Find("Name"), 24, 15, 634, 44);
                    Place(panel.Find("Lore"), 24, 65, 634, 30);
                    Place(panel.Find("UnitRows"), 24, 105, 634, 60);
                    Place(panel.Find("BaseStats"), 24, 175, 634, 130);
                    panel.Find("BaseStats").GetComponent<TMP_Text>().fontSize = 21;
                    Place(panel.Find("SkillsTitle"), 24, 309, 634, 32);
                    Place(panel.Find("SkillsScroll"), 24, 350, 634, 112);
                    Place(panel.Find("EmptySkills"), 24, 359, 634, 40);
                    var skillContent = Read<Transform>(view, "_skillContent");
                    foreach (Transform unused in skillContent.Cast<Transform>().ToArray())
                        if (unused.GetComponent<UIItemSlot>() == null) Object.DestroyImmediate(unused.gameObject);
                }
                root.name = destination.Replace(".prefab", string.Empty);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + destination);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CreateBuildingDetail()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Source + "BuildingInfo.prefab");
            try
            {
                root.SetActive(false);
                var view = root.GetComponent<BuildingInfoView>();
                var scroll = Read<UnityEngine.UI.ScrollRect>(view, "_detailsScroll");
                scroll.transform.SetParent(root.transform, false);
                foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                    if (child != scroll.transform) Object.DestroyImmediate(child.gameObject);
                foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
                    if (component != view) Object.DestroyImmediate(component);
                Set(view, "_playerPopup", null);
                foreach (Transform child in scroll.content.Cast<Transform>().ToArray())
                    if (child.name != "Building") Object.DestroyImmediate(child.gameObject);
                Stretch((RectTransform)scroll.transform, 12, 10, 12, 10);
                root.AddComponent<UnityEngine.UI.Image>().color = Panel;
                root.name = "ArchiveBuildingContent";
                PrefabUtility.SaveAsPrefabAsset(root, Folder + root.name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CreateWindow()
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var host = new GameObject("ArchiveWindow", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(host, preview);
                Stretch((RectTransform)host.transform);
                var screen = host.AddComponent<UIScreen>();
                RectTransform overlay = Rect("Root", host.transform);
                Stretch(overlay);
                overlay.gameObject.SetActive(false);
                overlay.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .76f);
                var group = overlay.gameObject.AddComponent<CanvasGroup>();
                var view = overlay.gameObject.AddComponent<ArchiveView>();
                Set(screen, "_root", overlay.gameObject);
                Set(screen, "_inputGroup", group);
                var so = new SerializedObject(screen);
                so.FindProperty("_id").intValue = (int)UIId.Archive;
                so.FindProperty("_blocksHudInput").boolValue = true;
                so.FindProperty("_closeWhenCovered").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                RectTransform window = Rect("Window", overlay);
                window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
                window.sizeDelta = new Vector2(1560, 930);
                window.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(17, 26, 33, 255);
                Label(window, "Title", "도감", 30, 26, 260, 46, 32).color = Accent;
                Label(window, "Subtitle", "한 번 만난 기록은 다음 모험에도 남습니다", 30, 78, 850, 32, 19).color = Muted;
                var close = Button(window, "Close", "닫기  ×", 1398, 24, 130, 46);
                var closeUtility = close.gameObject.AddComponent<CloseUtility>();
                Set(closeUtility, "_button", close);
                Set(closeUtility, "_screen", screen);
                Set(view, "_artifactTabButton", Button(window, "ArtifactTab", "아티팩트", 30, 130, 228, 52));
                Set(view, "_buildingTabButton", Button(window, "BuildingTab", "건물", 274, 130, 228, 52));
                Set(view, "_unitTabButton", Button(window, "UnitTab", "유닛", 518, 130, 228, 52));
                var team = Rect("Teams", window);
                Place(team, 30, 196, 338, 40);
                Set(view, "_teamControls", team.gameObject);
                Set(view, "_allyButton", Button(team, "Allies", "아군", 0, 0, 150, 40));
                Set(view, "_enemyButton", Button(team, "Enemies", "적군", 164, 0, 150, 40));
                Set(view, "_progressText", Label(window, "Progress", "발견 0 / 0", 395, 198, 351, 36, 20));
                var list = Scroll(window, "Entries", 30, 254, 716, 618, out RectTransform entries);
                var grid = entries.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                grid.cellSize = new Vector2(132, 146);
                grid.spacing = new Vector2(10, 12);
                grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 5;
                Set(view, "_listScroll", list);
                Set(view, "_listContent", entries);
                Set(view, "_slotTemplate", Instance("ArchiveEntrySlot.prefab", entries).GetComponent<UIItemSlot>());
                Label(window, "Hint", "?  아직 발견하지 못한 항목", 30, 885, 710, 25, 17).color = Muted;
                Set(view, "_backButton", Button(window, "Previous", "‹  이전 정보", 792, 130, 186, 48));
                Label(window, "DetailLabel", "상세 정보", 1090, 136, 408, 38, 21).alignment = TextAlignmentOptions.Right;
                var details = Rect("Details", window);
                Place(details, 792, 196, 706, 480);
                var empty = Label(details, "EmptyDetail", "목록에서 발견한 항목을 선택하세요", 32, 195, 642, 90, 23);
                empty.alignment = TextAlignmentOptions.Center;
                empty.color = Muted;
                Set(view, "_emptyDetail", empty.gameObject);
                Set(view, "_artifactDetail", Instance("ArchiveArtifactContent.prefab", details).GetComponent<ArtifactPopupView>());
                Set(view, "_buildingDetail", Instance("ArchiveBuildingContent.prefab", details).GetComponent<BuildingInfoView>());
                Set(view, "_unitDetail", Instance("ArchiveUnitContent.prefab", details).GetComponent<UnitPopupView>());
                var relations = Scroll(window, "Relations", 792, 696, 706, 210, out RectTransform links);
                var layout = links.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.spacing = 8;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                var header = Label(links, "HeaderTemplate", string.Empty, 0, 0, 680, 36, 20);
                header.color = Accent;
                header.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 38;
                header.gameObject.SetActive(false);
                Set(view, "_relationsRoot", relations.gameObject);
                Set(view, "_relationsContent", links);
                Set(view, "_relationHeaderTemplate", header);
                Set(view, "_relationSlotTemplate", Instance("ArchiveRelationSlot.prefab", links).GetComponent<UIItemSlot>());
                Set(view, "_artifactCatalog", AssetDatabase.LoadMainAssetAtPath("Assets/Data/Artifacts/Artifact_Catalog.asset"));
                Set(view, "_buildingDatabase", AssetDatabase.LoadMainAssetAtPath("Assets/Data/Buildings/DB/BuildingDatabase.asset"));
                Set(view, "_allyUnitDatabase", AssetDatabase.LoadMainAssetAtPath("Assets/Data/Units/UnitDatas/UnitDB/AllyDB.asset"));
                Set(view, "_enemyUnitDatabase", AssetDatabase.LoadMainAssetAtPath("Assets/Data/Units/UnitDatas/UnitDB/EnemyDB.asset"));
                Object[] icons = AssetDatabase.LoadAllAssetsAtPath(Folder + "ArchiveIcons.asset");
                Set(view, "_placeholderIcon", icons.OfType<Sprite>().Single(s => s.name == "KnownUnit"));
                Set(view, "_unknownIcon", icons.OfType<Sprite>().Single(s => s.name == "Unknown"));
                ConfigureRightPane(window);
                PrefabUtility.SaveAsPrefabAsset(host, Folder + "ArchiveWindow.prefab");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static void RefineLayout()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Folder + "ArchiveWindow.prefab");
            try
            {
                ConfigureRightPane(root.transform.Find("Root/Window"));
                foreach (var scroll in root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
                    if (scroll.name == "Entries" || scroll.name == "Relations") AddScrollbar(scroll);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "ArchiveWindow.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureRightPane(Transform window)
        {
            Transform body = window.Find("RightBody");
            if (body != null) return;
            body = Rect("RightBody", window);
            Place(body, 792, 196, 706, 710);
            var bodyLayout = body.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            bodyLayout.spacing = 20;
            bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;
            Transform details = window.Find("Details");
            details.SetParent(body, false);
            var detailsLayout = details.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            detailsLayout.childControlWidth = detailsLayout.childControlHeight = true;
            detailsLayout.childForceExpandWidth = true;
            detailsLayout.childForceExpandHeight = false;
            foreach (Transform detail in details)
            {
                var element = detail.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minHeight = element.preferredHeight = detail.GetComponent<BuildingInfoView>() != null ? 260 : 480;
                element.flexibleHeight = 0;
            }
            Transform relations = window.Find("Relations");
            relations.SetParent(body, false);
            var relationsLayout = relations.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            relationsLayout.minHeight = relationsLayout.preferredHeight = 180;
            relationsLayout.flexibleHeight = 1;
        }

        private static void RegisterWindow()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Source + "InGameUIRoot.prefab");
            try
            {
                var manager = root.GetComponent<InGameUIManager>();
                Transform popupRoot = root.transform.Find("PopupCanvas/Root");
                Transform archive = popupRoot.Find("ArchiveWindow");
                if (archive == null) archive = Instance("ArchiveWindow.prefab", popupRoot).transform;
                archive.gameObject.SetActive(true);
                var view = archive.GetComponentInChildren<ArchiveView>(true);
                Set(view.GetComponentInChildren<UnitPopupView>(true), "_skillPopup", root.GetComponentInChildren<SkillPopupView>(true));
                Set(root.GetComponent<InGameUIStartup>(), "_archiveView", view);
                var data = new SerializedObject(manager);
                var screens = data.FindProperty("_screenInstances");
                var screen = archive.GetComponent<UIScreen>();
                if (!Enumerable.Range(0, screens.arraySize).Any(i => screens.GetArrayElementAtIndex(i).objectReferenceValue == screen))
                    screens.GetArrayElementAtIndex(screens.arraySize++).objectReferenceValue = screen;
                data.ApplyModifiedPropertiesWithoutUndo();
                var hud = root.GetComponentInChildren<GameHudView>(true);
                Transform bottom = hud.transform.Find("Content/BottomBar");
                Transform entry = bottom.Find("Archive");
                if (entry == null)
                {
                    entry = Object.Instantiate(bottom.Find("Artifacts").gameObject, bottom).transform;
                    entry.name = "Archive";
                    foreach (MonoBehaviour component in entry.GetComponents<MonoBehaviour>())
                        if (!(component is UnityEngine.UI.Image) && !(component is UnityEngine.UI.Button)) Object.DestroyImmediate(component);
                    var button = entry.GetComponent<UnityEngine.UI.Button>();
                    button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                    var open = entry.gameObject.AddComponent<OpenUtility>();
                    Set(open, "_button", button);
                    Set(open, "_manager", manager);
                    var openData = new SerializedObject(open);
                    openData.FindProperty("_target").intValue = (int)UIId.Archive;
                    openData.ApplyModifiedPropertiesWithoutUndo();
                    entry.GetComponentInChildren<TMP_Text>(true).text = "도감";
                    var rect = (RectTransform)entry;
                    rect.anchoredPosition -= new Vector2(232, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Source + "InGameUIRoot.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void ConnectBootstrapPrefab()
        {
            const string path = "Assets/Prefabs/Core/CoreSystem.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BootStrap bootstrap = root.GetComponent<BootStrap>();
                var controller = bootstrap.GetComponent<ArchiveController>();
                if (controller == null) controller = bootstrap.gameObject.AddComponent<ArchiveController>();
                Set(bootstrap, "_archiveController", controller);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject Instance(string file, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + file), parent);
            Stretch((RectTransform)instance.transform);
            return instance;
        }

        private static UnityEngine.UI.ScrollRect Scroll(Transform parent, string name, float x, float y, float w, float h, out RectTransform content)
        {
            RectTransform root = Rect(name, parent);
            Place(root, x, y, w, h);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            RectTransform viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            AddScrollbar(scroll);
            return scroll;
        }

        private static void AddScrollbar(UnityEngine.UI.ScrollRect scroll)
        {
            if (scroll.verticalScrollbar != null) return;
            RectTransform track = Rect("Scrollbar", scroll.transform);
            track.anchorMin = new Vector2(1, 0);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(1, .5f);
            track.offsetMin = new Vector2(-8, 0);
            track.offsetMax = Vector2.zero;
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(28, 39, 47, 255);
            RectTransform handle = Rect("Handle", track);
            Stretch(handle);
            var image = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color32(109, 131, 141, 255);
            var bar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = image;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.viewport.offsetMax = new Vector2(-12, scroll.viewport.offsetMax.y);
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name, string text, float x, float y, float w, float h)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, x, y, w, h);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color32(48, 65, 75, 255);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            TMP_Text label = Label(rect, "Label", text, 4, 0, w - 8, h, 22);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_Text Label(Transform parent, string name, string text, float x, float y, float w, float h, float size)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.color = Ink;
            label.fontSize = size;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            root.transform.SetParent(parent, false);
            return (RectTransform)root.transform;
        }

        private static void Place(Transform transform, float x, float y, float w, float h)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static T Read<T>(Object target, string field) where T : Object =>
            (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;

        private static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
