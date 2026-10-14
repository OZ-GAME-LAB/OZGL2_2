#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.UI.InGame.Editor
{
    /// <summary>기존 팝업의 프레임과 행을 유지하고 단일 유닛 표시용 참조를 연결한다.</summary>
    public static class UnitSkillPopupSetup
    {
        public static void Configure(GameObject uiRoot)
        {
            UIScreen[] screens = uiRoot.GetComponentsInChildren<UIScreen>(true);
            UIScreen unitScreen = screens.Single(screen => screen.Id == UIId.UnitDetail);
            UIScreen skillScreen = screens.Single(screen => screen.Id == UIId.SkillDetail);
            SkillPopupView skillView = ConfigureSkill(skillScreen);
            ConfigureUnit(unitScreen, skillView);
        }

        public static void ConfigureUnit(UIScreen screen, SkillPopupView skillView)
        {
            Transform panel = screen.Root.transform.Find("Panel");
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 720);
            Place(panel.Find("TopAccent"), 0, 0, 600, 3);
            Place(panel.Find("Close"), 536, 14, 44, 44);
            TMP_Text name = panel.Find("Name").GetComponent<TMP_Text>();
            Place(name.transform, 24, 20, 500, 44);
            name.fontSize = 28;
            TMP_Text subtitle = panel.Find("Lore").GetComponent<TMP_Text>();
            Place(subtitle.transform, 24, 72, 552, 34);
            subtitle.fontSize = 18;
            subtitle.alignment = TextAlignmentOptions.Left;

            Transform rows = panel.Find("UnitRows");
            Place(rows, 24, 122, 552, 64);
            var unitRows = new GameObject[rows.childCount];
            for (int i = 0; i < rows.childCount; i++)
            {
                unitRows[i] = rows.GetChild(i).gameObject;
                unitRows[i].SetActive(i == 0);
            }
            TMP_Text health = rows.Find("UnitRow1/HealthBar/HealthText").GetComponent<TMP_Text>();
            var healthBar = rows.Find("UnitRow1/HealthBar").GetComponent<UnityEngine.UI.Slider>();
            healthBar.interactable = false;
            healthBar.SetValueWithoutNotify(healthBar.maxValue);

            TMP_Text stats = Text(panel, "BaseStats", name, 22);
            Place(stats.transform, 28, 204, 544, 142);
            stats.lineSpacing = 10;
            Transform summary = panel.Find("Summary");
            summary.gameObject.SetActive(false);
            TMP_Text skillsTitle = Text(panel, "SkillsTitle", name, 22);
            skillsTitle.text = "스킬 · 눌러서 상세 확인";
            Place(skillsTitle.transform, 24, 364, 552, 34);

            Transform skills = panel.Find("Skills") ?? panel.Find("SkillsScroll/Viewport/Skills");
            var oldLayout = skills.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (oldLayout != null) Object.DestroyImmediate(oldLayout);
            UnityEngine.UI.ScrollRect scroll = Scroll(panel, "SkillsScroll", skills, 410, 278);
            foreach (Transform child in skills) child.gameObject.SetActive(false);

            Transform template = skills.Find("SkillSlot1");
            UIItemSlot slot = GetOrAdd<UIItemSlot>(template.gameObject);
            TMP_Text skillName = template.Find("Name").GetComponent<TMP_Text>();
            skillName.fontSize = 21;
            skillName.alignment = TextAlignmentOptions.Left;
            skillName.textWrappingMode = TextWrappingModes.NoWrap;
            skillName.overflowMode = TextOverflowModes.Ellipsis;
            Place(skillName.transform, 16, 6, 504, 32);
            TMP_Text skillType = Text(template, "Type", name, 16);
            Place(skillType.transform, 16, 39, 504, 24);
            GetOrAdd<UnityEngine.UI.LayoutElement>(template.gameObject).preferredHeight = 72;
            Set(slot, "_button", template.GetComponent<UnityEngine.UI.Button>());
            Set(slot, "_title", skillName);
            Set(slot, "_value", skillType);
            TMP_Text empty = Text(panel, "EmptySkills", name, 20);
            empty.text = "등록된 스킬이 없습니다.";
            Place(empty.transform, 28, 420, 536, 40);

            UnitPopupView view = GetOrAdd<UnitPopupView>(screen.gameObject);
            Set(view, "_screen", screen);
            Set(view, "_nameText", name);
            Set(view, "_subtitleText", subtitle);
            Set(view, "_healthText", health);
            Set(view, "_statsText", stats);
            SetArray(view, "_unitRows", unitRows);
            SetArray(view, "_groupTotals", new[] { summary.gameObject });
            Set(view, "_skillContent", skills);
            Set(view, "_skillTemplate", slot);
            Set(view, "_emptySkillsText", empty);
            Set(view, "_skillScroll", scroll);
            Set(view, "_skillPopup", skillView);
            KeepParentOpen(screen);
        }

        public static SkillPopupView ConfigureSkill(UIScreen screen)
        {
            Transform panel = screen.Root.transform.Find("Panel");
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 500);
            Place(panel.Find("TopAccent"), 0, 0, 560, 3);
            Place(panel.Find("Close"), 496, 14, 44, 44);
            Transform header = panel.Find("Header");
            Place(header, 24, 26, 472, 116);
            Place(header.Find("IconFrame"), 0, 0, 60, 60);
            TMP_Text name = header.Find("Name").GetComponent<TMP_Text>();
            Place(name.transform, 76, 0, 392, 64);
            name.fontSize = 26;
            name.alignment = TextAlignmentOptions.TopLeft;
            name.enableAutoSizing = true;
            name.fontSizeMin = 18;
            name.fontSizeMax = 26;
            TMP_Text type = header.Find("Type").GetComponent<TMP_Text>();
            Place(type.transform, 0, 80, 500, 40);
            type.fontSize = 18;
            panel.Find("Lore").gameObject.SetActive(false);

            Transform content = panel.Find("DescriptionScroll/Viewport/Content");
            if (content == null) content = Child(panel, "Content");
            TMP_Text effect = (panel.Find("Effect") ?? content.Find("Effect")).GetComponent<TMP_Text>();
            if (effect.transform.parent != content)
                effect.transform.SetParent(content, false);
            effect.fontSize = 22;
            effect.alignment = TextAlignmentOptions.TopLeft;
            effect.lineSpacing = 10;
            effect.raycastTarget = false;
            UnityEngine.UI.ScrollRect scroll = Scroll(panel, "DescriptionScroll", content, 170, 300);

            SkillPopupView view = GetOrAdd<SkillPopupView>(screen.gameObject);
            Set(view, "_screen", screen);
            Set(view, "_nameText", name);
            Set(view, "_typeText", type);
            Set(view, "_effectText", effect);
            Set(view, "_icon", header.Find("IconFrame/Icon").GetComponent<UnityEngine.UI.Image>());
            Set(view, "_emptyIcon", header.Find("IconFrame/EmptyIcon").gameObject);
            Set(view, "_descriptionScroll", scroll);
            KeepParentOpen(screen);
            return view;
        }

        private static UnityEngine.UI.ScrollRect Scroll(Transform panel, string name, Transform content, float top, float height)
        {
            RectTransform root = Child(panel, name);
            float width = panel.GetComponent<RectTransform>().sizeDelta.x - 48;
            Place(root, 24, top, width, height);
            RectTransform viewport = Child(root, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            GetOrAdd<UnityEngine.UI.Image>(viewport.gameObject).color = Color.white;
            GetOrAdd<UnityEngine.UI.Mask>(viewport.gameObject).showMaskGraphic = false;
            if (content.parent != viewport)
                content.SetParent(viewport, false);
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var layout = GetOrAdd<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject);
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            GetOrAdd<UnityEngine.UI.ContentSizeFitter>(content.gameObject).verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = GetOrAdd<UnityEngine.UI.ScrollRect>(root.gameObject);
            scroll.viewport = viewport;
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return scroll;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<RectTransform>();
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static TMP_Text Text(Transform parent, string name, TMP_Text style, float size)
        {
            var text = GetOrAdd<TextMeshProUGUI>(Child(parent, name).gameObject);
            text.font = style.font;
            text.color = style.color;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(Transform target, float left, float top, float width, float height)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, GameObject[] values)
        {
            var data = new SerializedObject(target);
            SerializedProperty array = data.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void KeepParentOpen(UIScreen screen)
        {
            var data = new SerializedObject(screen);
            data.FindProperty("_closeWhenCovered").boolValue = false;
            data.FindProperty("_canCloseByUser").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
