#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 수동 실행하는 격리 검사. 실제 씬과 저장 파일은 사용하지 않습니다.
public static class SettlementScoreChecks
{
    private static readonly List<string> Results = new();
    public static string Status { get; private set; }
    private static void Check(bool condition, string name)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new Exception(name);
    }
    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        catch (OverflowException) { rejected = true; }
        Check(rejected, name);
    }
    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static CurrencyModifier Mod(float value, CurrencyModifierType type,
        CurrencyType currency = CurrencyType.Bloodstone, CurrencyRewardType reward = CurrencyRewardType.RunSettlement)
        => new CurrencyModifier(null, currency, reward, type, (decimal)value); // 10.22 부동소수점 버그 수정

    public static async UniTask Run()
    {
        Results.Clear();
        Status = "Running";
        var settings = ScriptableObject.CreateInstance<RunScoreSettings>();
        var totem = ScriptableObject.CreateInstance<TotemData>();
        var host = new GameObject("SettlementChecks");
        host.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var totemSO = new SerializedObject(totem);
            totemSO.FindProperty("_rewardBonusPerLevel").floatValue = 10;
            totemSO.ApplyModifiedPropertiesWithoutUndo();
            var stats = new RunStats { ClearWaveCount = 5, ClearBossCount = 1 };
            stats.Artifacts.Add(ArtifactRarity.Common, 2);
            stats.Artifacts.Add(ArtifactRarity.Rare, 1);
            stats.Artifacts.Add(ArtifactRarity.Legendary, 1);
            var summary = new RunSummary(stats, new[] { new ActiveTotem { Data = totem, Level = 3 } });
            var calc = new RunRewardCalculator();
            var result = calc.Calculate(summary, settings, null);
            Check(result.ProgressScore == 100 && result.ArtifactScore == 75 && result.BaseScore == 175, "100 + 75 = 175점");
            Check(result.TotemAdjustedScore == 227.5 && result.FinalBloodstones == 22, "토템 30% → 227.5점, 22혈석");
            var mods = new[] { Mod(2, CurrencyModifierType.Flat), Mod(.2f, CurrencyModifierType.Percent) };
            Check(calc.Calculate(summary, settings, mods).FinalBloodstones == 30, "고정 +2 / +20% → 30혈석");
            Check(calc.Calculate(new RunSummary(stats, null), settings, null).FinalBloodstones == 17, "토템 없음");
            Check(calc.Calculate(new RunSummary(stats, new[] {new ActiveTotem {Data=totem, Level=1}, new ActiveTotem {Data=totem, Level=2}}), settings, null).FinalBloodstones == 22, "여러 토템 보너스 합산");
            Check(calc.Calculate(new RunSummary(new RunStats(), null), settings, null).FinalBloodstones == 0, "빈 기록 0혈석");
            foreach (ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity)))
            {
                var single = new RunStats(); single.Artifacts.Add(rarity, 2);
                Check(calc.Calculate(new RunSummary(single, null), settings, null).ArtifactScore == 2L * settings.GetArtifactScore(rarity), "등급 점수 " + rarity);
            }
            Check(calc.Calculate(summary, settings, new[] { Mod(-100, CurrencyModifierType.Flat), Mod(-2, CurrencyModifierType.Percent) }).FinalBloodstones == 0, "음수 보정의 하한 0");
            Check(calc.Calculate(summary, settings, new[] { Mod(999, CurrencyModifierType.Flat, CurrencyType.Gold), Mod(999, CurrencyModifierType.Flat, reward:CurrencyRewardType.WaveReward) }).FinalBloodstones == 22, "다른 재화/보상 경로 제외");
            Check(calc.Calculate(new RunSummary(stats, new[] { new ActiveTotem { Data = totem, Level = 2 } }), settings, null).FinalBloodstones == 21,
                "중간 버림 제거: 17.5 × 1.2 = 21 (기존 단계별 버림은 20)");
            Reject(() => calc.Calculate(summary, settings, new[] { Mod(float.NaN, CurrencyModifierType.Flat) }), "NaN 거부");
            Reject(() => calc.Calculate(summary, settings, new[] { Mod(float.PositiveInfinity, CurrencyModifierType.Percent) }), "무한 보정 거부");
            SetInt(settings, "_pointsPerBloodstone", 0);
            Reject(() => calc.Calculate(summary, settings, null), "0 환산 기준 거부");
            SetInt(settings, "_pointsPerBloodstone", 10);
            SetInt(settings, "_commonScore", -1);
            Reject(() => calc.Calculate(summary, settings, null), "음수 설정 거부");
            SetInt(settings, "_commonScore", 5);
            Reject(() => calc.Calculate(new RunSummary(new RunStats {ClearWaveCount = -1}, null), settings, null), "음수 진행 기록 거부");
            var invalid = new RunStats(); invalid.Artifacts.Add(ArtifactRarity.Common, -1);
            Reject(() => calc.Calculate(new RunSummary(invalid, null), settings, null), "음수 유물 거부");
            SetInt(settings, "_waveScore", int.MaxValue);
            Reject(() => calc.Calculate(new RunSummary(new RunStats {ClearWaveCount = int.MaxValue}, null), settings, null), "혈석 int 범위 초과 거부");
            SetInt(settings, "_waveScore", 10);
            var huge = new RunStats {ClearWaveCount=int.MaxValue, ClearBossCount=int.MaxValue};
            foreach(ArtifactRarity rarity in Enum.GetValues(typeof(ArtifactRarity))) huge.Artifacts.Add(rarity, int.MaxValue);
            foreach(string field in new[]{"_waveScore","_bossBonusScore","_commonScore","_rareScore","_legendaryScore","_mythicScore"}) SetInt(settings, field, int.MaxValue);
            Reject(() => calc.Calculate(new RunSummary(huge, null), settings, null), "점수 long 합산 오버플로 거부");
            Object.DestroyImmediate(settings);
            settings = ScriptableObject.CreateInstance<RunScoreSettings>();

            var currency = AssetDatabase.LoadAssetAtPath<CurrencyData>("Assets/Data/Economy/S.O/Currency_Bloodstone.asset");
            var oldCalculator = new CurrencyRewardCalculator();
            Check(oldCalculator.CalculateProductionReward(new CurrencyAmount(currency, 10), new[]{Mod(.25f, CurrencyModifierType.Percent, reward:CurrencyRewardType.Production)}).Amount == 12, "기존 생산 보상 버림 유지");
            Check(oldCalculator.CalculateWaveReward(new CurrencyAmount(currency, 5), 3, new[]{Mod(2, CurrencyModifierType.Flat, reward:CurrencyRewardType.WaveReward)}).Amount == 7, "웨이브 보상은 사령부 레벨 배수 없이 고정 보정만 적용");

            var wallet = host.AddComponent<PersistentCurrencyManager>(); wallet.Initialize();
            var manager = host.AddComponent<RunRewardManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_settings").objectReferenceValue = settings;
            serialized.FindProperty("_currencyCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CurrencyCatalog>("Assets/Data/Economy/S.O/CurrencyCatalog.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var writer = new MemoryWriter(wallet) { FailNext = true };
            var ui = new ProbeUI();
            manager.Initialize(null, null, wallet, null, ui, writer);
            int before = wallet.Balance;
            Check(!await manager.TryApplyReward(summary) && wallet.Balance == before && writer.Writes == 0, "저장 실패 시 지급 없음 (의도된 오류 로그)");
            SetInt(settings, "_waveScore", 999);
            ui.FailNext = true;
            Check(!await manager.TryApplyReward(summary), "저장 후 UI 실패 재현 (의도된 오류 로그)");
            Check(wallet.Balance == before + 22 && writer.Writes == 1 && ui.Result.FinalBloodstones == 22, "실패 후 재시도에도 최초 점수표 결과 지급");
            Check(await manager.TryApplyReward(summary) && wallet.Balance == before + 22 && writer.Writes == 1, "UI 재표시 시 중복 지급 없음");
            Check(ui.Result.FinalBloodstones == writer.Saved.Wallet.Amount - before, "화면 전달값 = 저장 증가 = 지갑 증가");

            var second = new RunSummary(new RunStats {ClearWaveCount=1}, null);
            ui.Block = new UniTaskCompletionSource();
            using(var cts = new CancellationTokenSource())
            {
                var pending = manager.TryApplyReward(second, cts.Token);
                Check(!await manager.TryApplyReward(second), "동시 요청 거부 (의도된 오류 로그)");
                int paid = wallet.Balance; int writes = writer.Writes;
                cts.Cancel();
                bool canceled = false;
                try { await pending; } catch(OperationCanceledException) { canceled = true; }
                Check(canceled, "UI 대기 취소 전파");
                ui.Block = null;
                Check(await manager.TryApplyReward(second) && wallet.Balance == paid && writer.Writes == writes, "취소 후 재표시 재지급 없음");
            }
            Status = "Passed";
        }
        catch(Exception e) { Status = "Failed: " + e; Results.Add(Status); }
        finally
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(settings); Object.DestroyImmediate(totem);
            Directory.CreateDirectory("PersonalDocs/UIValidation/SettlementScores");
            File.WriteAllLines("PersonalDocs/UIValidation/SettlementScores/calculation-and-payment.txt", Results);
        }
    }

    private sealed class ProbeUI : IRunSettlementUI
    {
        public RunResult Result;
        public bool FailNext;
        public UniTaskCompletionSource Block;
        public async UniTask ShowAndWaitAsync(RunSummary summary, RunResult result, CancellationToken token)
        {
            Result = result;
            if(FailNext) { FailNext = false; throw new InvalidOperationException("검사용 UI 실패"); }
            if(Block != null) await Block.Task.AttachExternalCancellation(token);
        }
    }
    private sealed class MemoryWriter : IPersistentSaveWriter
    {
        private readonly PersistentCurrencyManager _wallet;
        public bool IsReady => true;
        public bool FailNext;
        public int Writes;
        public PersistentSaveData Saved;
        public MemoryWriter(PersistentCurrencyManager wallet) { _wallet = wallet; }
        public PersistentSaveData CaptureSaveData() => new PersistentSaveData {
            Traits = new TraitSaveData(new List<TraitLevelEntry>()), Wallet = _wallet.CaptureSaveData() };
        public bool TrySave(PersistentSaveData data, out string error)
        {
            if(FailNext) { FailNext = false; error = "검사용 저장 실패"; return false; }
            Saved = data; Writes++; error = null; return true;
        }
    }
}
#endif
