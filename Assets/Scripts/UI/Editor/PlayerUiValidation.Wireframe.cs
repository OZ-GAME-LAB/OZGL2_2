using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static partial class PlayerUiValidation
    {
        private static async UniTask RunWireframeChecks(GameUIController hud, RunCurrencyManager wallet,
            ArtifactRewardPanel reward, PlayerUiNavigation navigation)
        {
            var inventory = UnityEngine.Object.FindFirstObjectByType<ArtifactInventoryPanel>();
            var manager = UnityEngine.Object.FindFirstObjectByType<ArtifactManager>();
            var settlement = hud.GetComponent<RunSettlementPanel>();
            int gold = wallet.GetBalance(CurrencyType.Gold);
            int stacks = manager.Instances.Sum(entry => entry.StackCount);
            var slots = new SerializedObject(inventory).FindProperty("_slots");
            Check(slots.arraySize == 8, "wireframe inventory uses four columns and two rows");
            var first = slots.GetArrayElementAtIndex(0).FindPropertyRelative("Button").objectReferenceValue as Button;
            for (int i = 0; i < 3; i++)
            {
                PlayerUiBuilder.Ref<Button>(inventory, "_openButton").onClick.Invoke();
                Check(inventory.Popup.IsVisible && inventory.ItemCount == manager.Instances.Count,
                    "owned inventory queries the authoritative manager");
                first.onClick.Invoke();
                Check(inventory.IsDetailVisible && !string.IsNullOrWhiteSpace(PlayerUiBuilder.Ref<TMP_Text>(inventory, "_description").text),
                    "owned icon opens its effect description");
                if (i == 0) await Capture("10-owned-detail", 1280, 720);
                Check(navigation.TryCloseActivePopup() && !inventory.IsDetailVisible && inventory.Popup.IsVisible,
                    "first Escape returns from details to inventory");
                if (i == 0) await Capture("11-owned-inventory", 1920, 1080);
                Check(navigation.TryCloseActivePopup() && !inventory.Popup.IsVisible, "second Escape closes inventory");
            }
            Check(wallet.GetBalance(CurrencyType.Gold) == gold && manager.Instances.Sum(e => e.StackCount) == stacks,
                "inventory reads do not spend currency or change stacks");
            inventory.gameObject.SetActive(false); inventory.gameObject.SetActive(true);
            inventory.Show(); first.onClick.Invoke();
            Check(inventory.IsDetailVisible, "inventory listeners recover after disable/enable");
            PlayerUiBuilder.Ref<Button>(inventory, "_closeDetail").onClick.Invoke();
            Check(!inventory.IsDetailVisible && inventory.Popup.IsVisible, "detail back button retains inventory");

            // Test fixture setup through the real manager API; no definition assets are edited.
            var definitions = AssetDatabase.FindAssets("t:ArtifactData", new[] { "Assets/Data/Artifacts" })
                .Select(id => AssetDatabase.LoadAssetAtPath<ArtifactData>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(data => data != null).OrderBy(data => data.Id).ToArray();
            foreach (var data in definitions)
                if (!manager.Contains(data)) Check(manager.TryAdd(data), "fixture acquisition: " + data.Id);
            Check(inventory.ItemCount > 8, "inventory event updates exceed a single page");
            PlayerUiBuilder.Ref<Button>(inventory, "_next").onClick.Invoke();
            Check(PlayerUiBuilder.Ref<TMP_Text>(inventory, "_page").text == "2 / 2", "inventory next page displays overflow items");
            first.onClick.Invoke();
            Check(inventory.IsDetailVisible, "second-page item opens a detail");
            await Capture("12-owned-second-page-detail", 1280, 720);
            inventory.TryCloseDetail();
            PlayerUiBuilder.Ref<Button>(inventory, "_previous").onClick.Invoke();
            Check(PlayerUiBuilder.Ref<TMP_Text>(inventory, "_page").text == "1 / 2", "inventory previous page returns");
            await Capture("13-owned-grid", 1280, 720);
            first.onClick.Invoke();
            Check(manager.TryEndRun() && inventory.ItemCount == 0 && !inventory.IsDetailVisible,
                "run cleanup clears the inventory and stale detail");
            inventory.Popup.Hide();

            hud.ShowRunResult(true, 100);
            Check(settlement.IsVisible && PlayerUiBuilder.Ref<TMP_Text>(settlement, "_score").text == "—",
                "run result displays pending settlement, not invented score");
            Check(!PlayerUiBuilder.Ref<Button>(settlement, "_mainButton").interactable,
                "main-menu control remains disabled without its owner");
            inventory.Show();
            Check(!inventory.Popup.IsVisible, "required settlement blocks optional inventory");
            await Capture("14-settlement-pending", 1920, 1080);
            Check(wallet.GetBalance(CurrencyType.Gold) == gold, "result display has no currency side effects");

            int requests = 0;
            Action handler = () => requests++;
            settlement.MainMenuRequested += handler;
            settlement.Show(new RunSettlementViewData(true, "불굴의 토템 ×1.2\n침공의 토템 ×1.5",
                "5분기 완료\n보스 5회 격파", "전쟁의 문장\n수호의 문장\n전리품 주머니", 83400, 834, 50, 100));
            Check(PlayerUiBuilder.Ref<TMP_Text>(settlement, "_score").text == 83400L.ToString("N0") &&
                Mathf.Approximately(PlayerUiBuilder.Ref<RectTransform>(settlement, "_gaugeFill").anchorMax.x, .5f),
                "settlement shows provided snapshot and gauge fraction");
            await Capture("15-settlement-fixture", 1920, 1080);
            await Capture("16-settlement-fixture-small", 1280, 720);
            var main = PlayerUiBuilder.Ref<Button>(settlement, "_mainButton");
            main.onClick.Invoke(); main.onClick.Invoke();
            Check(requests == 1 && !main.interactable, "main-menu request rejects duplicate clicks");
            settlement.ResetMainMenuRequest(); main.onClick.Invoke();
            Check(requests == 2, "owner can release failed main-menu request for retry");
            settlement.MainMenuRequested -= handler;
            settlement.Show(new RunSettlementViewData(false, "토템 없음", "진행 종료", "획득 유물 없음",
                long.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
            Check(!main.interactable, "unsubscribed main-menu owner disables control");
            await Capture("17-settlement-large-values", 1280, 720);
            settlement.Show(new RunSettlementViewData(false, null, null, null, 0, 0, 0, 100));
            Check(PlayerUiBuilder.Ref<TMP_Text>(settlement, "_score").text == "0" &&
                PlayerUiBuilder.Ref<RectTransform>(settlement, "_gaugeFill").anchorMax.x == 0, "zero settlement is distinct from missing data");
            RejectSettlement(() => new RunSettlementViewData(true, null, null, null, -1, 0, null, null));
            RejectSettlement(() => new RunSettlementViewData(true, null, null, null, 0, -1, null, null));
            RejectSettlement(() => new RunSettlementViewData(true, null, null, null, 0, 0, 1, 0));
            RejectSettlement(() => new RunSettlementViewData(true, null, null, null, 0, 0, 1, null));
            RejectSettlement(() => new RunSettlementViewData(true, null, null, null, 0, 0, 2, 1));
            hud.HideRunResult();
            Check(!settlement.IsVisible && wallet.GetBalance(CurrencyType.Gold) == gold,
                "settlement fixtures neither pay out nor leave a visible modal");
        }

        private static void RejectSettlement(Func<RunSettlementViewData> create)
        {
            bool rejected = false;
            try { create(); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "invalid settlement values rejected");
        }
    }
}
