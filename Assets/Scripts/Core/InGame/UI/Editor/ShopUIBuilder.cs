#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>상점 화면만 추가하고 기존 Test 씬의 상점 참조를 연결한다.</summary>
    public static class ShopUIBuilder
    {
        private const string RootPrefabPath = "Assets/Prefabs/UI/Test/InGameUIRoot.prefab";
        private const string ShopPrefabPath = "Assets/Prefabs/UI/Test/ShopScreen.prefab";
        private const string ScenePath = "Assets/Scenes/Test/Test.unity";

        // 기존 인게임 UI와 같은 색상이다.
        private static readonly Color Panel = new Color32(29, 36, 40, 255);
        private static readonly Color Tile = new Color32(39, 48, 51, 255);
        private static readonly Color Paper = new Color32(235, 230, 211, 255);
        private static readonly Color Muted = new Color32(156, 171, 166, 255);
        private static readonly Color Gold = new Color32(222, 184, 105, 255);
        private static readonly Color Ink = new Color32(19, 24, 28, 255);

        [MenuItem("Game/UI/InGame/Build And Connect Shop")]
        public static void BuildAndConnect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("상점 UI는 Edit Mode에서 생성하세요.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
                throw new InvalidOperationException("Test 씬의 기존 변경을 저장한 뒤 실행하세요.");

            InGameUIManager manager = FindInScene<InGameUIManager>(scene).Single();
            BootStrap bootstrap = FindInScene<BootStrap>(scene).Single();
            InGameUIStartup startup = bootstrap.GetComponent<InGameUIStartup>();
            if (startup == null)
                throw new InvalidOperationException("인게임 UI 연결 도구로 InGameUIStartup을 먼저 연결하세요.");
            RequirePopupRoot(manager.gameObject);

            GameObject rootPrefab = Load<GameObject>(RootPrefabPath);
            TMP_Text fontSource = rootPrefab.GetComponentsInChildren<TMP_Text>(true)
                .First(text => text.font != null);
            GameObject shopSystem = Load<GameObject>("Assets/Prefabs/Shop/ShopSystem.prefab");
            ConsumableItemCatalog catalog = Load<ConsumableItemCatalog>(
                "Assets/Data/ConsumableItems/ConsumableItemCatalog.asset");
            ConsumableItemCaster caster = Load<GameObject>(
                "Assets/Prefabs/Consumables/ConsumableItemCaster.prefab").GetComponent<ConsumableItemCaster>();
            if (caster == null) throw new InvalidOperationException("소모품 Caster 프리팹의 컴포넌트를 확인하세요.");

            GameObject shopPrefab = CreateShopPrefab(fontSource);
            RegisterInRootPrefab(shopPrefab);
            UIScreen screen = RegisterShop(manager.gameObject, shopPrefab);

            ShopManager shop = FindInScene<ShopManager>(scene).SingleOrDefault();
            if (shop == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(shopSystem, scene);
                instance.name = "ShopSystem";
                instance.SetActive(true);
                shop = instance.GetComponent<ShopManager>();
            }
            if (shop == null || !shop.gameObject.activeInHierarchy)
                throw new InvalidOperationException("ShopSystem은 씬의 활성 시스템 오브젝트여야 합니다.");

            // 꺼진 UI 패널에 배치하면 아이템 Caster가 비활성화되어 초기화되지 않는다.
            ConsumableItemManager consumables = shop.GetComponent<ConsumableItemManager>();
            if (consumables == null) consumables = shop.gameObject.AddComponent<ConsumableItemManager>();
            SetReference(consumables, "_itemCatalog", catalog);
            SetReference(consumables, "_itemCasterPrefab", caster);
            SetReference(bootstrap, "_shopManager", shop);
            SetReference(bootstrap, "_consumableItemManager", consumables);
            SetReference(startup, "_shopView", screen.GetComponent<ShopView>());

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("상점 연결을 Test 씬에 저장하지 못했습니다.");
            Debug.Log("[Shop UI] ShopScreen 프리팹 생성 및 Test 씬 연결 완료: " + ShopPrefabPath);
        }

        private static GameObject CreateShopPrefab(TMP_Text fontSource)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (existing != null)
            {
                if (existing.GetComponent<ShopView>() == null || existing.GetComponent<UIScreen>() == null ||
                    existing.GetComponentsInChildren<Canvas>(true).Length != 0)
                    throw new InvalidOperationException("기존 ShopScreen 프리팹 구조를 확인하세요.");
                return existing;
            }

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var host = new GameObject("ShopScreen", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(host, preview);
                host.SetActive(false);
                Stretch((RectTransform)host.transform);

                RectTransform overlay = Rect("Overlay", host.transform, Vector2.zero, Vector2.zero);
                Stretch(overlay);
                AddImage(overlay, new Color(.015f, .022f, .025f, .72f), true);
                CanvasGroup input = overlay.gameObject.AddComponent<CanvasGroup>();
                input.interactable = false;
                input.blocksRaycasts = false;

                RectTransform window = Rect("Window", overlay, new Vector2(1320, 860), Vector2.zero);
                AddImage(window, Panel, true);
                RectTransform accent = Rect("TopAccent", window, new Vector2(1320, 4), new Vector2(0, 428));
                AddImage(accent, Gold);
                Label("Title", window, new Vector2(450, 64), new Vector2(0, 367),
                    "상점", 38, Paper, fontSource);
                TMP_Text goldText = Label("Gold", window, new Vector2(270, 48), new Vector2(470, 369),
                    "골드  0", 26, Gold, fontSource);

                Label("ArtifactHeading", window, new Vector2(1220, 32), new Vector2(0, 315),
                    "아티팩트", 22, Muted, fontSource, TextAlignmentOptions.MidlineLeft);
                var artifactCards = new ShopItemCardView[5];
                for (int i = 0; i < artifactCards.Length; i++)
                    artifactCards[i] = CreateCard("ArtifactCard" + (i + 1), window,
                        new Vector2(230, 320), new Vector2((i - 2) * 250, 132), false, fontSource);

                Label("ConsumableHeading", window, new Vector2(600, 32), new Vector2(-315, -60),
                    "1회성 소모 아이템", 22, Muted, fontSource, TextAlignmentOptions.MidlineLeft);
                ShopItemCardView consumable = CreateCard("ConsumableCard", window,
                    new Vector2(600, 250), new Vector2(-315, -216), true, fontSource);
                CreateExchangePanel(window, fontSource);

                TMP_Text feedback = Label("Feedback", window, new Vector2(930, 44), new Vector2(-135, -385),
                    string.Empty, 20, Paper, fontSource, TextAlignmentOptions.MidlineLeft);
                UnityEngine.UI.Button leave = CreateButton("Leave", window, new Vector2(240, 52),
                    new Vector2(480, -385), "상점 나가기", fontSource, out _);

                UIScreen screen = host.AddComponent<UIScreen>();
                var screenData = new SerializedObject(screen);
                screenData.FindProperty("_id").intValue = (int)UIId.Shop;
                screenData.FindProperty("_isHud").boolValue = false;
                screenData.FindProperty("_canCloseByUser").boolValue = false;
                screenData.FindProperty("_blocksHudInput").boolValue = true;
                screenData.FindProperty("_root").objectReferenceValue = overlay.gameObject;
                screenData.FindProperty("_inputGroup").objectReferenceValue = input;
                screenData.ApplyModifiedPropertiesWithoutUndo();

                ShopView view = host.AddComponent<ShopView>();
                SetReference(view, "_screen", screen);
                SetReference(view, "_goldText", goldText);
                SetArray(view, "_artifactCards", artifactCards);
                SetArray(view, "_consumableCards", new[] { consumable });
                SetReference(view, "_feedbackText", feedback);
                SetReference(view, "_leaveButton", leave);

                // 요청을 받는 Host는 활성 상태로 두고 표시 패널만 숨긴다.
                overlay.gameObject.SetActive(false);
                host.SetActive(true);
                return PrefabUtility.SaveAsPrefabAsset(host, ShopPrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static ShopItemCardView CreateCard(string name, Transform parent, Vector2 size,
            Vector2 position, bool wide, TMP_Text fontSource)
        {
            RectTransform card = Rect(name, parent, size, position);
            AddImage(card, Tile);
            Vector2 iconPosition = wide ? new Vector2(-220, 35) : new Vector2(0, 96);
            RectTransform iconRect = Rect("Icon", card, new Vector2(72, 72), iconPosition);
            UnityEngine.UI.Image icon = AddImage(iconRect, Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;
            RectTransform emptyRect = Rect("EmptyIcon", card, new Vector2(64, 64), iconPosition);
            UIIcon emptyIcon = emptyRect.gameObject.AddComponent<UIIcon>();
            emptyIcon.Kind = wide ? UIIcon.Symbol.Plus : UIIcon.Symbol.Spark;
            emptyIcon.color = Gold;
            emptyIcon.raycastTarget = false;

            TMP_Text nameText = Label("Name", card, wide ? new Vector2(390, 46) : new Vector2(210, 46),
                wide ? new Vector2(65, 72) : new Vector2(0, 39), "상품", 22, Paper, fontSource);
            TMP_Text description = Label("Description", card, wide ? new Vector2(390, 80) : new Vector2(202, 78),
                wide ? new Vector2(65, 9) : new Vector2(0, -26), string.Empty, 18, Muted, fontSource,
                TextAlignmentOptions.TopLeft);
            TMP_Text price = Label("Price", card, wide ? new Vector2(240, 36) : new Vector2(210, 34),
                wide ? new Vector2(-72, -83) : new Vector2(0, -87), "골드  0", 21, Gold, fontSource);
            UnityEngine.UI.Button buy = CreateButton("Buy", card, wide ? new Vector2(190, 48) : new Vector2(202, 44),
                wide ? new Vector2(180, -83) : new Vector2(0, -133), "구매", fontSource, out TMP_Text buyText);

            ShopItemCardView view = card.gameObject.AddComponent<ShopItemCardView>();
            SetReference(view, "_icon", icon);
            SetReference(view, "_emptyIcon", emptyIcon);
            SetReference(view, "_nameText", nameText);
            SetReference(view, "_descriptionText", description);
            SetReference(view, "_priceText", price);
            SetReference(view, "_buyButton", buy);
            SetReference(view, "_buyButtonText", buyText);
            return view;
        }

        private static void CreateExchangePanel(Transform parent, TMP_Text fontSource)
        {
            RectTransform exchange = Rect("Exchange", parent, new Vector2(600, 280), new Vector2(315, -201));
            AddImage(exchange, Tile);
            Label("Title", exchange, new Vector2(520, 36), new Vector2(0, 111),
                "아티팩트 교환", 22, Muted, fontSource);
            foreach (int direction in new[] { -1, 1 })
            {
                RectTransform slot = Rect(direction < 0 ? "OwnedSlot" : "OfferedSlot", exchange,
                    new Vector2(160, 120), new Vector2(direction * 160, 11));
                AddImage(slot, Panel);
                Label("Empty", slot, new Vector2(140, 36), Vector2.zero,
                    "빈 슬롯", 20, Muted, fontSource);
            }
            Label("Arrow", exchange, new Vector2(72, 52), new Vector2(0, 11),
                "↔", 34, Muted, fontSource);
            UnityEngine.UI.Button button = CreateButton("ExchangeButton", exchange, new Vector2(280, 44),
                new Vector2(0, -94), "교환", fontSource, out _);
            button.interactable = false;
        }

        private static void RegisterInRootPrefab(GameObject shopPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RootPrefabPath);
            try
            {
                RegisterShop(root, shopPrefab);
                PrefabUtility.SaveAsPrefabAsset(root, RootPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static UIScreen RegisterShop(GameObject root, GameObject shopPrefab)
        {
            Transform popupRoot = RequirePopupRoot(root);
            UIScreen screen = popupRoot.GetComponentsInChildren<UIScreen>(true)
                .Where(item => item.Id == UIId.Shop).SingleOrDefault();
            if (screen == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(shopPrefab, popupRoot);
                instance.name = "ShopScreen";
                screen = instance.GetComponent<UIScreen>();
            }
            screen.gameObject.SetActive(true);
            screen.Root.SetActive(false);

            var managerData = new SerializedObject(root.GetComponent<InGameUIManager>());
            SerializedProperty screens = managerData.FindProperty("_screenInstances");
            bool registered = false;
            for (int i = 0; i < screens.arraySize; i++)
                registered |= screens.GetArrayElementAtIndex(i).objectReferenceValue == screen;
            if (!registered)
            {
                int index = screens.arraySize;
                screens.arraySize++;
                screens.GetArrayElementAtIndex(index).objectReferenceValue = screen;
                managerData.ApplyModifiedPropertiesWithoutUndo();
                RecordOverride(root.GetComponent<InGameUIManager>());
            }
            RecordOverride(screen.gameObject);
            RecordOverride(screen.Root);
            return screen;
        }

        private static Transform RequirePopupRoot(GameObject root)
        {
            Transform popupRoot = root.transform.Find("PopupCanvas/PopupRoot");
            if (popupRoot == null || root.GetComponentsInChildren<Canvas>(true).Length != 2)
                throw new InvalidOperationException("기존 HUD/Popup Canvas 두 개와 PopupRoot를 확인하세요.");
            return popupRoot;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform));
            var rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static UnityEngine.UI.Image AddImage(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 position,
            string value, int fontSize, Color color, TMP_Text fontSource,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = fontSource.font;
            label.fontSharedMaterial = fontSource.fontSharedMaterial;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.color = color;
            label.raycastTarget = false;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = value;
            return label;
        }

        private static UnityEngine.UI.Button CreateButton(string name, Transform parent, Vector2 size,
            Vector2 position, string value, TMP_Text fontSource, out TMP_Text text)
        {
            RectTransform rect = Rect(name, parent, size, position);
            UnityEngine.UI.Image background = AddImage(rect, Gold, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.disabledColor = new Color(.5f, .5f, .5f, .55f);
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            text = Label("Label", rect, size - new Vector2(16, 4), Vector2.zero,
                value, 21, Ink, fontSource);
            return button;
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("필요한 에셋이 없습니다: " + path);

        private static void SetReference(Object target, string field, Object value)
        {
            if (target == null || value == null) throw new InvalidOperationException("연결 참조가 없습니다: " + field);
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field)
                ?? throw new InvalidOperationException("필드가 없습니다: " + field);
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            RecordOverride(target);
        }

        private static void SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field)
                ?? throw new InvalidOperationException("배열 필드가 없습니다: " + field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RecordOverride(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
#endif
