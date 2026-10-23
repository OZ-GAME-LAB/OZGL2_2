#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Archive.Editor
{
    public static class ArchiveSaveValidation
    {
        private static readonly List<string> Results = new();
        private static void Check(bool value, string message)
        {
            Results.Add((value ? "PASS " : "FAIL ") + message);
            if (!value) throw new InvalidOperationException(message);
        }

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only");
            Results.Clear();
            using (var f = new Fixture())
            {
                var old = f.Coordinator.CaptureSaveData();
                old.ArchiveVersion = 0; old.Archive = null;
                Check(f.Save.TrySave(OutGameSaveCoordinator.SaveKey, old, out var error), "old persistent fixture");
                var legacy = ArchiveProgressSaveData.CreateEmpty();
                legacy.DiscoveredKeys.AddRange(new[] { "0:old", "1:old", "2:old" });
                legacy.UnreadKeys.Add("0:old");
                Check(f.Save.TrySave(ArchiveSaveMigration.LegacyKey, legacy, out error), "legacy fixture");
                string original = File.ReadAllText(f.Path);
                string oldArchive = File.ReadAllText(f.LegacyPath);
                Check(f.Coordinator.TryLoad(out error), "legacy migration: " + error);
                Check(f.Load().Archive.DiscoveredKeys.Count == 3 && f.Load().Archive.UnreadKeys.Count == 1, "all categories / NEW retained");
                Check(Directory.GetFiles(f.Directory, "*.bak").Any(p => File.ReadAllText(p) == original), "original persistent backup exact");
                Check(File.ReadAllText(f.LegacyPath) == oldArchive, "legacy file unchanged");
                File.WriteAllText(f.LegacyPath, "broken old file after migration");
                int before = f.Save.SuccessfulWriteCount;
                Check(f.Coordinator.TryLoad(out error) && f.Save.SuccessfulWriteCount == before, "v1 ignores old file and does not rewrite");
                bool rejected = false;
                try { f.Reinitialize(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "coordinator rejects repeated initialization before adding subscriptions");
                f.Discover("a"); f.Discover("b"); f.Discover("c");
                Check(f.Save.SuccessfulWriteCount == before, "discovery requests defer until frame end");
                f.Tick();
                Check(f.Save.SuccessfulWriteCount == before + 1 && !f.Archive.IsDirty, "multiple discoveries write once");
                before = f.Save.SuccessfulWriteCount;
                string unreadFile = File.ReadAllText(f.Path);
                f.Archive.MarkRead(ArchiveCategory.Artifact, "a");
                f.Archive.MarkRead(ArchiveCategory.Artifact, "b");
                f.Tick(); f.Discover("a"); f.Tick();
                Check(f.Save.SuccessfulWriteCount == before && File.ReadAllText(f.Path) == unreadFile && f.Archive.IsDirty, "reads and duplicate discovery do not save");
                f.Archive.RequestSave(); f.Archive.RequestSave(); f.Tick();
                Check(f.Save.SuccessfulWriteCount == before + 1 && !f.Load().Archive.UnreadKeys.Contains("0:a"), "close batches reads once");
                f.Archive.RequestSave(); f.Tick();
                Check(f.Save.SuccessfulWriteCount == before + 1, "unchanged close writes zero");
                var stale = f.Coordinator.CaptureSaveData();
                f.Archive.MarkRead(ArchiveCategory.Artifact, "c"); f.Discover("later");
                before = f.Save.SuccessfulWriteCount;
                Check(f.Coordinator.TrySave(stale, out error), "ordinary save includes pending discovery/read");
                f.Tick();
                Check(f.Save.SuccessfulWriteCount == before + 1 && f.Load().Archive.DiscoveredKeys.Contains("0:later") &&
                    !f.Load().Archive.UnreadKeys.Contains("0:c"), "old transaction snapshot cannot overwrite archive; no extra late save");
                Check(stale.Archive.UnreadKeys.Contains("0:c"), "caller snapshot not modified");
                f.Discover("failed");
                before = f.Save.SuccessfulWriteCount;
                using (var locked = new FileStream(f.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    bool logging = Debug.unityLogger.logEnabled;
                    try { Debug.unityLogger.logEnabled = false; f.Tick(); }
                    finally { Debug.unityLogger.logEnabled = logging; }
                }
                f.Tick();
                Check(f.Archive.IsDirty && f.Save.SuccessfulWriteCount == before, "failed write stays dirty without per-frame retry");
                f.Archive.RequestSave(); f.Tick();
                Check(!f.Archive.IsDirty && f.Load().Archive.DiscoveredKeys.Contains("0:failed"), "next request retries successfully");
                f.Archive.MarkRead(ArchiveCategory.Artifact, "failed");
                Check(f.Coordinator.TrySave(out error) && !f.Load().Archive.UnreadKeys.Contains("0:failed"), "outgame provider without combat initialization preserves reads");
                Check(f.Coordinator.TryLoad(out error) && !f.Archive.IsNew(ArchiveCategory.Artifact, "failed") && !f.Archive.IsDirty,
                    "reload restores directly into the same provider");
                f.Discover("pause"); f.Tick(); f.Archive.MarkRead(ArchiveCategory.Artifact, "pause");
                Call(f.Coordinator, "OnApplicationPause", true);
                Check(!f.Load().Archive.UnreadKeys.Contains("0:pause"), "pause flushes pending reads");
                // JsonUtility.ToJson은 null 목록도 []로 쓰므로 원본 JSON에서 필수 목록을 제거합니다.
                var malformed = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(f.Path));
                ((Newtonsoft.Json.Linq.JObject)malformed["Data"]["Archive"]).Remove("UnreadKeys");
                File.WriteAllText(f.Path, malformed.ToString());
                string malformedJson = File.ReadAllText(f.Path);
                Check(!f.Coordinator.TryLoad(out error) && !f.Coordinator.IsReady && File.ReadAllText(f.Path) == malformedJson,
                    "malformed v1 fails without overwrite");
            }
            using (var f = new Fixture())
            {
                File.WriteAllText(f.LegacyPath, "{broken");
                Check(!f.Coordinator.TryLoadOrCreate(out var error) && !File.Exists(f.Path), "broken legacy blocks initial creation");
                var invalid = ArchiveProgressSaveData.CreateEmpty(); invalid.UnreadKeys.Add("0:missing");
                f.Save.TrySave(ArchiveSaveMigration.LegacyKey, invalid, out error);
                Check(!f.Coordinator.TryLoadOrCreate(out error) && !File.Exists(f.Path), "invalid legacy subset blocks initialization");
                File.Delete(f.LegacyPath);
                Check(f.Coordinator.TryLoadOrCreate(out error) && f.Load().Archive.DiscoveredKeys.Count == 0, "no legacy creates empty integrated archive");
            }
            using (var f = new Fixture())
            {
                var old = f.Coordinator.CaptureSaveData(); old.ArchiveVersion = 0; old.Archive = null;
                f.Save.TrySave(OutGameSaveCoordinator.SaveKey, old, out var error);
                string original = File.ReadAllText(f.Path);
                using (var locked = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Check(!f.Coordinator.TryLoad(out error) && !f.Coordinator.IsReady, "migration cannot replace locked destination");
                Check(File.ReadAllText(f.Path) == original, "failed migration preserves original");
                Check(f.Coordinator.TryLoad(out error) && f.Load().ArchiveVersion == 1, "migration retries successfully");
            }
            string report = $"PASS: {Results.Count} archive save checks\n" + string.Join("\n", Results);
            System.IO.Directory.CreateDirectory("PersonalDocs/ArchiveSaveValidation");
            File.WriteAllText("PersonalDocs/ArchiveSaveValidation/data.txt", report);
            return report;
        }

        private static void Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private sealed class Fixture : IDisposable
        {
            public readonly string Directory;
            public readonly SaveManager Save;
            public readonly OutGameSaveCoordinator Coordinator;
            public readonly ArchiveController Archive;
            private readonly Scene _scene;
            private readonly GameObject _holder;
            public string Path => Save.GetFilePath(OutGameSaveCoordinator.SaveKey);
            public string LegacyPath => Save.GetFilePath(ArchiveSaveMigration.LegacyKey);
            public Fixture()
            {
                Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OZGL_ArchiveSave_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(Directory);
                _scene = EditorSceneManager.NewPreviewScene();
                _holder = new GameObject("Archive save fixture") { hideFlags = HideFlags.HideAndDontSave };
                _holder.SetActive(false);
                SceneManager.MoveGameObjectToScene(_holder, _scene);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OutGame/OutGameSetup.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
                root.transform.SetParent(_holder.transform, false);
                Save = root.GetComponentInChildren<SaveManager>(true);
                Save.ConfigureDirectory(Directory);
                Coordinator = root.GetComponentInChildren<OutGameSaveCoordinator>(true);
                var wallet = root.GetComponentInChildren<PersistentCurrencyManager>(true);
                var traits = root.GetComponentInChildren<OutGameTraitController>(true);
                var altar = root.GetComponentInChildren<OutGameAltarController>(true);
                var totems = root.GetComponentInChildren<OutGameTotemController>(true);
                var unlocks = root.GetComponentInChildren<UnlockManager>(true);
                unlocks.Initialize();
                wallet.Initialize(); altar.Initialize(unlocks); traits.Initialize(wallet, Coordinator); totems.Initialize();
                Archive = root.GetComponentInChildren<ArchiveController>(true);
                if (Archive == null) throw new Exception("OutGameSetup archive provider missing");
                Coordinator.Initialize(Save, traits, wallet, altar, totems, Archive, unlocks);
            }
            public void Reinitialize() => Coordinator.Initialize(Save, null, null, null, null, Archive, null);
            public void Tick() => Call(Coordinator, "LateUpdate");
            public PersistentSaveData Load()
            {
                if (!Save.TryLoad(OutGameSaveCoordinator.SaveKey, out PersistentSaveData data, out string error)) throw new Exception(error);
                return data;
            }
            public void Discover(string id)
            {
                var data = ScriptableObject.CreateInstance<ArtifactData>();
                try
                {
                    typeof(ArtifactData).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, id);
                    Call(Archive, "HandleArtifactChanged", new ArtifactInstance(data), 0, 1);
                }
                finally { Object.DestroyImmediate(data); }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(_holder);
                EditorSceneManager.ClosePreviewScene(_scene);
                foreach (string file in System.IO.Directory.GetFiles(Directory)) File.Delete(file);
                System.IO.Directory.Delete(Directory, false);
            }
        }
    }
}
#endif
