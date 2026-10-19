#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.UI.InGame;
using Units;
using Units.UnitDatas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Core.Editor
{
    [InitializeOnLoad]
    public static class ProgressPlayChecks
    {
        private const string EnabledKey = "ProgressPlayChecks.Enabled";
        private const string DirectoryKey = "ProgressPlayChecks.Directory";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<Object> TemporaryAssets = new List<Object>();
        public static string Status { get; private set; }

        static ProgressPlayChecks()
        {
            SceneManager.sceneLoaded += ConfigureScene;
        }

        public static string Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode가 필요합니다.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            string directory = Path.Combine(Path.GetTempPath(), "OZGL_ProgressPlay", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            SessionState.SetString(DirectoryKey, directory);
            SessionState.SetBool(EnabledKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Test/Test_MainScreen.unity");
            return directory;
        }

        private static void ConfigureScene(Scene scene, LoadSceneMode mode)
        {
            if (!SessionState.GetBool(EnabledKey, false) || !Application.isPlaying) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var save in root.GetComponentsInChildren<SaveManager>(true))
                    save.ConfigureDirectory(SessionState.GetString(DirectoryKey, ""));
            // 원본 동적 글꼴 대신 복사본으로 검증 중 새 글리프를 생성합니다.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (text.font != null)
                    {
                        var clone = Object.Instantiate(text.font);
                        clone.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
                        text.font = clone;
                        TemporaryAssets.Add(clone);
                    }
        }

        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static void Check(bool condition, string name)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + name);
            if (!condition) throw new Exception(name);
        }
        private static async UniTask Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("조건 대기 시간 초과");
                await UniTask.NextFrame();
            }
        }

        public static async UniTask Run()
        {
            Results.Clear();
            Status = "Running";
            try
            {
                Check(SessionState.GetBool(EnabledKey, false) && Application.isPlaying, "격리 Play 검증 준비");
                await Until(() => SceneLoadingManager.Instance != null && !SceneLoadingManager.Instance.IsLoading);
                await SceneLoadingManager.Instance.LoadAsync("Test_OutGame");
                var coordinator = Find<OutGameSaveCoordinator>();
                Check(coordinator.IsReady, "실제 아웃게임 초기화");
                Check(Find<SaveManager>().DirectoryPath == SessionState.GetString(DirectoryKey, ""), "사용자 저장과 분리된 경로");
                var seed = coordinator.CaptureSaveData();
                seed.Tutorial.OutGameHandled = true; seed.Tutorial.InGameHandled = true;
                // 조건 직전의 영구 기록으로 시작하고 실제 피해 적용을 통해 경계를 넘깁니다.
                seed.Records.TotalDamage = 4900;
                seed.Records.EnemyKills = seed.Records.SkillKills = 499;
                Check(coordinator.TrySave(seed, out string error), "경계 직전 검증 데이터 저장: " + error);
                Find<OutGameStartController>().StartRun();
                await Until(() => SceneManager.GetActiveScene().name == "Test_InGame" && Find<BootStrap>() != null &&
                    Find<BootStrap>().IsUIConnected && Find<InGameSaveCoordinator>().IsReady && !SceneLoadingManager.Instance.IsLoading);
                var archive = Find<RunRecordManager>();
                var units = Find<RuntimeUnitManager>();
                var waves = Find<WaveController>();
                var flow = Find<GameFlowController>();
                coordinator = Find<OutGameSaveCoordinator>();
                var artifact = Find<ArtifactManager>();
                archive.Initialize(artifact, units, waves, Find<TotemRunApplier>(), Find<AltarManager>(), Find<TraitRunApplier>());
                archive.Initialize(artifact, units, waves, Find<TotemRunApplier>(), Find<AltarManager>(), Find<TraitRunApplier>());

                var ally = CreateUnit("RecordAlly", UnitTeam.Ally);
                var enemy = CreateUnit("RecordEnemy", UnitTeam.Enemy);
                units.RegisterUnit(ally); units.RegisterUnit(enemy);
                enemy.AddShieldWithResult(20, CombatEventMetadata.Create(enemy));
                enemy.TakeDamageWithResult(new DamageResult(ally, enemy, 30, DamageSourceType.BasicAttack, false));
                var data = archive.CaptureSaveData();
                Check(data.Records.TotalDamage == 30 && data.Records.HighestHitDamage == 30, "HP 10 + 보호막 20 실제 피해 합산 / 재초기화 중복 없음");
                ally.TakeDamageWithResult(new DamageResult(enemy, ally, 10, DamageSourceType.Skill, false));
                Check(archive.CaptureSaveData().Records.TotalDamage == 30, "적이 아군에 준 피해 제외");
                bool nested = false;
                enemy.GetComponent<Unit_Life>().Damaged += _ =>
                {
                    if (nested) return;
                    nested = true;
                    enemy.TakeDamageWithResult(new DamageResult(ally, enemy, 5, DamageSourceType.Dot, false));
                };
                enemy.TakeDamageWithResult(new DamageResult(ally, enemy, 10, DamageSourceType.Skill, false));
                Check(archive.CaptureSaveData().Records.TotalDamage == 45, "중첩 피해 각각 한 번 집계");
                enemy.TakeDamageWithResult(new DamageResult(ally, enemy, 999, DamageSourceType.Skill, false));
                data = archive.CaptureSaveData();
                Check(data.Records.TotalDamage == 120 && data.EnemyKillCount == 1 && data.Records.SkillKills == 1, "초과 피해 제외 / Skill 확정 처치 1회");
                Check(data.Records.HighestHitDamage == 75 && data.Records.HighestWaveDamage == 120, "단일 최고 피해와 웨이브 합계 구분");

                enemy.Core.Initialize(null, null, enemy.RuntimeStatus.UnitData);
                units.RegisterUnit(enemy);
                enemy.TakeDamageWithResult(new DamageResult(ally, enemy, 100, DamageSourceType.Dot, false));
                Check(archive.CaptureSaveData().EnemyKillCount == 2 && archive.CaptureSaveData().Records.SkillKills == 1, "풀 재사용 / Dot 처치 제외");
                enemy.Core.Initialize(null, null, enemy.RuntimeStatus.UnitData);
                units.RegisterUnit(enemy);
                units.UnregisterUnit(enemy); units.RegisterUnit(enemy);
                enemy.TakeDamageWithResult(new DamageResult(ally, enemy, 100, DamageSourceType.BasicAttack, false));
                Check(archive.CaptureSaveData().EnemyKillCount == 3 && archive.CaptureSaveData().Records.TotalDamage == 320 &&
                    archive.CaptureSaveData().Records.SkillKills == 1, "재등록 구독 정리 / 기본 공격 처치 제외");
                units.UnregisterUnit(ally);
                Object.Destroy(ally.gameObject); Object.Destroy(enemy.gameObject);
                // 진행을 건너뛴 실제 테스트 입력과 같은 알림. 자연 클리어 검증과 구분합니다.
                typeof(WaveController).GetMethod("NotifyWaveCleared", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(waves, new object[] { new WaveInfo(4, 5, WaveBattleType.Boss, default) });
                Check(!coordinator.Unlocks.IsUnlocked("altar.conquest") && !coordinator.Unlocks.IsUnlocked("altar.guardian"), "정산 전 영구 해금 불변");
                Check(Find<InGameSaveCoordinator>().TrySave(out error), "종료 전 체크포인트 저장: " + error);
                string runId = flow.RunId;
                int balanceBefore = Find<PersistentCurrencyManager>().Balance;
                flow.AbandonRun();
                await Until(() => Find<RunResultView>() != null && Find<RunResultView>().IsPending);
                var view = Find<RunResultView>();
                Check(coordinator.Records.CaptureSaveData().TotalDamage == 5220 && coordinator.Records.CaptureSaveData().SkillKills == 500,
                    "실제 종료 정산에서 기록 합산");
                Check(coordinator.Unlocks.CaptureSaveData().Ids.Count == 4 && coordinator.LastSettledRunId == runId, "실제 종료에서 제단 3종 해금·영수증 확정");
                Check(Find<SaveManager>().TryLoad(OutGameSaveCoordinator.SaveKey, out var persisted, out error), "실제 정산 파일 조회");
                int reward = int.Parse(((TMPro.TMP_Text)Get(view, "_bloodstone")).text.Replace(",", ""));
                Check(reward > 0 && persisted.Wallet.Amount - balanceBefore == reward && Find<PersistentCurrencyManager>().Balance == persisted.Wallet.Amount,
                    "결과 표시액 = 파일 증가분 = 지갑 증가분");
                ((Button)Get(view, "_mainButton")).onClick.Invoke();
                await Until(() => SceneManager.GetActiveScene().name == "Test_MainScreen" && !SceneLoadingManager.Instance.IsLoading);
                Check(!Find<SaveManager>().HasSaveFile(InGameSaveCoordinator.SaveKey), "결과 확인 → 메인 복귀 / 완료 런 삭제");
                await SceneLoadingManager.Instance.LoadAsync("Test_OutGame");
                var altar = Find<OutGameAltarController>();
                Check(altar.IsUnlocked(AltarId.Conquest) && altar.IsUnlocked(AltarId.Arcane) && altar.TrySelect(AltarId.Guardian), "아웃게임에서 해금 복원·제단 선택");
                Find<OutGameStartController>().StartRun();
                await Until(() => SceneManager.GetActiveScene().name == "Test_InGame" && Find<InGameSaveCoordinator>() != null && Find<InGameSaveCoordinator>().IsReady && !SceneLoadingManager.Instance.IsLoading);
                Check(Find<AltarManager>().Selected.Id == AltarId.Guardian, "새 런에 해금 제단 적용");
                Check(Find<OutGameSaveCoordinator>().Records.CaptureSaveData().TotalDamage == 5220 && Find<RunRecordManager>().CaptureSaveData().Records.TotalDamage == 0,
                    "새 런 임시 기록 초기화 / 영구 기록 유지");
                Status = string.Join("\n", Results);
            }
            catch (Exception exception) { Status = string.Join("\n", Results) + "\nERROR " + exception; }
            finally
            {
                Directory.CreateDirectory("PersonalDocs/ProgressValidation");
                File.WriteAllText("PersonalDocs/ProgressValidation/play-checks.txt", Status);
            }
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static Unit_Gateway CreateUnit(string name, UnitTeam team)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            TemporaryAssets.Add(data);
            var so = new SerializedObject(data);
            so.FindProperty("_team").enumValueIndex = (int)team;
            so.FindProperty("_unitId").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
            typeof(UnitData).GetField("_stats", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(data, new List<UnitStatEntry> { new UnitStatEntry(UnitStatType.MaxHp, 100) });
            var go = new GameObject(name);
            var gateway = go.AddComponent<Unit_Gateway>();
            go.AddComponent<Unit_RuntimeStatus>(); go.AddComponent<Unit_Life>();
            go.AddComponent<Unit_Core>().Initialize(null, null, data);
            return gateway;
        }

        public static void Finish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode 종료 후 실행하세요.");
            SessionState.SetBool(EnabledKey, false);
            EditorSceneManager.OpenScene("Assets/Scenes/Test/Test_InGame.unity");
        }
    }
}
#endif
