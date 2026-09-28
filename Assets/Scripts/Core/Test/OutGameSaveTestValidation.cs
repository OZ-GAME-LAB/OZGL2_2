using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.SaveTests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.SaveTests.Editor
{
    /// <summary>격리된 임시 파일과 Preview Scene에서 저장 계약만 검증합니다.</summary>
    [InitializeOnLoad]
    public static class OutGameSaveTestValidation
    {
        private static string TempRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
        private static string RequestPath => Path.Combine(TempRoot, "OutGameSaveTestValidation.request");
        private static string ResultPath => Path.Combine(TempRoot, "OutGameSaveTestValidation.result.json");

        static OutGameSaveTestValidation()
        {
            EditorApplication.delayCall += RunRequestedValidation;
        }

        private static void RunRequestedValidation()
        {
            if (!File.Exists(RequestPath)) return;
            try
            {
                File.Delete(RequestPath);
                Run();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[OutGame/SaveTest] 요청 처리 실패: {exception.Message}");
            }
        }

        [MenuItem("Tools/OutGame/Validate Save Test")]
        public static void Run()
        {
            var report = new ValidationReport { Utc = DateTime.UtcNow.ToString("O") };
            Scene preview = default;
            string directory = Path.GetFullPath(Path.Combine(TempRoot, "OutGameSaveValidation-" + Guid.NewGuid().ToString("N")));
            Scene originalScene = SceneManager.GetActiveScene();
            bool originalDirty = originalScene.isDirty;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Play Mode를 종료한 후 검증하세요.");

                preview = EditorSceneManager.NewPreviewScene();
                OutGameSaveTestPanel panel = CreatePanel(preview, directory, out SaveManager manager);
                panel.ApplySampleData();
                OutGameSaveTestData sample = panel.CaptureSaveData();

                Check(report, "샘플 값", () =>
                {
                    Require(sample.Permanent.Bloodstone == 500, "혈석");
                    Require(sample.LastSelection.AltarId == "test_altar_sun", "선택 제단");
                    Require(sample.Permanent.UnlockedAltarIds.Contains("test_altar_sun"), "제단 해금");
                    Require(HasLevel(sample.Permanent.Traits, "test_trait_attack", 2), "특성 레벨");
                    Require(HasLevel(sample.LastSelection.Totems, "test_totem_sun", 3), "토템 레벨");
                });

                Check(report, "파일 저장과 복원", () =>
                {
                    Require(panel.TrySave(out string error), error);
                    Require(File.Exists(manager.GetFilePath(OutGameSaveTestPanel.SaveKey)), "저장 파일 없음");
                    panel.ClearMemory();
                    Require(!Equal(sample, panel.CaptureSaveData()), "메모리 초기화 실패");
                    Require(panel.TryLoad(out error), error);
                    Require(Equal(sample, panel.CaptureSaveData()), "복원값 불일치");
                });

                Check(report, "새 객체에서 복원", () =>
                {
                    panel.RestoreSaveData(sample);
                    Require(panel.TrySave(out string error), error);
                    OutGameSaveTestPanel fresh = CreatePanel(preview, directory, out _);
                    try
                    {
                        Require(fresh.TryLoad(out error), error);
                        Require(Equal(sample, fresh.CaptureSaveData()), "새 객체 복원값 불일치");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(fresh.gameObject); }
                });

                Check(report, "Capture와 Restore의 참조 분리", () =>
                {
                    panel.RestoreSaveData(sample);
                    OutGameSaveTestData captured = panel.CaptureSaveData();
                    Mutate(captured);
                    Require(Equal(sample, panel.CaptureSaveData()), "Capture가 런타임과 참조 공유");
                    OutGameSaveTestData incoming = Clone(sample);
                    panel.RestoreSaveData(incoming);
                    Mutate(incoming);
                    Require(Equal(sample, panel.CaptureSaveData()), "Restore가 입력과 참조 공유");
                });

                Check(report, "반복 복원 시 중복 누적 없음", () =>
                {
                    panel.RestoreSaveData(sample);
                    panel.RestoreSaveData(sample);
                    Require(Equal(sample, panel.CaptureSaveData()), "중복 복원으로 상태 변경");
                });

                Check(report, "잘못된 데이터는 현재 상태 유지", () =>
                {
                    var invalidCases = new List<OutGameSaveTestData> { null };
                    OutGameSaveTestData nullSection = Clone(sample);
                    nullSection.Permanent = null;
                    invalidCases.Add(nullSection);
                    OutGameSaveTestData nullSelection = Clone(sample);
                    nullSelection.LastSelection = null;
                    invalidCases.Add(nullSelection);
                    OutGameSaveTestData negative = Clone(sample);
                    negative.Permanent.Bloodstone = -1;
                    invalidCases.Add(negative);
                    OutGameSaveTestData duplicate = Clone(sample);
                    duplicate.LastSelection.Totems.Add(duplicate.LastSelection.Totems[0]);
                    invalidCases.Add(duplicate);
                    OutGameSaveTestData nullList = Clone(sample);
                    nullList.Permanent.Traits = null;
                    invalidCases.Add(nullList);
                    foreach (OutGameSaveTestData invalid in invalidCases)
                    {
                        panel.RestoreSaveData(sample);
                        try { panel.RestoreSaveData(invalid); }
                        catch (ArgumentException) { }
                        catch (InvalidOperationException) { }
                        Require(Equal(sample, panel.CaptureSaveData()), "잘못된 복원으로 런타임 변경");
                    }
                });

                Check(report, "파일 없음과 손상은 상태 유지", () =>
                {
                    panel.RestoreSaveData(sample);
                    string path = manager.GetFilePath(OutGameSaveTestPanel.SaveKey);
                    if (File.Exists(path)) File.Delete(path);
                    Require(!panel.TryLoad(out _), "없는 파일을 성공으로 처리");
                    Require(Equal(sample, panel.CaptureSaveData()), "없는 파일로 런타임 변경");
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(path, "{ invalid json");
                    Require(!panel.TryLoad(out _), "손상 파일을 성공으로 처리");
                    Require(Equal(sample, panel.CaptureSaveData()), "손상 파일로 런타임 변경");
                });

                Check(report, "올바른 JSON의 잘못된 내용 거부", () =>
                {
                    panel.RestoreSaveData(sample);
                    OutGameSaveTestData invalid = Clone(sample);
                    invalid.Permanent.Bloodstone = -1;
                    Require(manager.TrySave(OutGameSaveTestPanel.SaveKey, invalid, out string error), error);
                    Require(!panel.TryLoad(out _), "음수 잔액 복원 허용");
                    Require(Equal(sample, panel.CaptureSaveData()), "유효성 검사 전 런타임 변경");
                });

                Check(report, "저장 덮어쓰기", () =>
                {
                    panel.RestoreSaveData(sample);
                    Require(panel.TrySave(out string error), error);
                    OutGameSaveTestData changed = Clone(sample);
                    changed.Permanent.Bloodstone = 123;
                    panel.RestoreSaveData(changed);
                    Require(panel.TrySave(out error), error);
                    panel.ClearMemory();
                    Require(panel.TryLoad(out error), error);
                    Require(Equal(changed, panel.CaptureSaveData()), "이전 파일이 복원됨");
                });

                Check(report, "키의 타입과 버전 불일치 거부", () =>
                {
                    var key = new SaveKey<OutGameSaveTestData>("validation_contract", 1);
                    Require(manager.TrySave(key, sample, out string error), error);
                    Require(!manager.TryLoad(new SaveKey<OtherSaveData>("validation_contract", 1), out _, out _),
                        "다른 타입으로 읽기 허용");
                    Require(!manager.TryLoad(new SaveKey<OutGameSaveTestData>("validation_contract", 2), out _, out _),
                        "다른 버전으로 읽기 허용");
                });

                Check(report, "유효하지 않은 저장 키 거부", () =>
                {
                    bool rejected = false;
                    try { _ = new SaveKey<OutGameSaveTestData>("../outside"); }
                    catch (ArgumentException) { rejected = true; }
                    Require(rejected, "저장 키로 상위 경로 접근 허용");
                });

                Check(report, "지원하지 않는 루트 타입 거부와 기존 파일 보존", () =>
                {
                    const string id = "validation_type_safety";
                    var key = new SaveKey<OutGameSaveTestData>(id);
                    Require(manager.TrySave(key, sample, out string error), error);
                    string path = manager.GetFilePath(key);
                    string before = Convert.ToBase64String(File.ReadAllBytes(path));
                    RejectSave(manager, id, new Dictionary<string, int> { { "bloodstone", 500 } }, path, before);
                    RejectSave(manager, id, new List<int> { 1, 2 }, path, before);
                    RejectSave(manager, id, new[] { 1, 2 }, path, before);
                    RejectSave(manager, id, new object(), path, before);
                    RejectSave(manager, id, "not-a-dto", path, before);
                    RejectSave(manager, id, new NonSerializableSaveData(), path, before);
                    RejectSave(manager, id, panel.gameObject, path, before);
                });

                Check(report, "기본 타입 키에 파생 데이터 거부와 기존 파일 보존", () =>
                {
                    const string id = "validation_exact_type";
                    var key = new SaveKey<BaseSaveData>(id);
                    Require(manager.TrySave(key, new BaseSaveData { Value = 7 }, out string error), error);
                    string path = manager.GetFilePath(key);
                    string before = Convert.ToBase64String(File.ReadAllBytes(path));
                    RejectSave<BaseSaveData>(manager, id, new DerivedSaveData { Value = 8, Extra = 9 }, path, before);
                    Require(manager.TryLoad(key, out BaseSaveData loaded, out error), error);
                    Require(loaded.Value == 7, "거부한 저장이 이전 데이터를 변경함");
                });
            }
            catch (Exception exception)
            {
                report.Errors.Add("검증 준비: " + exception.Message);
            }
            finally
            {
                try
                {
                    if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                    string allowedRoot = TempRoot + Path.DirectorySeparatorChar;
                    if (!directory.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("검증 임시 경로가 Temp 외부입니다.");
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    Require(SceneManager.GetActiveScene() == originalScene && originalScene.isDirty == originalDirty,
                        "기존 활성 씬 또는 저장 상태가 변경되었습니다.");
                }
                catch (Exception exception) { report.Errors.Add("정리: " + exception.Message); }
                report.Passed = report.Errors.Count == 0;
                try
                {
                    Directory.CreateDirectory(TempRoot);
                    File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
                }
                catch (Exception exception) { Debug.LogError($"[OutGame/SaveTest] 결과 기록 실패: {exception.Message}"); }
                string summary = $"[OutGame/SaveTest] {(report.Passed ? "PASS" : "FAIL")} | " +
                    $"검사 {report.Checks}, 실패 {report.Errors.Count} | {ResultPath}";
                if (report.Passed) Debug.Log(summary);
                else Debug.LogError(summary + "\n" + string.Join("\n", report.Errors));
            }
        }

        private static OutGameSaveTestPanel CreatePanel(Scene scene, string directory, out SaveManager manager)
        {
            var host = new GameObject("OutGameSaveValidation") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(host, scene);
            manager = host.AddComponent<SaveManager>();
            manager.ConfigureDirectory(directory);
            return host.AddComponent<OutGameSaveTestPanel>();
        }

        private static void Mutate(OutGameSaveTestData data)
        {
            data.Permanent.Bloodstone = 1;
            data.Permanent.UnlockedAltarIds.Clear();
            data.Permanent.Traits.Clear();
            data.LastSelection.AltarId = "changed";
            data.LastSelection.Totems.Clear();
        }

        private static bool HasLevel(List<TestLevelEntry> entries, string id, int level)
            => entries.Exists(entry => entry.Id == id && entry.Level == level);

        private static OutGameSaveTestData Clone(OutGameSaveTestData data)
            => JsonUtility.FromJson<OutGameSaveTestData>(JsonUtility.ToJson(data));

        private static bool Equal(OutGameSaveTestData left, OutGameSaveTestData right)
            => JsonUtility.ToJson(left) == JsonUtility.ToJson(right);

        private static void Check(ValidationReport report, string label, Action action)
        {
            report.Checks++;
            try { action(); }
            catch (Exception exception) { report.Errors.Add(label + ": " + exception.Message); }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "조건 불일치");
        }

        private static void RejectSave<T>(SaveManager manager, string id, T data, string path, string before)
            where T : class
        {
            Require(!manager.TrySave(new SaveKey<T>(id), data, out string error),
                typeof(T).Name + " 저장을 성공으로 처리함");
            Require(!string.IsNullOrWhiteSpace(error), "거부 사유 없음");
            Require(Convert.ToBase64String(File.ReadAllBytes(path)) == before,
                typeof(T).Name + " 저장 거부 시 기존 파일 변경");
        }

        [Serializable]
        private sealed class OtherSaveData { public int Value = 0; }

        private sealed class NonSerializableSaveData { public int Value = 1; }

        [Serializable]
        private class BaseSaveData { public int Value = 0; }

        [Serializable]
        private sealed class DerivedSaveData : BaseSaveData { public int Extra = 0; }

        [Serializable]
        private sealed class ValidationReport
        {
            public bool Passed;
            public int Checks;
            public string Utc;
            public List<string> Errors = new List<string>();
        }
    }
}
