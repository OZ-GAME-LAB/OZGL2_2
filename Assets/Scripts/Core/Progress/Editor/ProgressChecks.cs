#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Core.Editor
{
    // 기존 사용자 저장과 분리된 디렉터리에서 실행하는 회귀 검사입니다.
    public static class ProgressChecks
    {
        public static string Status { get; private set; }
        private static readonly List<string> Results = new List<string>();
        public static UnlockData[] Definitions() => new[] { "Abundance", "Conquest", "Arcane", "Guardian" }
            .Select(n => AssetDatabase.LoadAssetAtPath<UnlockData>(ProgressSetup.Folder + "/Unlock_" + n + ".asset")).ToArray();

        public static void Check(bool condition, string name)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + name);
            if (!condition) throw new Exception(name);
        }
        private static void Reject(Action action, string name)
        {
            bool failed = false;
            try { action(); } catch (ArgumentException) { failed = true; } catch (OverflowException) { failed = true; }
            Check(failed, name);
        }

        public static string Run()
        {
            Results.Clear();
            var holder = new GameObject("ProgressChecks");
            holder.SetActive(false);
            try
            {
                var unlocks = holder.AddComponent<UnlockManager>();
                var serializedUnlocks = new SerializedObject(unlocks);
                var definitionArray = serializedUnlocks.FindProperty("_definitions");
                var definitions = Definitions();
                definitionArray.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++)
                    definitionArray.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                serializedUnlocks.ApplyModifiedPropertiesWithoutUndo();
                unlocks.Initialize();
                var unconfigured = holder.AddComponent<UnlockManager>();
                Reject(() => unconfigured.Initialize(), "정의가 없는 MonoBehaviour 초기화 거부");
                Check(!unconfigured.IsInitialized, "초기화 실패 시 준비 완료로 처리하지 않음");
                Object.DestroyImmediate(unconfigured);
                var records = PlayerRecordSaveData.CreateEmpty();
                var initial = unlocks.CaptureSaveData();
                Check(initial.Ids.SequenceEqual(new[] { "altar.abundance" }), "풍요 기본 해금");
                records.TotalDamage = 4999;
                records.EnemyKills = records.SkillKills = 499;
                Check(unlocks.Evaluate(records, initial).Ids.Count == 1, "피해 4999 / 스킬 499 미해금");
                records.TotalDamage = 5000;
                records.EnemyKills = records.SkillKills = 500;
                var earned = unlocks.Evaluate(records, initial);
                Check(earned.Ids.Contains("altar.conquest") && earned.Ids.Contains("altar.arcane"), "피해 5000 / 스킬 500 해금");
                Check(!earned.Ids.Contains("altar.guardian"), "분기 도착만으로 수호 해금하지 않음");
                records.ClearedBossQuarters.Add(4);
                earned = unlocks.Evaluate(records, earned);
                Check(earned.Ids.Contains("altar.guardian"), "4분기 보스 클리어 해금");
                Check(unlocks.Evaluate(PlayerRecordSaveData.CreateEmpty(), earned).Ids.Count == 4, "해금 ID 영구 유지");
                unlocks.RestoreSaveData(earned);
                unlocks.Initialize();
                Check(unlocks.CaptureSaveData().Ids.Count == 4, "반복 초기화가 복원된 해금을 유지");
                Reject(() => new PlayerRecordSaveData { Version = 1, EnemyKills = -1 }.Validate(), "음수 횟수 거부");
                Reject(() => new PlayerRecordSaveData { Version = 1, TotalDamage = double.NaN }.Validate(), "NaN 피해 거부");
                Reject(() => new PlayerRecordSaveData { Version = 1, TotalDamage = double.PositiveInfinity }.Validate(), "무한 피해 거부");
                Reject(() => new PlayerRecordSaveData { Version = 1, ClearedBossQuarters = null }.Validate(), "새 기록 목록 누락 거부");
                var missing = JsonUtility.FromJson<PersistentSaveData>("{\"ProgressVersion\":1}");
                Reject(() => missing.Records.Validate(), "JSON 누락이 빈 객체로 복원되어도 기록 거부");
                Reject(() => missing.Unlocks.Validate(), "JSON 누락이 빈 객체로 복원되어도 해금 거부");
                var manager = new PlayerRecordManager();
                manager.RestoreSaveData(new PlayerRecordSaveData { Version = 1, EnemyKills = long.MaxValue });
                Reject(() => manager.AddRun(new RunSummary(new RunStats { EnemyKillCount = 1 }, null)), "횟수 오버플로 거부");
                manager.RestoreSaveData(new PlayerRecordSaveData { Version = 1, TotalDamage = double.MaxValue });
                var excessive = new RunStats(); excessive.Records.TotalDamage = double.MaxValue;
                Reject(() => manager.AddRun(new RunSummary(excessive, null)), "피해 합계 오버플로 거부");

                var run = new RunStats { EnemyKillCount = 500, ClearWaveCount = 5, ClearBossCount = 1 };
                run.Records.TotalDamage = 5000;
                run.Records.HighestHitDamage = 100;
                run.Records.HighestWaveDamage = 2000;
                run.Records.SkillKills = 500;
                run.Records.ClearedBossQuarters.Add(4);
                var summary = new RunSummary(run, null);
                run.Records.ClearedBossQuarters.Clear(); run.Records.TotalDamage = 0;
                Check(summary.CaptureRecords().TotalDamage == 5000 && summary.CaptureRecords().ClearedBossQuarters.Contains(4), "종료 기록 목록과 수치 복사");
                var archive = holder.AddComponent<RunRecordManager>();
                var legacy = new ArchiveSaveData(new RunStats { EnemyKillCount = 17, ClearWaveCount = 2 });
                legacy.ProgressVersion = 0; legacy.Records = null;
                archive.RestoreSaveData(legacy);
                Check(archive.CaptureSaveData().Records.EnemyKills == 17 && archive.CaptureSaveData().Records.TotalDamage == 0, "이전 런 저장 기존 집계 보존");
                var checkpoint = new ArchiveSaveData(new RunStats { CurrentWaveDamage = 120,
                    Records = new PlayerRecordSaveData { Version = 1, TotalDamage = 200, HighestHitDamage = 80, HighestWaveDamage = 120 } });
                archive.RestoreSaveData(JsonUtility.FromJson<ArchiveSaveData>(JsonUtility.ToJson(checkpoint)));
                Check(archive.CaptureSaveData().CurrentWaveDamage == 120, "체크포인트 피해 기록 JSON 왕복");
                checkpoint.Records = null;
                Reject(() => archive.RestoreSaveData(checkpoint), "새 런 저장 필수 기록 누락 거부");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OutGame/OutGameSetup.prefab");
                var root = Object.Instantiate(prefab, holder.transform);
                var save = root.GetComponentInChildren<SaveManager>(true);
                string directory = Path.Combine(Path.GetTempPath(), "OZGL_ProgressChecks", Guid.NewGuid().ToString("N"));
                save.ConfigureDirectory(directory);
                var coordinator = root.GetComponentInChildren<OutGameSaveCoordinator>(true);
                var wallet = root.GetComponentInChildren<PersistentCurrencyManager>(true);
                var traits = root.GetComponentInChildren<OutGameTraitController>(true);
                var altar = root.GetComponentInChildren<OutGameAltarController>(true);
                var totems = root.GetComponentInChildren<OutGameTotemController>(true);
                var sceneUnlocks = root.GetComponentInChildren<UnlockManager>(true);
                sceneUnlocks.Initialize();
                wallet.Initialize(); altar.Initialize(sceneUnlocks); traits.Initialize(wallet, coordinator); totems.Initialize();
                coordinator.Initialize(save, traits, wallet, altar, totems, root.GetComponentInChildren<Game.Archive.ArchiveController>(true), sceneUnlocks);
                Check(coordinator.TryLoadOrCreate(out string error), "최초 영구 저장: " + error);
                var locked = coordinator.CaptureSaveData();
                locked.Altar.SelectedAltar = AltarId.Guardian;
                Check(!coordinator.TrySave(locked, out error), "잠긴 제단 선택 저장 거부");
                var oldSave = coordinator.CaptureSaveData();
                oldSave.ProgressVersion = 0; oldSave.Records = null; oldSave.Unlocks = null;
                Check(save.TrySave(OutGameSaveCoordinator.SaveKey, oldSave, out error) && coordinator.TryLoad(out error), "구버전 영구 저장 변환: " + error);
                Check(coordinator.Records.CaptureSaveData().EnemyKills == 0 && sceneUnlocks.IsUnlocked("altar.abundance"), "이전 프로필 빈 기록 / 풍요 유지");
                var provider = new SummaryProvider { Summary = summary };
                var ui = new ResultStub();
                var bridge = new RunSettlementSaveBridge(coordinator, () => "progress-test-run", ui, _ => UniTask.CompletedTask,
                    provider, coordinator.Records, sceneUnlocks);
                var next = bridge.CaptureSaveData();
                int before = next.Wallet.Amount;
                next.Wallet = new PersistentWalletSaveData(before + 30);
                string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "not a directory");
                save.ConfigureDirectory(blocked);
                Check(!bridge.TrySave(next, out error), "실제 파일 저장 실패 검출");
                Check(coordinator.Records.CaptureSaveData().TotalDamage == 0 && !sceneUnlocks.IsUnlocked("altar.conquest") && wallet.CaptureSaveData().Amount == before,
                    "저장 실패 시 기록·해금·지갑 불변");
                save.ConfigureDirectory(directory);
                next = bridge.CaptureSaveData(); next.Wallet = new PersistentWalletSaveData(before + 30);
                Check(bridge.TrySave(next, out error), "저장 재시도 성공: " + error);
                // 실제 지급 경로와 같은 저장 성공 후 지갑 반영.
                wallet.RestoreSaveData(next.Wallet, false);
                Check(save.TryLoad(OutGameSaveCoordinator.SaveKey, out var saved, out error) && saved.Wallet.Amount == wallet.CaptureSaveData().Amount &&
                    saved.Records.TotalDamage == 5000 && saved.LastSettledRunId == "progress-test-run" && saved.Unlocks.Ids.Count == 4,
                    "혈석·기록·해금·영수증 동일 파일 저장");
                Check(!bridge.TrySave(bridge.CaptureSaveData(), out error) && coordinator.Records.CaptureSaveData().EnemyKills == 500,
                    "중복 정산 요청 재누적 거부");
                Check(coordinator.TryLoad(out error) && altar.TrySelect(AltarId.Guardian), "해금 복원 후 제단 선택");
                Check(coordinator.TrySave(out error) && coordinator.TryCompleteTutorial(true, out error), "제단·튜토리얼 일반 저장 경로");
                Check(save.TryLoad(OutGameSaveCoordinator.SaveKey, out saved, out error) && saved.Records.TotalDamage == 5000 && saved.Unlocks.Ids.Count == 4,
                    "일반 저장에서 진행 기록 보존");
                saved.Records = null;
                Check(!coordinator.TrySave(saved, out error), "새 영구 저장 기록 누락 거부");
                Check(coordinator.TryLoad(out error) && altar.SelectedAltar == AltarId.Guardian, "제단 선택 저장·불러오기");
                // 실제 구매 코드도 전체 저장 사본에서 특성/지갑만 변경하는지 확인합니다.
                var funded = coordinator.CaptureSaveData(); funded.Wallet.Amount = 100000;
                Check(coordinator.TrySave(funded, out error), "구매 검사 잔액 준비");
                wallet.RestoreSaveData(funded.Wallet, false);
                var purchasable = traits.Data.First(t => traits.CanUpgrade(t.Id, out _));
                Check(traits.TryUpgrade(purchasable.Id, out error) && save.TryLoad(OutGameSaveCoordinator.SaveKey, out saved, out error) &&
                    saved.Records.TotalDamage == 5000 && saved.Unlocks.Ids.Count == 4 && saved.LastSettledRunId == "progress-test-run",
                    "실제 특성 구매 후 기록·해금·영수증 보존");
                Status = string.Join("\n", Results);
            }
            catch (Exception exception) { Status = string.Join("\n", Results) + "\nERROR " + exception; }
            finally { Object.DestroyImmediate(holder); }
            Directory.CreateDirectory("PersonalDocs/ProgressValidation");
            File.WriteAllText("PersonalDocs/ProgressValidation/data-checks.txt", Status);
            return Status;
        }

        private sealed class SummaryProvider : ISummary
        {
            public RunSummary Summary;
            public void CompleteRun() { }
            public bool TryGetRunSummary(out RunSummary summary) { summary = Summary; return summary != null; }
        }
        private sealed class ResultStub : IRunSettlementUI
        {
            public UniTask ShowAndWaitAsync(RunSummary summary, RunResult result, CancellationToken token) => UniTask.CompletedTask;
        }
    }
}
#endif
