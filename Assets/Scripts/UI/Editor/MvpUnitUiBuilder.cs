using System;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Game.UI.Editor.MvpHudBuilder;

namespace Game.UI.Editor
{
    public static class MvpUnitUiBuilder
    {
        public const string UnitPrefabPath = "Assets/Prefabs/UI/MvpUnitInfo.prefab";
        public const string UnitScenePath = "Assets/Scenes/Test/MvpUnitInfoTest.unity";

        private static readonly Color Panel = new Color32(25, 36, 48, 255);
        private static readonly Color Paper = new Color32(233, 241, 244, 255);
        private static readonly Color Mint = new Color32(121, 229, 195, 255);
        private static readonly Color Muted = new Color32(145, 169, 182, 255);

        [MenuItem("Game/UI/Create Missing Unit Info Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("Create common HUD assets first.");
            EnsureFont();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath) == null) CreatePrefab();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(UnitScenePath) == null) CreateScene(scene);
                Debug.Log("[UI/MvpUnitUiBuilder] Missing unit info assets created; existing assets preserved.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CreatePrefab()
        {
            var root = CreateCanvas("MvpUnitInfo", 5);
            root.SetActive(false);
            try
            {
                var panel = root.AddComponent<UnitInfoPanel>();
                var card = Box(root.transform, "UnitCard", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(-660, -370), new Vector2(-40, 360), Panel);
                Box(card, "Accent", new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero, Mint);
                var empty = Label(card, "EmptyState", "유닛을 선택하세요\n선택한 유닛의 정보가 여기에 표시됩니다.", 27, Muted, 32, 245, 556, 180);
                empty.textWrappingMode = TextWrappingModes.Normal;
                empty.alignment = TextAlignmentOptions.Center;
                var content = Box(card, "Content", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
                content.GetComponent<Image>().raycastTarget = false;
                var faction = Label(content, "Faction", "", 20, Mint, 32, 675, 430, 40);
                var close = Button(content, "Close", "닫기", 100, 44);
                Place((RectTransform)close.transform, 488, 661, 100, 44);
                var name = Label(content, "Name", "", 34, Paper, 32, 576, 430, 82);
                name.textWrappingMode = TextWrappingModes.Normal;
                name.enableAutoSizing = true;
                name.fontSizeMin = 22;
                name.fontSizeMax = 34;
                var role = Label(content, "Role", "", 22, Muted, 32, 523, 430, 44);
                var iconBox = Box(content, "IconFrame", Vector2.zero, Vector2.zero,
                    new Vector2(488, 552), new Vector2(588, 642), new Color32(41, 61, 73, 255));
                var icon = Box(iconBox, "Icon", Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8), Color.white).GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var placeholder = Label(iconBox, "Placeholder", "유닛", 24, Muted, 0, 0, 100, 90);
                placeholder.alignment = TextAlignmentOptions.Center;
                var health = Label(content, "Health", "", 24, Paper, 32, 473, 360, 44);
                var healthState = Label(content, "HealthState", "", 20, Mint, 408, 473, 180, 44);
                healthState.alignment = TextAlignmentOptions.MidlineRight;
                var track = Box(content, "HealthTrack", Vector2.zero, Vector2.zero,
                    new Vector2(32, 447), new Vector2(588, 461), new Color32(45, 65, 77, 255));
                track.GetComponent<Image>().raycastTarget = false;
                var fill = Box(track, "HealthFill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Mint);
                fill.GetComponent<Image>().raycastTarget = false;

                var viewport = Box(content, "Viewport", Vector2.zero, Vector2.zero, new Vector2(32, 70), new Vector2(588, 420), Panel);
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 28;
                var details = new GameObject("Details", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                var rect = (RectTransform)details.transform;
                rect.SetParent(viewport, false);
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = Vector2.zero;
                var layout = details.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 24;
                layout.padding = new RectOffset(0, 12, 0, 8);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                details.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = rect;
                var description = Detail(rect, "Description", Paper);
                var combat = Detail(rect, "Combat", Mint);
                var traits = Detail(rect, "Traits", Mint);
                Label(content, "ScrollHint", "긴 내용은 스크롤해서 확인하세요", 18, Muted, 32, 22, 556, 36);
                Assign(panel, "_emptyState", empty.gameObject, "_contentPanel", content.gameObject,
                    "_nameText", name, "_factionText", faction, "_roleText", role,
                    "_healthText", health, "_healthStateText", healthState, "_healthFill", fill,
                    "_icon", icon, "_iconPlaceholder", placeholder.gameObject, "_closeButton", close,
                    "_detailsScroll", scroll, "_descriptionText", description, "_combatText", combat, "_traitText", traits);
                panel.HideUnitInfo();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, UnitPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static TMP_Text Detail(Transform parent, string name, Color color)
        {
            var text = Label(parent, name, "", 24, color, 0, 0, 544, 60);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static void CreateScene(Scene scene)
        {
            var camera = new GameObject("Unit UI Test Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(15, 22, 31, 255);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var hud = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene)).GetComponent<GameUIController>();
            var panel = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath), scene)).GetComponent<UnitInfoPanel>();
            var controls = CreateCanvas("Unit Info Test Controls - NOT GAMEPLAY", 0);
            controls.SetActive(false);
            var card = Box(controls.transform, "TestCard", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(40, -330), new Vector2(820, 320), Panel);
            Label(card, "Eyebrow", "UI / UNIT     ·     TEST SCENE", 18, Mint, 32, 590, 716, 36);
            Label(card, "Title", "유닛 정보 · 체력 표시 테스트", 34, Paper, 32, 520, 716, 54);
            Label(card, "Description", "임시 수치 · 실제 유닛 생성/전투 없음", 23, Muted, 32, 462, 716, 40);
            var warrior = TestButton(card, "Warrior", "전사 선택", 32, 365);
            var archer = TestButton(card, "Archer", "궁사 선택", 408, 365);
            var enemy = TestButton(card, "Enemy", "적군 선택", 32, 280);
            var longText = TestButton(card, "LongText", "긴 설명", 408, 280);
            var damage = TestButton(card, "Damage", "체력 -25 표시", 32, 195);
            var heal = TestButton(card, "Heal", "체력 +25 표시", 408, 195);
            var remove = TestButton(card, "Remove", "제거 알림", 32, 110);
            var stale = TestButton(card, "Stale", "이전 유닛 알림", 408, 110);
            var status = Label(card, "Status", "", 19, Paper, 32, 20, 716, 72);
            status.textWrappingMode = TextWrappingModes.Normal;
            var sample = controls.AddComponent<MvpUnitInfoSample>();
            Assign(sample, "_panel", panel, "_hud", hud, "_warriorButton", warrior, "_archerButton", archer,
                "_enemyButton", enemy, "_longTextButton", longText, "_damageButton", damage,
                "_healButton", heal, "_removeButton", remove, "_staleButton", stale, "_status", status);
            controls.SetActive(true);
            EditorSceneManager.SaveScene(scene, UnitScenePath);
        }

        private static Button TestButton(Transform parent, string name, string text, float x, float y)
        {
            var button = Button(parent, name, text, 340, 60);
            Place((RectTransform)button.transform, x, y, 340, 60);
            return button;
        }
    }
}
