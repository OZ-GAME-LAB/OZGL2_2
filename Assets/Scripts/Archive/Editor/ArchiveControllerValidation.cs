using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Core;
using OZGL.KDH;
using Units;
using Units.UnitDatas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Archive.Editor
{
    // 임시 preview scene과 고유한 임시 저장 경로만 사용합니다.
    public static class ArchiveControllerValidation
    {
        private static int _checks;

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit Mode에서 실행하세요.");
            _checks = 0;
            string directory = Path.Combine(Path.GetTempPath(), "OZGL2ArchiveValidation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var folders = new List<string>();
            var assets = new List<UnityEngine.Object>();
            var controllers = new List<ArchiveController>();
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("ArchiveValidation (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                GameObject Child(string name)
                {
                    var child = new GameObject(name);
                    child.SetActive(false);
                    SceneManager.MoveGameObjectToScene(child, scene);
                    child.transform.SetParent(root.transform);
                    return child;
                }
                T Asset<T>() where T : ScriptableObject
                {
                    var value = ScriptableObject.CreateInstance<T>();
                    assets.Add(value);
                    return value;
                }
                ArtifactInstance Artifact(string id)
                {
                    var data = Asset<ArtifactData>();
                    Set(data, "_id", id);
                    return new ArtifactInstance(data);
                }
                Building Building(string id)
                {
                    var data = Asset<BuildingData>();
                    Set(data, "buildingId", id);
                    var building = Child(id).AddComponent<Building>();
                    Set(building, "data", data);
                    return building;
                }
                SaveManager Save(string name)
                {
                    var save = Child(name).AddComponent<SaveManager>();
                    string folder = Path.Combine(directory, name);
                    folders.Add(folder);
                    save.ConfigureDirectory(folder);
                    return save;
                }
                ArchiveController Controller()
                {
                    var controller = Child("ArchiveController").AddComponent<ArchiveController>();
                    controllers.Add(controller);
                    return controller;
                }

                var flow = root.AddComponent<GameFlowController>();
                var units = root.AddComponent<RuntimeUnitManager>();
                var slotObject = Child("Slot");
                slotObject.AddComponent<BoxCollider2D>();
                var slot = slotObject.AddComponent<BuildingSlot>();
                Check(slot.TryOccupy(Building("seed_building")), "seed building occupied");
                var reader = new ArtifactReader();
                reader.Owned.Add(Artifact("seed_artifact"));
                var save = Save("main");
                var archive = Controller();
                archive.Initialize(save, reader, new[] { slot }, flow, units);
                Check(archive.IsDiscovered(ArchiveCategory.Artifact, "seed_artifact") &&
                    !archive.IsNew(ArchiveCategory.Artifact, "seed_artifact"), "existing artifact seeded read");
                Check(archive.IsDiscovered(ArchiveCategory.Building, "seed_building") &&
                    !archive.IsNew(ArchiveCategory.Building, "seed_building"), "existing building seeded read");
                Check(archive.IsDiscovered(ArchiveCategory.Unit, "ally", true) &&
                    !archive.IsNew(ArchiveCategory.Unit, "ally"), "allies public without NEW");
                Check(!archive.IsDiscovered(ArchiveCategory.Unit, "unseen_enemy"), "unseen enemies hidden");

                var relic = Artifact("shared_id");
                reader.Emit(relic, 0, 1);
                Check(archive.IsDiscovered(ArchiveCategory.Artifact, relic.Data.Id) &&
                    archive.IsNew(ArchiveCategory.Artifact, relic.Data.Id), "upward artifact event discovers NEW");
                reader.Emit(Artifact("declined_only"), 1, 0);
                Check(!archive.IsDiscovered(ArchiveCategory.Artifact, "declined_only"), "downward artifact event ignored");
                archive.MarkRead(ArchiveCategory.Artifact, relic.Data.Id);
                reader.Emit(relic, 1, 2);
                reader.Emit(relic, 2, 0);
                Check(archive.IsDiscovered(ArchiveCategory.Artifact, relic.Data.Id) &&
                    !archive.IsNew(ArchiveCategory.Artifact, relic.Data.Id), "repeat gain and removal preserve read discovery");
                slot.ReleaseCurrent();
                Check(slot.TryOccupy(Building("shared_id")), "replacement building occupied");
                Check(archive.IsNew(ArchiveCategory.Building, "shared_id") &&
                    !archive.IsNew(ArchiveCategory.Artifact, "shared_id"), "category IDs do not collide");
                Check(archive.IsDiscovered(ArchiveCategory.Building, "seed_building"), "replacement retains prior building");

                var enemyData = Asset<UnitData>();
                Set(enemyData, "_unitId", "actual_enemy");
                var enemy = Child("Enemy");
                var status = enemy.AddComponent<Unit_RuntimeStatus>();
                Set(status, "_unitData", enemyData);
                var core = enemy.AddComponent<Unit_Core>();
                Set(core, "_runtimeStatus", status);
                var gateway = enemy.AddComponent<Unit_Gateway>();
                Set(gateway, "_core", core);
                ((List<Unit_Gateway>)Get(units, "_enemyUnits")).Add(gateway);
                Call(flow, "ChangePhase", GamePhase.BattlePreparing);
                Check(!archive.IsDiscovered(ArchiveCategory.Unit, "actual_enemy"), "preparation is not encounter");
                Call(flow, "ChangePhase", GamePhase.Battle);
                Check(archive.IsDiscovered(ArchiveCategory.Unit, "actual_enemy") &&
                    archive.IsNew(ArchiveCategory.Unit, "actual_enemy"), "battle collects actual enemy IDs");
                archive.MarkRead(ArchiveCategory.Unit, "actual_enemy");
                Call(flow, "ChangePhase", GamePhase.Battle);
                Check(!archive.IsNew(ArchiveCategory.Unit, "actual_enemy"), "repeat encounter remains read");

                Check(save.TryLoad(ArchiveController.SaveKey, out ArchiveProgressSaveData saved, out string error),
                    "real save JSON loads: " + error);
                Check(saved.UnreadKeys.Contains("1:shared_id") && !saved.UnreadKeys.Contains("0:shared_id"),
                    "read state persisted independently");
                saved.DiscoveredKeys.Add("2:removed_definition");
                Check(save.TrySave(ArchiveController.SaveKey, saved, out error), "unknown definition fixture saved: " + error);
                archive.Initialize(save, reader, new[] { slot }, flow, units);
                Check(reader.SubscriberCount == 1, "reinitialization does not duplicate subscriptions");
                Check(archive.IsNew(ArchiveCategory.Building, "shared_id"), "seed preserves NEW from prior save");
                Check(archive.IsDiscovered(ArchiveCategory.Unit, "removed_definition"), "missing catalog IDs retained");
                archive.MarkRead(ArchiveCategory.Building, "shared_id");
                var reloaded = Controller();
                reloaded.Initialize(save, new ArtifactReader(), Array.Empty<BuildingSlot>(), flow, units);
                Check(reloaded.IsDiscovered(ArchiveCategory.Building, "shared_id") &&
                    !reloaded.IsNew(ArchiveCategory.Building, "shared_id"), "fresh controller restores discovered and read");

                var corruptSave = Save("corrupt");
                Directory.CreateDirectory(corruptSave.DirectoryPath);
                string corruptPath = corruptSave.GetFilePath(ArchiveController.SaveKey);
                const string corrupt = "{ invalid archive file";
                File.WriteAllText(corruptPath, corrupt);
                var corruptReader = new ArtifactReader();
                var corruptArchive = Controller();
                ExpectedFailure(() => corruptArchive.Initialize(corruptSave, corruptReader,
                    Array.Empty<BuildingSlot>(), flow, units));
                corruptReader.Emit(Artifact("after_corrupt"), 0, 1);
                corruptArchive.Flush();
                Check(File.ReadAllText(corruptPath) == corrupt, "corrupt existing file never overwritten");

                var invalidSave = Save("invalid");
                var invalid = new ArchiveProgressSaveData { UnreadKeys = new List<string> { "0:undiscovered" } };
                Check(invalidSave.TrySave(ArchiveController.SaveKey, invalid, out error), "invalid fixture envelope saved");
                string invalidPath = invalidSave.GetFilePath(ArchiveController.SaveKey);
                string invalidJson = File.ReadAllText(invalidPath);
                var invalidArchive = Controller();
                ExpectedFailure(() => invalidArchive.Initialize(invalidSave, new ArtifactReader(),
                    new[] { slot }, flow, units));
                invalidArchive.Flush();
                Check(File.ReadAllText(invalidPath) == invalidJson, "invalid unread state never overwritten by seeds");

                var retrySave = Save("retry");
                File.WriteAllText(retrySave.DirectoryPath, "temporary directory blocker");
                var retryReader = new ArtifactReader();
                var retryArchive = Controller();
                retryArchive.Initialize(retrySave, retryReader, Array.Empty<BuildingSlot>(), flow, units);
                ExpectedFailure(() => retryReader.Emit(Artifact("retry_artifact"), 0, 1));
                Check(retryArchive.IsNew(ArchiveCategory.Artifact, "retry_artifact"), "failed write retains in-memory discovery");
                File.Delete(retrySave.DirectoryPath);
                retryArchive.Flush();
                Check(retrySave.TryLoad(ArchiveController.SaveKey, out ArchiveProgressSaveData retried, out error) &&
                    retried.UnreadKeys.Contains("0:retry_artifact"), "dirty state retries successfully without another discovery");
                return $"PASS: {_checks} archive checks (isolated temporary saves; discovery, NEW/read, corruption preservation, dirty retry).";
            }
            finally
            {
                foreach (ArchiveController controller in controllers) Call(controller, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (UnityEngine.Object asset in assets) UnityEngine.Object.DestroyImmediate(asset);
                // Only exact fixture folders are emptied; no recursive deletion is used.
                foreach (string folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                    Directory.Delete(folder, false);
                }
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory, false);
            }
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Archive validation failed: " + message);
            _checks++;
        }

        private static void ExpectedFailure(Action action)
        {
            bool logging = Debug.unityLogger.logEnabled;
            try { Debug.unityLogger.logEnabled = false; action(); }
            finally { Debug.unityLogger.logEnabled = logging; }
        }

        private static object Get(object target, string field) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private sealed class ArtifactReader : IArtifactReader
        {
            public readonly List<ArtifactInstance> Owned = new();
            public bool IsInitialized => true;
            public IReadOnlyList<ArtifactInstance> Instances => Owned;
            public ArtifactCatalog ArtifactCatalog => null;
            public event Action<ArtifactInstance, int, int> StackChanged;
            public event Action<IReadOnlyList<ArtifactInstance>> Cleared { add { } remove { } }
            public int SubscriberCount => StackChanged?.GetInvocationList().Length ?? 0;
            public void Emit(ArtifactInstance instance, int previous, int current) => StackChanged?.Invoke(instance, previous, current);
            public bool Contains(ArtifactData artifact) => Owned.Exists(instance => instance.Data == artifact);
            public bool TryGetById(string id, out ArtifactInstance instance)
            {
                instance = Owned.Find(owned => owned.Data.Id == id);
                return instance != null;
            }
            public List<ArtifactInstance> GetExchangeCandidates(ArtifactData rewardArtifact) => new();
        }
    }
}
