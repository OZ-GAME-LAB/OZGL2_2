using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static partial class PlayerUiBuilder
    {
        /// <summary>명시적인 격리 검증 복사본에서 UI 소유 씬만 변경한다. 기존 씬 GUID는 유지한다.</summary>
        public static void ApplyWireframeBatch()
        {
            if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Use an isolated UnityUIValidation project.");
            _font = GetFont();
            foreach (var path in new[] { ScenePath, RuntimeBuildingUiSetup.ScenePath,
                RuntimeBuildingWorldUiSetup.ScenePath, TeamBuildingUiSetup.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplyWireframe(scene);
                ValidateWireframeReferences(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("UI save failed: " + path);
            }
            AssetDatabase.SaveAssetIfDirty(_font);
            Debug.Log("[UI/Wireframe] Updated four UI-owned scenes; team scenes/settings untouched.");
        }

        private static void ApplyWireframe(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            T Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).SingleOrDefault();
            var hud = Find<GameUIController>();
            if (hud == null) return;
            ApplySettlement(hud);
            var reward = Find<ArtifactRewardPanel>();
            if (reward != null) ApplyRewardLayout(reward);
            var manager = Find<ArtifactManager>();
            // A construction-only scene without an artifact backend must not display a fake inventory.
            if (manager != null && Find<ArtifactInventoryPanel>() == null)
                CreateInventory(hud, manager, reward, Find<PlayerUiNavigation>());
        }

        private static void ApplyRewardLayout(ArtifactRewardPanel panel)
        {
            var card = Ref<GameObject>(panel, "_panelRoot").transform.Find("Window") as RectTransform;
            card.sizeDelta = new Vector2(900, 720);
            Top(card.Find("TopAccent") as RectTransform, 0, 0, 900, 3);
            var title = card.Find("Title").GetComponent<TMP_Text>();
            Top(title.rectTransform, 88, 25, 724, 48); title.alignment = TextAlignmentOptions.Center;
            Top(Ref<TMP_Text>(panel, "_rewardText").rectTransform, 28, 94, 844, 38);
            Ref<TMP_Text>(panel, "_rewardText").alignment = TextAlignmentOptions.Center;
            Top(Ref<TMP_Text>(panel, "_instructionText").rectTransform, 28, 140, 844, 32);
            var cards = new SerializedObject(panel).FindProperty("_cards");
            for (int i = 0; i < cards.arraySize; i++)
            {
                var row = cards.GetArrayElementAtIndex(i);
                var button = (Button)row.FindPropertyRelative("Button").objectReferenceValue;
                Top(button.transform as RectTransform, 28 + i * 284, 184, 264, 350);
                Top(button.transform.Find("Selection") as RectTransform, 0, 0, 264, 4);
                Top(button.transform.Find("Icon") as RectTransform, 86, 22, 92, 92);
                Top(button.transform.Find("MissingIcon") as RectTransform, 86, 22, 92, 92);
                Top(button.transform.Find("Name") as RectTransform, 14, 126, 236, 56);
                Top(button.transform.Find("Rarity") as RectTransform, 14, 190, 236, 28);
                var effect = (TMP_Text)row.FindPropertyRelative("Effect").objectReferenceValue;
                effect.gameObject.SetActive(true); effect.fontSize = 19; effect.color = Paper;
                if (button.transform.Find("EffectViewport") == null)
                {
                    var viewport = Box(button.transform, "EffectViewport", 12, 228, 240, 110, Color.clear);
                    Scroll(viewport, effect);
                }
            }
            Top(Ref<TMP_Text>(panel, "_statusText").rectTransform, 28, 558, 844, 56);
            Top(Ref<Button>(panel, "_previousPageButton").transform as RectTransform, 28, 644, 48, 48);
            Top(Ref<TMP_Text>(panel, "_pageText").rectTransform, 78, 644, 110, 48);
            Top(Ref<Button>(panel, "_nextPageButton").transform as RectTransform, 190, 644, 48, 48);
            ResizeButton(Ref<Button>(panel, "_clearButton"), 278, 640, 224, 54);
            ResizeButton(Ref<Button>(panel, "_confirmButton"), 524, 640, 348, 54);
            Value(panel, "_showCardEffects", true);
            SaveView(panel.gameObject, "PlayerVictoryReward");
        }

        private static void ApplySettlement(GameUIController hud)
        {
            Ref<TMP_Text>(hud, "_runResultTitleText").fontSize = 32;
            if (hud.GetComponent<RunSettlementPanel>() != null) return;
            var root = Ref<GameObject>(hud, "_runResultPanel");
            var card = root.transform.Find("Window") as RectTransform;
            card.sizeDelta = new Vector2(920, 632);
            var accent = card.Find("TopAccent") as RectTransform;
            Top(accent, 0, 0, 920, 3);
            card.Find("VictoryCrest").gameObject.SetActive(false);
            var title = Ref<TMP_Text>(hud, "_runResultTitleText");
            Top(title.rectTransform, 32, 24, 856, 52); title.alignment = TextAlignmentOptions.Center;
            Ref<TMP_Text>(hud, "_runRewardText").gameObject.SetActive(false);
            var notice = Text(card, "SettlementNotice", "", 32, 88, 856, 32, 18, Muted);
            Text(card, "TotemHeading", "토템 배율", 32, 128, 268, 36, 23, Gold);
            Text(card, "ProgressHeading", "진행도 · 획득 유물", 326, 128, 268, 36, 23, Gold);
            Text(card, "ScoreHeading", "총계", 620, 128, 268, 36, 23, Gold);
            var tc = Box(card, "TotemColumn", 32, 174, 268, 234, Tile);
            var totems = Text(tc, "Totems", "", 0, 0, 244, 200, 21, Paper, true); Scroll(tc, totems);
            var pc = Box(card, "ProgressColumn", 326, 174, 268, 234, Tile);
            var progress = Text(pc, "Progress", "", 0, 0, 244, 50, 21, Paper, true);
            var artifacts = Text(pc, "Artifacts", "", 0, 0, 244, 120, 19, Muted, true); Scroll(pc, progress, artifacts);
            var sc = Box(card, "ScoreColumn", 620, 174, 268, 234, Tile);
            Text(sc, "Caption", "종합 점수", 20, 28, 228, 32, 21, Muted);
            var score = Text(sc, "Score", "—", 20, 84, 228, 114, 34, Gold, true);
            var blood = Text(card, "Bloodstones", "", 32, 428, 856, 48, 28, Gold);
            var track = Box(card, "BloodstoneTrack", 32, 494, 856, 12, Tile);
            var fill = Box(track, "Fill", 0, 0, 0, 0, Mint); Stretch(fill); fill.GetComponent<Image>().raycastTarget = false;
            var gauge = Text(card, "BloodstoneGauge", "", 32, 514, 856, 30, 18, Muted);
            var main = Btn(card, "MainMenu", "메인으로 이동", 584, 564, 304, 48, true);
            ResizeButton(Ref<Button>(hud, "_restartButton"), 32, 564, 180, 48);
            var panel = hud.gameObject.AddComponent<RunSettlementPanel>();
            Assign(panel, "_root", root, "_title", title, "_totems", totems, "_progress", progress,
                "_artifacts", artifacts, "_score", score, "_bloodstone", blood, "_gaugeText", gauge,
                "_gaugeFill", fill, "_notice", notice, "_mainButton", main);
            Assign(hud, "_settlementPanel", panel);
            main.interactable = false;
            root.SetActive(false);
        }

        private static void CreateInventory(GameUIController hud, ArtifactManager manager,
            ArtifactRewardPanel reward, PlayerUiNavigation navigation)
        {
            var open = Btn(hud.transform.Find("BottomBar"), "Artifacts", "보유 유물", 1136, 25, 216, 56);
            var owner = CanvasRoot("Player Artifact Inventory", 60);
            var popup = Popup(owner, "보유 유물", 640, 460, out var card);
            var panel = owner.AddComponent<ArtifactInventoryPanel>();
            Assign(panel, "_manager", manager, "_rewardPresentation", reward, "_popup", popup, "_openButton", open);
            var so = new SerializedObject(panel); var slots = so.FindProperty("_slots"); slots.arraySize = 8;
            for (int i = 0; i < 8; i++)
            {
                var button = Btn(card, "OwnedArtifact" + i, "", 24 + i % 4 * 148, 88 + i / 4 * 142, 136, 128);
                var icon = Box(button.transform, "Icon", 36, 14, 64, 64, Paper).GetComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false;
                var missing = Icon(button.transform, "MissingIcon", PlayerUiIcon.Symbol.Spark, 36, 14, 64, Gold);
                var count = Text(button.transform, "Count", "", 10, 86, 116, 30, 19, Paper);
                count.alignment = TextAlignmentOptions.Center;
                var slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("Button").objectReferenceValue = button;
                slot.FindPropertyRelative("Icon").objectReferenceValue = icon;
                slot.FindPropertyRelative("MissingIcon").objectReferenceValue = missing.gameObject;
                slot.FindPropertyRelative("Count").objectReferenceValue = count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var previous = Btn(card, "Previous", "‹", 24, 390, 48, 44);
            var next = Btn(card, "Next", "›", 568, 390, 48, 44);
            var page = Text(card, "Page", "", 220, 390, 200, 44, 19, Muted); page.alignment = TextAlignmentOptions.Center;
            var empty = Text(card, "Empty", "아직 획득한 유물이 없습니다", 32, 172, 576, 84, 23, Muted, true);
            empty.alignment = TextAlignmentOptions.Center;
            var detail = Overlay(owner, "ArtifactDetail", 480, 380, out var detailRoot);
            var name = Text(detail, "Name", "", 28, 20, 424, 60, 26, Paper, true);
            var rarity = Text(detail, "Rarity", "", 28, 82, 424, 32, 20, Mint);
            var viewport = Box(detail, "DescriptionViewport", 28, 130, 424, 152, Tile);
            var description = Text(viewport, "Description", "", 0, 0, 400, 130, 21, Paper, true);
            var scroll = Scroll(viewport, description);
            var close = Btn(detail, "Back", "목록으로", 28, 308, 424, 48);
            Assign(panel, "_previous", previous, "_next", next, "_page", page, "_empty", empty,
                "_detailRoot", detailRoot, "_closeDetail", close, "_name", name, "_rarity", rarity,
                "_description", description, "_descriptionScroll", scroll);
            detailRoot.SetActive(false);
            if (navigation != null)
            {
                var old = new SerializedObject(navigation).FindProperty("_popups");
                var popups = new UnityEngine.Object[old.arraySize + 1];
                for (int i = 0; i < old.arraySize; i++) popups[i] = old.GetArrayElementAtIndex(i).objectReferenceValue;
                popups[popups.Length - 1] = popup;
                Array(navigation, "_popups", popups);
                Assign(navigation, "_artifactInventory", panel);
            }
            owner.SetActive(true);
        }

        private static void ResizeButton(Button button, float x, float y, float width, float height)
        {
            Top(button.transform as RectTransform, x, y, width, height);
            Top(button.GetComponentInChildren<TMP_Text>(true).rectTransform, 10, 0, width - 20, height);
        }

        private static void ValidateWireframeReferences(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script: " + scene.path + "/" + transform.name);
                    foreach (var component in transform.GetComponents<MonoBehaviour>())
                    {
                        var property = new SerializedObject(component).GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var reference = property.objectReferenceValue;
                            if (reference == null && property.objectReferenceInstanceIDValue != 0)
                                throw new InvalidOperationException("Missing reference: " + component.GetType().Name + "." + property.propertyPath);
                            var referencedObject = reference is Component c ? c.gameObject : reference as GameObject;
                            if (referencedObject != null && !EditorUtility.IsPersistent(referencedObject) && referencedObject.scene != scene)
                                throw new InvalidOperationException("Cross-scene reference: " + property.propertyPath);
                        }
                    }
                }
            Debug.Log("[UI/Wireframe] References valid: " + scene.path);
        }
    }
}
