using System;
using System.Linq;
using Game.UI.InGame;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.UI.Editor
{
    /// <summary>기존 건물 화면의 루트와 닫기 동작을 보존하며 통합 패널을 연결한다.</summary>
    public static class BuildingPanelSetup
    {
        private static readonly Color Panel = new Color32(29, 36, 40, 255);
        private static readonly Color Tile = new Color32(39, 48, 51, 255);
        private static readonly Color Paper = new Color32(235, 230, 211, 255);
        private static readonly Color Muted = new Color32(156, 171, 166, 255);
        private static readonly Color Gold = new Color32(222, 184, 105, 255);
        private static readonly Color Mint = new Color32(117, 203, 174, 255);
        private static TMP_FontAsset _font;

        // 호출자가 비활성 프리팹 내용을 전달하고 저장한다. 이 메서드는 씬이나 에셋을 저장하지 않는다.
        public static void Configure(GameObject uiRoot)
        {
            if (uiRoot.activeInHierarchy)
                throw new InvalidOperationException("비활성 UI 루트에서 Configure를 실행하세요.");

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Data/UI/Player/PlayerUIFont.asset");
            var screens = uiRoot.GetComponentsInChildren<UIScreen>(true);
            var panel = screens.Single(screen => screen.Id == UIId.BuildingCatalog);
            var detail = screens.Single(screen => screen.Id == UIId.BuildingInfo);
            var presenter = uiRoot.GetComponent<BuildingUIPresenter>();
            var catalog = panel.GetComponent<BuildingCatalogView>();
            var detailInfo = detail.GetComponent<BuildingInfoView>();
            var actions = uiRoot.GetComponentsInChildren<BuildingActionView>(true).Single();
            foreach (var layout in uiRoot.GetComponentsInChildren<BuildingPopupLayout>(true)) layout.enabled = false;

            var window = ConfigureWindow(panel, true);
            var title = window.Find("PopupTitle").GetComponent<TMP_Text>();
            title.text = "건물 건설";
            var body = Child(window, "UnifiedContent");
            Stretch(body, 20, 92, 20, 58);
            var wallet = Text(window, "Wallet", "골드 0   ·   보석 0", 21, Gold);
            Top(wallet.rectTransform, 20, 58, 390, 28);
            var feedback = Text(window, "Feedback", "", 18, Gold);
            Bottom(feedback.rectTransform, 20, 14, 390, 36);

            var catalogRoot = Child(body, "Catalog");
            Stretch(catalogRoot);
            var tabs = new UnityEngine.UI.Button[3];
            string[] tabNames = { "전투", "자원", "연구" };
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = Button(catalogRoot, "Tab" + i, tabNames[i]);
                Top((RectTransform)tabs[i].transform, i * 133, 0, 124, 44);
                var colors = tabs[i].colors;
                colors.disabledColor = new Color(1.45f, 1.35f, 1.08f, 1);
                tabs[i].colors = colors;
            }
            var catalogScroll = Scroll(catalogRoot, "Buildings", out var catalogContent);
            Stretch((RectTransform)catalogScroll.transform, 0, 56, 0, 0);
            Grid(catalogContent, 186, true);
            var catalogTemplate = Card(catalogContent, "CardTemplate", 186);
            var empty = Text(catalogRoot, "Empty", "건설 가능한 건물이 없습니다.", 21, Muted);
            Top(empty.rectTransform, 0, 80, 390, 64);
            Reference(catalog, "_popup", panel);
            Reference(catalog, "_cardTemplate", catalogTemplate);
            Reference(catalog, "_content", catalogContent);
            Reference(catalog, "_scroll", catalogScroll);
            Reference(catalog, "_emptyText", empty);
            var catalogFields = new SerializedObject(catalog);
            var tabField = catalogFields.FindProperty("_tabs");
            tabField.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++) tabField.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            catalogFields.ApplyModifiedPropertiesWithoutUndo();

            var managementRoot = Child(body, "Management");
            Stretch(managementRoot);
            Vertical(managementRoot, false, 14);
            var managementScroll = Scroll(managementRoot, "Information", out var managementContent);
            Stretch((RectTransform)managementScroll.transform);
            var scrollSize = Component<UnityEngine.UI.LayoutElement>(managementScroll.transform);
            scrollSize.minHeight = 0;
            scrollSize.preferredHeight = 0;
            scrollSize.flexibleHeight = 1;
            Vertical(managementContent, true, 14);
            var currentHeading = Child(managementContent, "CurrentHeading");
            Height(currentHeading, 38);
            var currentLabel = Text(currentHeading, "Label", "현재 건물", 20, Gold);
            Top(currentLabel.rectTransform, 0, 0, 272, 36);
            var currentInfoButton = Button(currentHeading, "Details", "상세");
            Top((RectTransform)currentInfoButton.transform, 286, 0, 96, 36);
            var current = Info(managementContent, "Current", panel, managementScroll, true);
            Text(managementContent, "UpgradesTitle", "업그레이드", 24, Gold);
            var upgradeHint = Text(managementContent, "UpgradeHint", "업그레이드할 건물을 선택하세요.", 18, Muted);
            var upgradeContent = Child(managementContent, "UpgradeCandidates");
            Grid(upgradeContent, 186);
            var upgradeTemplate = Card(upgradeContent, "CardTemplate", 186);
            var comparisonRoot = Child(managementContent, "Comparison");
            Vertical(comparisonRoot, false, 12);
            Text(comparisonRoot, "Heading", "업그레이드 후", 24, Gold);
            var comparison = Info(comparisonRoot, "Information", panel, managementScroll, false);
            // 비교 정보를 스크롤해도 실행 비용과 버튼은 하단에 남긴다.
            var oldActions = managementContent.Find("Actions");
            if (oldActions != null) oldActions.gameObject.SetActive(false);
            var actionRoot = Child(managementRoot, "Actions");
            Vertical(actionRoot, false, 10);
            var actionTitle = Text(actionRoot, "Title", "", 20, Paper);
            actionTitle.gameObject.SetActive(false);
            var status = Text(actionRoot, "Status", "", 18, Gold);
            Reference(actions, "_title", actionTitle);
            Reference(actions, "_status", status);
            ActionRow(actionRoot, actions, "_build", "건설", "건설 비용", false);
            ActionRow(actionRoot, actions, "_upgrade", "업그레이드", "업그레이드 비용", false);
            ActionRow(actionRoot, actions, "_dismantle", "철거", "철거 시 환급", true);
            Boolean(actions, "_compactPresentation", true);
            managementRoot.gameObject.SetActive(false);

            var detailWindow = ConfigureWindow(detail, false);
            detailWindow.Find("PopupTitle").GetComponent<TMP_Text>().text = "건물 상세";
            var detailBody = Child(detailWindow, "UnifiedContent");
            Stretch(detailBody, 20, 80, 20, 70);
            var detailScroll = Scroll(detailBody, "Information", out var detailContent);
            Stretch((RectTransform)detailScroll.transform);
            Vertical(detailContent, true, 14);
            Info(detailContent, "Building", detail, detailScroll, true, detailInfo);
            var previewHint = Text(detailContent, "PreviewHint", "업그레이드 경로", 20, Gold);
            var previewContent = Child(detailContent, "UpgradeCandidates");
            Grid(previewContent, 206);
            var previewTemplate = Card(previewContent, "CardTemplate", 206);
            var back = Button(detailWindow, "PreviewBack", "이전 건물");
            Bottom((RectTransform)back.transform, 20, 18, 148, 40);
            back.gameObject.SetActive(false);

            Reference(presenter, "_panel", panel);
            Reference(presenter, "_catalogRoot", catalogRoot.gameObject);
            Reference(presenter, "_managementRoot", managementRoot.gameObject);
            Reference(presenter, "_titleText", title);
            Reference(presenter, "_walletText", wallet);
            Reference(presenter, "_feedbackText", feedback);
            Reference(presenter, "_catalog", catalog);
            Reference(presenter, "_info", current);
            Reference(presenter, "_currentInfoButton", currentInfoButton);
            Reference(presenter, "_comparison", comparison);
            Reference(presenter, "_comparisonRoot", comparisonRoot.gameObject);
            Reference(presenter, "_upgradeHint", upgradeHint);
            Reference(presenter, "_upgradeContent", upgradeContent);
            Reference(presenter, "_upgradeTemplate", upgradeTemplate);
            Reference(presenter, "_actions", actions);
            Reference(presenter, "_detailScreen", detail);
            Reference(presenter, "_detailInfo", detailInfo);
            Reference(presenter, "_previewContent", previewContent);
            Reference(presenter, "_previewTemplate", previewTemplate);
            Reference(presenter, "_previewHint", previewHint);
            Reference(presenter, "_detailBack", back);
            // UnitSkillPopupSetup에서 이 뷰를 생성한 뒤 실행하면 함께 연결된다.
            Reference(presenter, "_unitPopup", uiRoot.GetComponentInChildren<UnitPopupView>(true));
            EditorUtility.SetDirty(uiRoot);
        }

        private static RectTransform ConfigureWindow(UIScreen screen, bool left)
        {
            // 배경 버튼을 Window의 부모에 두면 창 내부의 빈 곳을 눌러도 닫힌다.
            // 형제로 배치하여 창 바깥을 누른 경우에만 닫기 이벤트를 받는다.
            screen.Root.GetComponent<UnityEngine.UI.Button>().enabled = false;
            screen.Root.GetComponent<CloseUtility>().enabled = false;
            screen.Root.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var outside = Child(screen.Root.transform, "OutsideClickArea");
            Stretch(outside);
            outside.SetAsFirstSibling();
            var outsideImage = Component<UnityEngine.UI.Image>(outside);
            outsideImage.color = Color.clear;
            outsideImage.raycastTarget = true;
            var outsideButton = Component<UnityEngine.UI.Button>(outside);
            outsideButton.targetGraphic = outsideImage;
            outsideButton.transition = UnityEngine.UI.Selectable.Transition.None;
            var close = Component<CloseUtility>(outside);
            Reference(close, "_button", outsideButton);
            Reference(close, "_screen", screen);

            var window = (RectTransform)screen.Root.transform.Find("Window");
            foreach (Transform child in window)
                if (child.name != "Close" && child.name != "PopupTitle" && child.name != "TopAccent" && child.name != "UnifiedContent")
                    child.gameObject.SetActive(false);

            window.anchorMin = window.anchorMax = new Vector2(left ? 0 : .5f, .5f);
            window.pivot = new Vector2(left ? 0 : .5f, .5f);
            window.anchoredPosition = new Vector2(left ? 24 : 0, 0);
            window.sizeDelta = new Vector2(left ? 430 : 470, 840);
            window.GetComponent<UnityEngine.UI.Image>().color = Panel;
            window.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            float width = window.sizeDelta.x;
            var title = window.Find("PopupTitle").GetComponent<TMP_Text>();
            title.font = _font;
            title.fontSize = 28;
            title.color = Paper;
            // 기존 Ellipsis 설정은 한글 글꼴의 줄 높이가 조금만 커도 제목 전체를 생략한다.
            title.overflowMode = TextOverflowModes.Overflow;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            Top(title.rectTransform, 20, 14, width - 94, 44);
            Top((RectTransform)window.Find("Close"), width - 58, 18, 38, 38);
            Top((RectTransform)window.Find("TopAccent"), 0, 0, width, 4);
            Boolean(screen, "_canCloseByUser", true);
            Boolean(screen, "_blocksHudInput", true);
            Boolean(screen, "_closeWhenCovered", false);
            return window;
        }

        private static BuildingInfoView Info(Transform parent, string name, UIScreen screen,
            UnityEngine.UI.ScrollRect scroll, bool resetScroll, BuildingInfoView existing = null)
        {
            var owner = Child(parent, name);
            Vertical(owner, false, 0);
            var view = existing != null ? existing : Component<BuildingInfoView>(owner);
            var display = Child(owner, "DisplayContent");
            Vertical(display, false, 10);
            var heading = Child(display, "Heading");
            Height(heading, 76);
            var iconRoot = Child(heading, "Icon");
            Top(iconRoot, 0, 5, 58, 58);
            var icon = Component<UnityEngine.UI.Image>(iconRoot);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var placeholder = Child(heading, "Placeholder");
            Top(placeholder, 0, 5, 58, 58);
            var tower = Component<UIIcon>(placeholder);
            tower.Kind = UIIcon.Symbol.Tower;
            tower.color = Gold;
            tower.raycastTarget = false;
            var label = Text(heading, "Name", "건물 이름", 25, Paper);
            Top(label.rectTransform, 72, 0, 304, 40);
            var category = Text(heading, "Category", "건물 분류", 18, Mint);
            Top(category.rectTransform, 72, 44, 304, 28);
            var level = Text(heading, "Level", "", 18, Muted);
            level.gameObject.SetActive(false);
            var description = Text(display, "Description", "", 20, Paper);
            var production = Text(display, "Production", "", 21, Mint);
            var effect = Text(display, "Effect", "", 20, Mint);
            var cost = Text(display, "Cost", "", 19, Gold);
            var unit = Button(display, "Unit", "유닛 정보 보기");
            Height((RectTransform)unit.transform, 42);
            var empty = Text(owner, "Empty", "건물을 선택하세요.", 20, Muted);
            empty.gameObject.SetActive(false);
            Reference(view, "_emptyState", empty.gameObject);
            Reference(view, "_contentPanel", display.gameObject);
            Reference(view, "_nameText", label);
            Reference(view, "_categoryText", category);
            Reference(view, "_levelText", level);
            Reference(view, "_icon", icon);
            Reference(view, "_iconPlaceholder", placeholder.gameObject);
            Reference(view, "_detailsScroll", scroll);
            Reference(view, "_descriptionText", description);
            Reference(view, "_productionText", production);
            Reference(view, "_effectText", effect);
            Reference(view, "_costText", cost);
            Reference(view, "_unitButton", unit);
            Reference(view, "_unitText", unit.GetComponentInChildren<TMP_Text>(true));
            Reference(view, "_playerPopup", screen);
            Boolean(view, "_resetScrollOnSelection", resetScroll);
            return view;
        }

        private static BuildingCardView Card(Transform parent, string name, float width)
        {
            var card = Child(parent, name);
            card.sizeDelta = new Vector2(width, 176);
            var body = Button(card, "Body", "");
            Stretch((RectTransform)body.transform);
            body.transform.Find("Label").gameObject.SetActive(false);
            var slot = Component<UIItemSlot>(body.transform);
            var icon = Component<UnityEngine.UI.Image>(Child(body.transform, "Icon"));
            Top((RectTransform)icon.transform, 12, 12, 32, 32);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var label = Text(body.transform, "Name", "건물 이름", 20, Paper);
            Top(label.rectTransform, 12, 54, width - 24, 48);
            var price = Text(body.transform, "Price", "0 골드", 18, Gold);
            Top(price.rectTransform, 12, 105, width - 24, 26);
            var summary = Text(body.transform, "Summary", "", 16, Muted);
            Top(summary.rectTransform, 12, 135, width - 24, 34);
            var selected = Child(card, "Selected");
            Stretch(selected);
            var outline = Component<UnityEngine.UI.Image>(selected);
            outline.color = new Color(Gold.r, Gold.g, Gold.b, .2f);
            outline.raycastTarget = false;
            selected.gameObject.SetActive(false);
            var info = Button(card, "Info", "i");
            Top((RectTransform)info.transform, width - 52, 8, 44, 40);
            info.GetComponent<UnityEngine.UI.Image>().color = new Color32(57, 69, 72, 255);
            var infoOutline = Component<UnityEngine.UI.Outline>(info.transform);
            infoOutline.effectColor = Gold;
            infoOutline.effectDistance = new Vector2(1, -1);
            var infoLabel = info.GetComponentInChildren<TMP_Text>();
            infoLabel.color = Gold;
            infoLabel.fontSize = 24;
            infoLabel.fontStyle = FontStyles.Bold;
            Reference(slot, "_button", body);
            Reference(slot, "_icon", icon);
            Reference(slot, "_title", label);
            Reference(slot, "_value", price);
            Reference(slot, "_selection", selected.gameObject);
            var view = Component<BuildingCardView>(card);
            Reference(view, "_slot", slot);
            Reference(view, "_icon", icon);
            Reference(view, "_infoButton", info);
            Reference(view, "_summary", summary);
            card.gameObject.SetActive(false);
            return view;
        }

        private static void ActionRow(Transform parent, BuildingActionView actions, string field,
            string caption, string quoteCaption, bool demolition)
        {
            var row = Child(parent, field);
            Vertical(row, false, 5);
            Text(row, "Caption", quoteCaption, 17, demolition ? Muted : Gold);
            var line = Child(row, "Line");
            Height(line, 44);
            var quote = Text(line, "Quote", "", 19, Paper);
            Top(quote.rectTransform, 0, 3, 214, 38);
            var button = Button(line, "Action", caption);
            Top((RectTransform)button.transform, 228, 0, 150, 44);
            if (demolition) button.GetComponent<UnityEngine.UI.Image>().color = new Color32(94, 49, 44, 255);
            var reason = Text(row, "Reason", "", 17, Muted);
            Reference(actions, field + "._rowRoot", row.gameObject);
            Reference(actions, field + "._button", button);
            Reference(actions, field + "._quote", quote);
            Reference(actions, field + "._reason", reason);
        }

        private static UnityEngine.UI.ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            var root = Child(parent, name);
            var scroll = Component<UnityEngine.UI.ScrollRect>(root);
            var viewport = Child(root, "Viewport");
            Stretch(viewport, 0, 0, 8, 0);
            var maskImage = Component<UnityEngine.UI.Image>(viewport);
            maskImage.color = Panel;
            maskImage.raycastTarget = true;
            Component<UnityEngine.UI.Mask>(viewport).showMaskGraphic = false;
            content = Child(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            var barRoot = Child(root, "Scrollbar");
            barRoot.anchorMin = new Vector2(1, 0);
            barRoot.anchorMax = Vector2.one;
            barRoot.pivot = new Vector2(1, .5f);
            barRoot.sizeDelta = new Vector2(5, 0);
            barRoot.anchoredPosition = Vector2.zero;
            var handle = Child(barRoot, "Handle");
            Stretch(handle);
            var handleImage = Component<UnityEngine.UI.Image>(handle);
            handleImage.color = Muted;
            var bar = Component<UnityEngine.UI.Scrollbar>(barRoot);
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private static void Grid(RectTransform content, float cellWidth, bool fitHeight = false)
        {
            var grid = Component<UnityEngine.UI.GridLayoutGroup>(content);
            grid.cellSize = new Vector2(cellWidth, 176);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            if (!fitHeight) return;
            var fit = Component<UnityEngine.UI.ContentSizeFitter>(content);
            fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void Vertical(RectTransform content, bool fitHeight, float spacing)
        {
            var layout = Component<UnityEngine.UI.VerticalLayoutGroup>(content);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (!fitHeight) return;
            var fit = Component<UnityEngine.UI.ContentSizeFitter>(content);
            fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        private static TMP_Text Text(Transform parent, string name, string caption, int size, Color color)
        {
            var text = Component<TextMeshProUGUI>(Child(parent, name));
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.text = caption;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name, string caption)
        {
            var root = Child(parent, name);
            var background = Component<UnityEngine.UI.Image>(root);
            background.color = Tile;
            background.raycastTarget = true;
            var button = Component<UnityEngine.UI.Button>(root);
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f);
            colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.selectedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.disabledColor = new Color(.55f, .55f, .55f);
            button.colors = colors;
            var label = Text(root, "Label", caption, 20, Paper);
            Stretch(label.rectTransform, 6, 4, 6, 4);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var created = new GameObject(name, typeof(RectTransform));
                created.layer = parent.gameObject.layer;
                child = created.transform;
                child.SetParent(parent, false);
            }
            child.gameObject.SetActive(true);
            return (RectTransform)child;
        }

        private static T Component<T>(Transform owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            return component != null ? component : owner.gameObject.AddComponent<T>();
        }

        private static void Height(RectTransform rect, float height)
        {
            var element = Component<UnityEngine.UI.LayoutElement>(rect);
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Bottom(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Reference(UnityEngine.Object target, string path, UnityEngine.Object value)
        {
            var fields = new SerializedObject(target);
            fields.FindProperty(path).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Boolean(UnityEngine.Object target, string path, bool value)
        {
            var fields = new SerializedObject(target);
            fields.FindProperty(path).boolValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
