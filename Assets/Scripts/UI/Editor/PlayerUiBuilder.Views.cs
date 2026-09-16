using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static partial class PlayerUiBuilder
    {
        private static GameUIController CreateHud(GameFlowController flow, WaveController waves, RunCurrencyManager currency,
            out CoreRunDecisionBinding decision, out TMP_Text hint, out Button build, out RuntimeUnitCountHud counts,
            out GameObject resultRoot, out GameObject decisionRoot)
        {
            var owner = CanvasRoot("Player HUD", 10); var ui = owner.AddComponent<GameUIController>();
            var top = Box(owner.transform, "TopBar", 32, 28, 1856, 82, Ink);
            Icon(top, "Crest", PlayerUiIcon.Symbol.Gate, 22, 17, 46, Gold);
            Text(top, "GameTitle", "마계의 관문", 86, 14, 300, 52, 28);
            var quarter = Text(top, "Quarter", "", 710, 8, 370, 30, 18, Muted);
            quarter.alignment = TextAlignmentOptions.Center;
            var wave = Text(top, "Wave", "", 710, 36, 370, 32, 26); wave.alignment = TextAlignmentOptions.Center;
            Icon(top, "GoldIcon", PlayerUiIcon.Symbol.Coin, 1644, 25, 32, Gold);
            var gold = Text(top, "Gold", "", 1690, 18, 134, 44, 28, Gold);
            var bottom = Box(owner.transform, "BottomBar", 32, 946, 1856, 104, Ink);
            var phase = Text(bottom, "Phase", "", 26, 17, 155, 35, 24, Mint);
            hint = Text(bottom, "Hint", "", 26, 56, 760, 30, 19, Muted);
            build = Btn(bottom, "Build", "건설", 1376, 25, 160, 56);
            var start = Btn(bottom, "WaveStart", "웨이브 시작", 1552, 25, 276, 56, true);
            var countBox = Box(owner.transform, "UnitCounts", 32, 128, 360, 48, Ink);
            var ally = Text(countBox, "AllyCount", "", 16, 6, 160, 36, 18, Mint);
            var enemy = Text(countBox, "EnemyCount", "", 190, 6, 160, 36, 18, Gold);
            counts = owner.AddComponent<RuntimeUnitCountHud>(); Assign(counts, "_allyText", ally, "_enemyText", enemy);
            var waveCard = Overlay(owner, "WaveReward", 480, 240, out var waveRoot);
            Text(waveCard, "Title", "방어 성공", 32, 24, 416, 48, 30);
            var waveReward = Text(waveCard, "Reward", "", 32, 80, 416, 42, 24, Gold);
            var proceed = Btn(waveCard, "Continue", "계속", 32, 152, 416, 56, true);
            var result = Overlay(owner, "RunResult", 560, 340, out resultRoot);
            Icon(result, "VictoryCrest", PlayerUiIcon.Symbol.Shield, 40, 38, 58, Gold);
            var resultTitle = Text(result, "Title", "방어 성공", 118, 42, 390, 56, 36);
            var runReward = Text(result, "Reward", "", 40, 128, 480, 60, 26, Gold);
            var restart = Btn(result, "Restart", "다시 도전", 40, 244, 480, 56, true);
            var msg = Overlay(owner, "Message", 460, 210, out var messageRoot);
            var message = Text(msg, "Message", "", 32, 32, 396, 140, 24, Paper, true);
            Assign(ui, "_goldText", gold, "_waveText", wave, "_phaseText", phase, "_waveStartButton", start,
                "_waveRewardPanel", waveRoot, "_waveRewardText", waveReward, "_continueButton", proceed,
                "_runResultPanel", resultRoot, "_runResultTitleText", resultTitle, "_runRewardText", runReward,
                "_restartButton", restart, "_messagePanel", messageRoot, "_messageText", message);
            waveRoot.SetActive(false); resultRoot.SetActive(false); messageRoot.SetActive(false);
            var core = owner.AddComponent<CoreHudBinding>(); Assign(core, "_ui", ui, "_flow", flow, "_waves", waves, "_quarterText", quarter);
            Assign(owner.AddComponent<RunGoldHudBinding>(), "_ui", ui, "_currencyManager", currency);
            var dc = Overlay(owner, "QuarterDecision", 580, 306, out decisionRoot);
            Text(dc, "Title", "분기 돌파", 32, 26, 516, 48, 32);
            var desc = Text(dc, "Description", "", 32, 96, 516, 80, 23, Muted, true);
            var finish = Btn(dc, "Finish", "승리로 종료", 32, 216, 246, 56);
            var next = Btn(dc, "Continue", "계속 도전", 298, 216, 250, 56, true);
            decision = owner.AddComponent<CoreRunDecisionBinding>();
            Assign(decision, "_flow", flow, "_waves", waves, "_panelRoot", decisionRoot, "_descriptionText", desc,
                "_finishButton", finish, "_continueButton", next);
            decisionRoot.SetActive(false); owner.SetActive(true); return ui;
        }

        private static GameObject CreateBoard(out Button[] plots, out Button camp, out Button unit)
        {
            var owner = CanvasRoot("Battlefield Backdrop - UI layout preview", 0);
            var area = Box(owner.transform, "Playfield", 0, 0, 1920, 1080, new Color32(26, 34, 35, 255));
            area.GetComponent<Image>().raycastTarget = false;
            // A restrained diagrammatic backdrop, not a replacement for the team's map or spawning code.
            for (int i = 0; i < 15; i++)
                Box(area, "GroundLine" + i, 60, 190 + i * 48, 1800, 1, new Color32(35, 44, 44, 255)).GetComponent<Image>().raycastTarget = false;
            for (int i = 0; i < 34; i++)
                Box(area, "GroundColumn" + i, 64 + i * 54, 186, 1, 696, new Color32(35, 44, 44, 255)).GetComponent<Image>().raycastTarget = false;
            var road = Box(area, "Approach", 100, 467, 1720, 148, new Color32(46, 51, 45, 255)); road.GetComponent<Image>().raycastTarget = false;
            Box(area, "GateWallNorth", 930, 190, 16, 262, Tile).GetComponent<Image>().raycastTarget = false;
            Box(area, "GateWallSouth", 930, 632, 16, 234, Tile).GetComponent<Image>().raycastTarget = false;
            Icon(area, "Gate", PlayerUiIcon.Symbol.Gate, 880, 479, 116, Gold);
            Text(area, "GateCaption", "관문", 880, 634, 120, 40, 20, Muted).alignment = TextAlignmentOptions.Center;
            Icon(area, "Portal", PlayerUiIcon.Symbol.Spark, 1660, 482, 118, new Color32(172, 144, 209, 255));
            Text(area, "PortalCaption", "차원문", 1626, 634, 188, 40, 20, Muted).alignment = TextAlignmentOptions.Center;
            camp = Btn(area, "BaseCamp", "", 168, 469, 148, 148);
            Icon(camp.transform, "CampIcon", PlayerUiIcon.Symbol.Tower, 32, 23, 84, Gold);
            Text(camp.transform, "Caption", "베이스캠프", 8, 108, 132, 34, 18).alignment = TextAlignmentOptions.Center;
            plots = new Button[4];
            for (int i = 0; i < plots.Length; i++)
            {
                plots[i] = Btn(area, "BuildPlot" + i, "", 422 + (i % 2) * 206, 304 + (i / 2) * 354, 160, 138);
                Icon(plots[i].transform, "Plus", PlayerUiIcon.Symbol.Plus, 59, 30, 42, Mint);
                Text(plots[i].transform, "Caption", "건설", 8, 89, 144, 32, 19, Muted).alignment = TextAlignmentOptions.Center;
            }
            unit = Btn(area, "Warrior", "", 1132, 486, 90, 108);
            Icon(unit.transform, "UnitIcon", PlayerUiIcon.Symbol.Shield, 24, 14, 42, Mint);
            Text(unit.transform, "Caption", "전사", 6, 70, 78, 30, 18).alignment = TextAlignmentOptions.Center;
            owner.SetActive(true); return owner;
        }

        private static BuildingCatalogPanel CreateCatalog()
        {
            var owner = CanvasRoot("Player Building Catalog", 50);
            var popup = Popup(owner, "건설", 620, 440, out var card);
            var panel = owner.AddComponent<BuildingCatalogPanel>(); Assign(panel, "_popup", popup);
            Text(card, "Subtitle", "마계의 방어를 준비하세요", 24, 60, 572, 30, 19, Muted);
            var fields = new SerializedObject(panel); var rows = fields.FindProperty("_cards"); rows.arraySize = 4;
            var symbols = new[] { PlayerUiIcon.Symbol.Shield, PlayerUiIcon.Symbol.Bow, PlayerUiIcon.Symbol.Coin, PlayerUiIcon.Symbol.Spark };
            for (int i = 0; i < 4; i++)
            {
                var button = Btn(card, "CatalogItem" + i, "", 24 + i % 2 * 292, 106 + i / 2 * 142, 280, 130);
                Icon(button.transform, "Icon", symbols[i], 14, 17, 38, Gold);
                var name = Text(button.transform, "Name", "", 66, 15, 200, 32, 23);
                var summary = Text(button.transform, "Summary", "", 14, 56, 252, 34, 17, Muted);
                var price = Text(button.transform, "Price", "", 14, 96, 252, 24, 18, Gold);
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Button").objectReferenceValue = button;
                row.FindPropertyRelative("Name").objectReferenceValue = name;
                row.FindPropertyRelative("Summary").objectReferenceValue = summary;
                row.FindPropertyRelative("Price").objectReferenceValue = price;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prev = Btn(card, "Previous", "‹", 24, 394, 40, 32); var next = Btn(card, "Next", "›", 556, 394, 40, 32);
            var page = Text(card, "Page", "", 240, 394, 140, 32, 18, Muted); page.alignment = TextAlignmentOptions.Center;
            var empty = Text(card, "Empty", "건설할 수 있는 건물이 없습니다", 40, 176, 540, 84, 22, Muted, true);
            Assign(panel, "_previous", prev, "_next", next, "_pageText", page, "_emptyText", empty);
            owner.SetActive(true); SaveView(owner, "PlayerBuildingCatalog"); return panel;
        }

        private static BuildingInfoPanel CreateBuilding(out BuildingActionPanel actions)
        {
            var owner = CanvasRoot("Player Building Info", 50);
            var popup = Popup(owner, "건물 정보", 440, 486, out var card);
            var content = Box(card, "Content", 0, 70, 440, 480, Color.clear); content.GetComponent<Image>().raycastTarget = false;
            var panel = owner.AddComponent<BuildingInfoPanel>(); actions = owner.AddComponent<BuildingActionPanel>();
            var empty = Text(card, "Empty", "", 0, 0, 1, 1);
            var name = Text(content, "Name", "", 24, 0, 320, 40, 28);
            var category = Text(content, "Category", "", 24, 42, 268, 30, 18, Mint);
            var level = Text(content, "Level", "", 330, 42, 86, 30, 18, Muted);
            var icon = Box(content, "Icon", 356, 0, 44, 40, Paper).GetComponent<Image>(); icon.raycastTarget = false;
            var placeholder = Icon(content, "Placeholder", PlayerUiIcon.Symbol.Tower, 362, 0, 38, Gold);
            var production = Text(content, "Production", "", 24, 86, 392, 32, 21);
            var detailsButton = Btn(content, "DetailsButton", "자세히", 288, 132, 128, 38);
            var details = Box(content, "Details", 24, 410, 392, 158, Tile); details.GetComponent<Image>().raycastTarget = false;
            var description = Text(details, "Description", "", 12, 8, 368, 90, 20, Paper, true);
            var effect = Text(details, "Effect", "", 12, 98, 368, 40, 18, Mint, true);
            var scroll = Scroll(details, description, effect);
            Assign(popup, "_detailsRoot", details.gameObject, "_detailsButton", detailsButton,
                "_detailsLabel", detailsButton.GetComponentInChildren<TMP_Text>()); Value(popup, "_expandedHeight", 666f);
            Assign(panel, "_playerPopup", popup, "_emptyState", empty.gameObject, "_contentPanel", content.gameObject,
                "_nameText", name, "_categoryText", category, "_levelText", level, "_icon", icon, "_iconPlaceholder", placeholder.gameObject,
                "_closeButton", card.Find("Close").GetComponent<Button>(), "_detailsScroll", scroll,
                "_descriptionText", description, "_productionText", production, "_effectText", effect);
            Value(panel, "_productionFormat", "생산 · {0}");
            var status = Text(content, "ActionStatus", "", 24, 384, 392, 32, 17, Muted);
            // Each action keeps its existing request contract. Absent offers disappear in this compact view.
            var af = new SerializedObject(actions);
            int index = 0;
            foreach (var key in new[] { "_build", "_upgrade", "_dismantle" })
            {
                var row = Box(content, key, 24, 182 + index * 66, 392, 64, Color.clear); row.GetComponent<Image>().raycastTarget = false;
                var button = Btn(row, "Action", key == "_build" ? "건설" : key == "_upgrade" ? "강화" : "해체", 230, 0, 162, 42, true);
                var quote = Text(row, "Quote", "", 0, 0, 220, 36, 20, Gold);
                var reason = Text(row, "Reason", "", 0, 40, 392, 24, 16, Muted);
                var p = af.FindProperty(key); p.FindPropertyRelative("_rowRoot").objectReferenceValue = row.gameObject;
                p.FindPropertyRelative("_button").objectReferenceValue = button;
                p.FindPropertyRelative("_quote").objectReferenceValue = quote;
                p.FindPropertyRelative("_reason").objectReferenceValue = reason;
                index++;
            }
            af.ApplyModifiedPropertiesWithoutUndo();
            var compactLayout = owner.AddComponent<PlayerBuildingPopupLayout>();
            Assign(compactLayout, "_popup", popup, "_status", status.rectTransform, "_details", details);
            Array(compactLayout, "_rows", new UnityEngine.Object[] { content.Find("_build"), content.Find("_upgrade"), content.Find("_dismantle") });
            // Actions get a private title field so clearing them never overwrites the selected building's name.
            var actionTitle = Text(owner.transform, "ActionTitleState", "", 0, 0, 1, 1); actionTitle.gameObject.SetActive(false);
            Assign(actions, "_title", actionTitle, "_status", status, "_playerPopup", popup); Value(actions, "_compactPresentation", true);
            content.gameObject.SetActive(false); empty.gameObject.SetActive(false); details.gameObject.SetActive(false);
            owner.SetActive(true); SaveView(owner, "PlayerBuildingInfo"); return panel;
        }

        private static UnitInfoPanel CreateUnitInfo()
        {
            var owner = CanvasRoot("Player Unit Info", 50); var popup = Popup(owner, "병력 정보", 400, 286, out var card);
            var content = Box(card, "Content", 0, 70, 400, 430, Color.clear); content.GetComponent<Image>().raycastTarget = false;
            var panel = owner.AddComponent<UnitInfoPanel>();
            var name = Text(content, "Name", "", 24, 0, 260, 40, 28);
            var role = Text(content, "Role", "", 24, 44, 352, 30, 19, Muted);
            var faction = Text(content, "Faction", "", 294, 6, 82, 30, 17, Mint);
            var hp = Text(content, "Health", "", 24, 92, 240, 30, 21);
            var state = Text(content, "HealthState", "", 272, 92, 104, 30, 17, Mint);
            var track = Box(content, "HealthTrack", 24, 128, 352, 8, Tile);
            var fill = Box(track, "HealthFill", 0, 0, 352, 8, Mint); Stretch(fill); fill.GetComponent<Image>().raycastTarget = false;
            var more = Btn(content, "DetailsButton", "자세히", 248, 158, 128, 36);
            var details = Box(content, "Details", 24, 222, 352, 180, Tile); details.GetComponent<Image>().raycastTarget = false;
            var description = Text(details, "Description", "", 12, 8, 328, 58, 18, Muted, true);
            var combat = Text(details, "Combat", "", 12, 70, 328, 90, 18, Paper, true);
            var traits = Text(details, "Traits", "", 12, 162, 328, 32, 17, Mint, true);
            var scroll = Scroll(details, description, combat, traits);
            var hidden = Box(owner.transform, "UnusedOptionalFields", 0, 0, 1, 1, Color.clear);
            var empty = Text(hidden, "Empty", "", 0, 0, 1, 1);
            var icon = Box(hidden, "Icon", 0, 0, 1, 1, Paper).GetComponent<Image>();
            var placeholder = Text(hidden, "Placeholder", "", 0, 0, 1, 1);
            hidden.gameObject.SetActive(false);
            Assign(panel, "_playerPopup", popup, "_emptyState", empty.gameObject, "_contentPanel", content.gameObject,
                "_nameText", name, "_factionText", faction, "_roleText", role, "_healthText", hp, "_healthStateText", state, "_healthFill", fill,
                "_icon", icon, "_iconPlaceholder", placeholder.gameObject, "_closeButton", card.Find("Close").GetComponent<Button>(),
                "_detailsScroll", scroll, "_descriptionText", description, "_combatText", combat, "_traitText", traits);
            Assign(popup, "_detailsButton", more, "_detailsRoot", details.gameObject,
                "_detailsLabel", more.GetComponentInChildren<TMP_Text>()); Value(popup, "_expandedHeight", 500f);
            content.gameObject.SetActive(false); details.gameObject.SetActive(false); owner.SetActive(true); SaveView(owner, "PlayerUnitInfo"); return panel;
        }

        private static ScrollRect Scroll(RectTransform viewport, params TMP_Text[] labels)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.GetComponent<Image>().raycastTarget = true;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            var go = new GameObject("ScrollableContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = (RectTransform)go.transform; rect.SetParent(viewport, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
            var layout = go.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(12, 12, 10, 10); layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var label in labels)
            {
                label.transform.SetParent(rect, false); label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow; label.alignment = TextAlignmentOptions.TopLeft;
            }
            scroll.content = rect; return scroll;
        }

        private static ArtifactRewardPanel CreateReward()
        {
            var owner = CanvasRoot("Player Victory Reward", 100);
            var card = Overlay(owner, "RewardOverlay", 780, 540, out var root);
            Icon(card, "VictoryCrest", PlayerUiIcon.Symbol.Shield, 28, 28, 44, Gold);
            Text(card, "Title", "방어 성공", 88, 25, 400, 48, 32);
            var reward = Text(card, "Reward", "", 28, 82, 724, 36, 22, Gold);
            var instruction = Text(card, "Instruction", "유물 하나를 선택하세요", 28, 134, 724, 32, 20, Muted);
            var panel = owner.AddComponent<ArtifactRewardPanel>(); var so = new SerializedObject(panel);
            // UI-only provisional names for developer-named fixtures. IDs, effects, prices and team assets remain unchanged.
            var names = new[] { "전쟁의 문장", "수호의 문장", "전리품 주머니", "전사의 맹세", "풍요의 인장", "혈석 부적", "강철의 계약", "황금빛 약속", "생명의 균형" };
            var aliases = so.FindProperty("_displayNames"); aliases.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                aliases.GetArrayElementAtIndex(i).FindPropertyRelative("ArtifactId").stringValue = "artifact_" + (i + 1).ToString("000");
                aliases.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayName").stringValue = names[i];
            }
            var cards = so.FindProperty("_cards"); cards.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var button = Btn(card, "Candidate" + i, "", 28 + i * 248, 184, 228, 194);
                var selection = Box(button.transform, "Selection", 0, 0, 228, 4, Gold); selection.GetComponent<Image>().raycastTarget = false;
                var icon = Box(button.transform, "Icon", 90, 26, 48, 48, Paper).GetComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
                var placeholder = Icon(button.transform, "MissingIcon", PlayerUiIcon.Symbol.Spark, 90, 26, 48, Gold);
                var name = Text(button.transform, "Name", "", 14, 94, 200, 52, 23, Paper, true); name.alignment = TextAlignmentOptions.Center;
                var rarity = Text(button.transform, "Rarity", "", 14, 153, 200, 30, 18, Mint); rarity.alignment = TextAlignmentOptions.Center;
                var effect = Text(button.transform, "EffectState", "", 0, 0, 1, 1); effect.gameObject.SetActive(false);
                var row = cards.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Button").objectReferenceValue = button; row.FindPropertyRelative("Name").objectReferenceValue = name;
                row.FindPropertyRelative("Rarity").objectReferenceValue = rarity; row.FindPropertyRelative("Effect").objectReferenceValue = effect;
                row.FindPropertyRelative("Icon").objectReferenceValue = icon; row.FindPropertyRelative("MissingIcon").objectReferenceValue = placeholder.gameObject;
                row.FindPropertyRelative("Selection").objectReferenceValue = selection.gameObject;
                selection.gameObject.SetActive(false);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var status = Text(card, "SelectedEffect", "", 28, 392, 724, 54, 20, Paper, true);
            var previous = Btn(card, "Previous", "‹", 28, 466, 40, 46); var next = Btn(card, "Next", "›", 166, 466, 40, 46);
            var page = Text(card, "Page", "", 72, 466, 90, 46, 18, Muted); page.alignment = TextAlignmentOptions.Center;
            var clear = Btn(card, "Clear", "선택 해제", 248, 466, 168, 46);
            var confirm = Btn(card, "Confirm", "유물 없이 계속", 436, 462, 316, 54, true);
            Assign(panel, "_panelRoot", root, "_rewardText", reward, "_statusText", status, "_instructionText", instruction,
                "_pageText", page, "_previousPageButton", previous, "_nextPageButton", next, "_clearButton", clear,
                "_confirmButton", confirm, "_confirmText", confirm.GetComponentInChildren<TMP_Text>());
            Value(panel, "_compactPresentation", true); root.SetActive(false); owner.SetActive(true); SaveView(owner, "PlayerVictoryReward"); return panel;
        }
    }
}
