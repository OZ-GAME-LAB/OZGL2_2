using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 임시 메모리 객체만 사용합니다. 씬/에셋/실제 저장 파일은 변경하지 않습니다.
public static class ManagerSaveValidation
{
    private static int _checks;
    [MenuItem("Tools/Save/Validate Economy Manager Snapshots")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        _checks = 0;
        var assets = new List<UnityEngine.Object>();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("SaveValidation (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); assets.Add(a); return a; }
            var gold = Asset<CurrencyData>(); Set(gold, "_id", "save_gold"); Set(gold, "_type", CurrencyType.Gold); Set(gold, "_lifetime", CurrencyLifetime.Run);
            var gem = Asset<CurrencyData>(); Set(gem, "_id", "save_gem"); Set(gem, "_type", CurrencyType.Gem); Set(gem, "_lifetime", CurrencyLifetime.Run);
            var currencies = Asset<CurrencyCatalog>(); Set(currencies, "_currencies", new List<CurrencyData> { gold, gem }); Call(currencies, "RebuildDicionary");
            var money = root.AddComponent<RunCurrencyManager>();
            Set(money, "_currencyCatalog", currencies);
            var wallet = new CurrencyWallet(CurrencyLifetime.Run); wallet.TryRegister(gold); wallet.TryRegister(gem); Set(money, "_wallet", wallet);
            var moneyData = new RunCurrencySaveData {
                Balances = new List<RunCurrencySaveEntry> { new() { Type = CurrencyType.Gold, Amount = 123 }, new() { Type = CurrencyType.Gem, Amount = 7 } },
                HasPreparedReward = true, PreparedQuarter = 1, PreparedWave = 1, WaveRewardApplied = true,
                PreparedRewards = new List<RunCurrencySaveEntry> { new() { Type = CurrencyType.Gold, Amount = 30 } }
            };
            money.RestoreSaveData(Clone(moneyData));
            money.RestoreSaveData(Clone(moneyData));
            Check(money.GetBalance(CurrencyType.Gold) == 123 && money.GetBalance(CurrencyType.Gem) == 7, "재화 반복 복원은 가산하지 않음");
            Check(money.CaptureSaveData().WaveRewardApplied && money.CurrentGoldReward == 30, "보상 지급 완료/예정량 보존");
            var badMoney = Clone(moneyData); badMoney.Balances[1].Amount = -1;
            Reject(() => money.RestoreSaveData(badMoney), "음수 잔액 거부");
            Equal(moneyData, money.CaptureSaveData(), "재화 실패 시 기존 상태 유지");
            var capturedMoney = money.CaptureSaveData(); capturedMoney.Balances[0].Amount = 999;
            Check(money.GetBalance(CurrencyType.Gold) == 123, "저장 객체와 런타임 잔액 분리");

            var effects = root.AddComponent<EffectManager>();
            var artifact = Asset<ArtifactData>(); Set(artifact, "_id", "save_artifact"); Set(artifact, "_maxStacks", 3);
            object slotEffect = new ConsumableSlotEffectData(); Set(slotEffect, "_additionalSlots", 1);
            Set(artifact, "_consumableSlotEffects", new List<ConsumableSlotEffectData> { (ConsumableSlotEffectData)slotEffect });
            var catalog = Asset<ArtifactCatalog>(); Set(catalog, "_artifacts", new List<ArtifactData> { artifact }); Call(catalog, "OnValidate");
            var artifacts = root.AddComponent<ArtifactManager>(); Set(artifacts, "_artifactCatalog", catalog); Set(artifacts, "_rewardTable", Asset<ArtifactRewardTable>());
            artifacts.Initialize(root.AddComponent<WaveController>(), effects);
            var artifactData = new ArtifactSaveData {
                Owned = new List<ArtifactSaveEntry> { new() { ArtifactId = artifact.Id, StackCount = 2 } },
                HasRewardCandidates = true, RewardQuarter = 1, RewardWave = 1, CandidateIds = new List<string> { artifact.Id }
            };
            object otherSource = new object();
            Check(effects.TrySetEffects(otherSource, new EffectDataGroup
            {
                SlotEffects = new List<ConsumableSlotEffectData> { (ConsumableSlotEffectData)slotEffect }
            }), "다른 출처 효과 준비");
            int effectEvents = 0; effects.EffectsChanged += () => effectEvents++;
            artifacts.RestoreSaveData(Clone(artifactData));
            artifacts.RestoreSaveData(Clone(artifactData));
            Check(artifacts.Instances.Count == 1 && artifacts.Instances[0].StackCount == 2, "아티팩트 반복 복원 중첩 유지");
            Check(effects.ConsumableSlotAdjustment == 3 && effectEvents == 2, "다른 출처 유지/한 번 알림/효과 중복 방지");
            Equal(artifactData, artifacts.CaptureSaveData(), "보상 후보/순서 복원");
            var badArtifacts = Clone(artifactData); badArtifacts.CandidateIds[0] = "missing";
            Reject(() => artifacts.RestoreSaveData(badArtifacts), "미등록 후보 거부");
            Equal(artifactData, artifacts.CaptureSaveData(), "아티팩트 실패 시 기존 상태 유지");
            Check(effects.ConsumableSlotAdjustment == 3, "검증 실패 시 효과 유지");

            var item = Asset<ConsumableItemData>(); Set(item, "_id", "save_item");
            var itemCatalog = Asset<ConsumableItemCatalog>(); Set(itemCatalog, "_items", new List<ConsumableItemData> { item }); Call(itemCatalog, "OnValidate");
            var items = root.AddComponent<ConsumableItemManager>(); Set(items, "_itemCatalog", itemCatalog); Set(items, "_inventory", new ConsumableItemInventory());
            var itemData = new ConsumableItemSaveData { Capacity = 5, SlotItemIds = new List<string> { item.Id, "", item.Id, "", "" } };
            items.RestoreSaveData(Clone(itemData)); items.RestoreSaveData(Clone(itemData));
            Equal(itemData, items.CaptureSaveData(), "동일 아이템/빈칸/위치/용량 보존");
            var overflow = new ConsumableItemSaveData { Capacity = 1, SlotItemIds = new List<string> { item.Id, "", item.Id } };
            items.RestoreSaveData(Clone(overflow));
            Equal(overflow, items.CaptureSaveData(), "초과 슬롯 위치 보존");
            Check(items.HasOverflow && !items.HasEmptySlot, "초과 상태 구매 공간 없음");
            var badItems = Clone(overflow); badItems.SlotItemIds[2] = "missing";
            Reject(() => items.RestoreSaveData(badItems), "미등록 아이템 거부");
            Equal(overflow, items.CaptureSaveData(), "아이템 실패 시 기존 상태 유지");

            var shop = root.AddComponent<ShopManager>(); Set(shop, "_shopTable", Asset<ShopTable>()); shop.Initialize(artifacts, money, items);
            var shopData = new ShopSaveData {
                HasStock = true,
                Artifacts = new List<ShopPurchaseSaveEntry> { new() { ItemId = artifact.Id, Currency = CurrencyType.Gold, Price = 91, Purchased = true } },
                Consumables = new List<ShopPurchaseSaveEntry> { new() { ItemId = item.Id, Currency = CurrencyType.Gem, Price = 4, Purchased = false } },
                Exchanges = new List<ShopExchangeSaveEntry> { new() { ArtifactId = artifact.Id, Exchanged = true } }
            };
            shop.RestoreSaveData(Clone(shopData)); shop.RestoreSaveData(Clone(shopData));
            Equal(shopData, shop.CaptureSaveData(), "상점 가격/구매/교환 상태 보존");
            Check(!shop.TryPurchase(shop.PurchaseSlots[0]), "구매 완료 상품 재구매 차단");
            Check(money.GetBalance(CurrencyType.Gold) == 123 && items.ItemCount == 2, "상점 복원은 결제/지급하지 않음");
            var badShop = Clone(shopData); badShop.Consumables[0].Price = -1;
            Reject(() => shop.RestoreSaveData(badShop), "음수 상품 가격 거부");
            Equal(shopData, shop.CaptureSaveData(), "상점 실패 시 기존 상태 유지");
            shop.RestoreSaveData(new ShopSaveData { HasStock = true });
            Check(shop.HasStock && shop.PurchaseSlots.Count == 0, "매진/빈 생성 상점을 미생성으로 바꾸지 않음");
            shop.RestoreSaveData(new ShopSaveData());
            Check(!shop.HasStock, "미생성 상점 복원");
            items.RestoreSaveData(new ConsumableItemSaveData());
            Check(items.Capacity == 0 && items.SlotCount == 0, "0칸 인벤토리 복원");
            var completed = Clone(artifactData); completed.RewardApplied = true; completed.CandidateIds.Clear();
            artifacts.RestoreSaveData(completed);
            Equal(completed, artifacts.CaptureSaveData(), "선택 완료 상태 복원");
            artifacts.RestoreSaveData(new ArtifactSaveData());
            Check(artifacts.Instances.Count == 0 && effects.ConsumableSlotAdjustment == 1, "빈 보유 복원은 아티팩트 효과만 제거");
            Debug.Log($"[ManagerSaveValidation] {_checks}개 검증 통과 (메모리/JSON 왕복, 파일·씬 변경 없음)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }
    private static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static void Equal<T>(T expected, T actual, string label) => Check(JsonUtility.ToJson(expected) == JsonUtility.ToJson(actual), label);
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); _checks++; }
    private static void Reject(Action action, string label) { try { action(); } catch (ArgumentException) { _checks++; return; } throw new InvalidOperationException(label); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
