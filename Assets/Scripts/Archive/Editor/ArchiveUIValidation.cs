#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.UI.InGame;
using OZGL.KDH;
using TMPro;
using Units;
using Units.UnitDatas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Archive.Editor
{
    // 실제 프리팹을 preview scene에서 검사한다. Play Mode 수명 검사는 별도로 실행한다.
    public static class ArchiveUIValidation
    {
        private static int _checks;

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit Mode에서 실행하세요.");
            _checks = 0;
            float previousScale = Time.timeScale;
            string directory = Path.Combine(Path.GetTempPath(), "OZGL2ArchiveUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Archive UI validation (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var assets = new List<Object>();
            ArchiveController controller = null;
            ArchiveView view = null;
            try
            {
                var windowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Archive/ArchiveWindow.prefab");
                var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InGame/InGameUIRoot.prefab");
                Check(windowPrefab != null && uiPrefab != null, "installed archive and ingame root prefabs");
                AssertReferences(windowPrefab.GetComponentInChildren<ArchiveView>(true));
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, scene);
                ui.transform.SetParent(root.transform, false);
                var manager = ui.GetComponent<InGameUIManager>();
                view = ui.GetComponentInChildren<ArchiveView>(true);
                AssertReferences(view);
                var screen = view.GetComponentInParent<UIScreen>(true);
                Check(screen.Id == UIId.Archive && screen.Root == view.gameObject && !screen.Root.activeSelf,
                    "archive uses an initially inactive UIScreen root");
                Check(screen.BlocksHudInput && !screen.CloseWhenCovered && screen.CanCloseByUser,
                    "archive modal and coverage policy");
                Check(view.GetComponentsInChildren<UIScreen>(true).Length == 0 &&
                    view.GetComponentsInChildren<CloseUtility>(true).Length == 1,
                    "detail content has no independent screens or outside close controls");
                var artifactDetail = (ArtifactPopupView)Get(view, "_artifactDetail");
                var buildingDetail = (BuildingInfoView)Get(view, "_buildingDetail");
                var unitDetail = (UnitPopupView)Get(view, "_unitDetail");
                AssertReferences(artifactDetail, "_screen");
                AssertReferences(buildingDetail, "_playerPopup");
                AssertReferences(unitDetail, "_screen");
                Check(Get(unitDetail, "_skillPopup") is SkillPopupView skill && skill.transform.IsChildOf(ui.transform),
                    "archive skills use the shared ingame popup");
                Check(ui.GetComponentsInChildren<OpenUtility>(true).Any(open =>
                    (UIId)Get(open, "_target") == UIId.Archive), "HUD archive entry registered");

                var catalog = (ArtifactCatalog)Get(view, "_artifactCatalog");
                Check(catalog.Artifacts.Count >= 2, "artifact catalog provides discovered and unknown entries");
                ArtifactData knownArtifact = catalog.Artifacts[0];
                ArtifactData unknownArtifact = catalog.Artifacts[1];
                var allies = UnitDataFrom((AllyUnitSpawnDatabaseSO)Get(view, "_allyUnitDatabase"));
                var enemies = UnitDataFrom((EnemyUnitSpawnDatabaseSO)Get(view, "_enemyUnitDatabase"));
                UnitData ally = allies.FirstOrDefault(unit => unit.ActiveSkills.Count + unit.PassiveSkillDatas.Count > 0);
                Check(ally != null && enemies.Count >= 2, "real unit databases provide skills and enemy lock fixtures");
                UnitData knownEnemy = enemies[0];
                UnitData unknownEnemy = enemies[1];
                var database = Object.Instantiate((BuildingDatabase)Get(view, "_buildingDatabase"));
                assets.Add(database);
                BuildingData Building(string id, bool spawn = false, string family = null)
                {
                    var data = ScriptableObject.CreateInstance<BuildingData>();
                    assets.Add(data);
                    Set(data, "buildingId", id);
                    Set(data, "displayName", "검증 " + id);
                    Set(data, "buildingFamilyId", family);
                    Set(data, "spawn", new BuildingSpawnSettings { enabled = spawn, unitType = ally.GetAllyType() });
                    return data;
                }
                BuildingData primary = Building("archive_ui_primary", true);
                BuildingData producer2 = Building("archive_ui_other_producer", true);
                BuildingData disabledProducer = Building("archive_ui_disabled_producer");
                BuildingData knownUpgrade = Building("archive_ui_known_upgrade");
                BuildingData lockedUpgrade = Building("archive_ui_locked_upgrade");
                BuildingData previous2 = Building("archive_ui_other_parent");
                BuildingData dependencyA = Building("archive_ui_dependency_a", family: "archive_ui_family_a");
                BuildingData dependencyA2 = Building("archive_ui_dependency_a2", family: "archive_ui_family_a");
                BuildingData dependencyB = Building("archive_ui_dependency_b", family: "archive_ui_family_b");
                Set(primary, "requiredFamilyIds", new[] { "archive_ui_family_a", "archive_ui_family_b" });
                Set(primary, "upgrades", new[]
                {
                    new BuildingUpgradeOption { nextBuilding = knownUpgrade, requiredCoreLevel = 999 },
                    new BuildingUpgradeOption { nextBuilding = lockedUpgrade },
                    new BuildingUpgradeOption { nextBuilding = knownUpgrade }
                });
                Set(previous2, "upgrades", new[] { new BuildingUpgradeOption { nextBuilding = knownUpgrade } });
                var originalBuildings = database.Buildings.Where(data => data != null && !string.IsNullOrWhiteSpace(data.BuildingId))
                    .GroupBy(data => data.BuildingId).Select(group => group.First()).ToArray();
                Set(database, "buildings", originalBuildings.Concat(new[] { primary, producer2, disabledProducer,
                    knownUpgrade, lockedUpgrade, previous2, dependencyA, dependencyA2, dependencyB, primary }).ToArray());
                Set(view, "_buildingDatabase", database);

                var saveObject = new GameObject("Temporary save");
                SceneManager.MoveGameObjectToScene(saveObject, scene);
                saveObject.transform.SetParent(root.transform, false);
                var save = saveObject.AddComponent<SaveManager>();
                save.ConfigureDirectory(directory);
                var fixture = new ArchiveProgressSaveData
                {
                    DiscoveredKeys = new List<string>
                    {
                        "0:" + knownArtifact.Id, "1:" + primary.BuildingId, "1:" + knownUpgrade.BuildingId,
                        "1:" + previous2.BuildingId, "1:" + dependencyA.BuildingId, "1:" + dependencyB.BuildingId,
                        "1:" + disabledProducer.BuildingId, "2:" + knownEnemy.UnitId
                    },
                    UnreadKeys = new List<string> { "0:" + knownArtifact.Id, "1:" + primary.BuildingId, "2:" + knownEnemy.UnitId }
                };
                Check(save.TrySave(ArchiveController.SaveKey, fixture, out string error), "temporary fixture save: " + error);
                var flow = root.AddComponent<GameFlowController>();
                var units = root.AddComponent<RuntimeUnitManager>();
                controller = root.AddComponent<ArchiveController>();
                controller.Initialize(save, new EmptyArtifactReader(), Array.Empty<BuildingSlot>(), flow, units);
                view.Initialize(controller);
                Call(buildingDetail, "Awake");
                Check(manager.InitializeScreens(), "actual ingame screen registry initializes");
                root.SetActive(true);
                Check(manager.ShowHud(), "HUD remains available in the fixture");

                Open(manager, view, 1.5f);
                Check(((GameObject)Get(view, "_emptyDetail")).activeSelf && !artifactDetail.gameObject.activeSelf,
                    "initial artifact tab has no automatic selection");
                Check(Slots(view).Count == catalog.Artifacts.Count, "catalog order and count preserved");
                UIItemSlot first = Slots(view)[0];
                Check(Text(first, "_title") == knownArtifact.DisplayName && Text(first, "_value") == "NEW",
                    "discovered artifact shows name and NEW");
                UIItemSlot locked = Slots(view)[1];
                Check(Text(locked, "_title") == "?" && Text(locked, "_value") == "미발견" &&
                    !Button(locked).interactable && Get(locked, "_onClick") == null,
                    "unknown artifact hides name and disables click");
                Check(!view.Navigate(ArchiveCategory.Artifact, unknownArtifact.Id), "direct navigation cannot bypass discovery");
                ClickSlot(view, first);
                Check(!controller.IsNew(ArchiveCategory.Artifact, knownArtifact.Id) && artifactDetail.gameObject.activeSelf,
                    "opening an artifact clears NEW and uses its existing detail view");

                ClickNavigation(view, "_buildingTabButton");
                Check(Slots(view).Count == originalBuildings.Length + 9, "building IDs deduplicated while preserving database order");
                PrepareRefresh(view);
                Check(view.Navigate(ArchiveCategory.Building, primary.BuildingId), "fixture building is navigable");
                Check(buildingDetail.SelectionId == "archive:" + primary.BuildingId &&
                    !((UnityEngine.UI.Button)Get(buildingDetail, "_unitButton")).gameObject.activeSelf,
                    "building content binds without runtime actions or duplicate unit button");
                Check(!controller.IsNew(ArchiveCategory.Building, primary.BuildingId), "building NEW clears on read");
                var scroll = (UnityEngine.UI.ScrollRect)Get(view, "_listScroll");
                var selected = Slots(view).Single(slot => Text(slot, "_title") == primary.DisplayName);
                Bounds selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
                Check(selectedBounds.min.y >= scroll.viewport.rect.yMin - 2 && selectedBounds.max.y <= scroll.viewport.rect.yMax + 2,
                    "cross navigation scrolls the selected grid entry into view");
                var headers = ((List<GameObject>)Get(view, "_relationObjects")).Select(item => item.GetComponent<TMP_Text>())
                    .Where(text => text != null).Select(text => text.text).ToArray();
                Check(headers.Count(text => text.StartsWith("선행 조건")) == 2 && headers.All(text => !text.Contains("archive_ui_family")),
                    "family prerequisites show separate required groups without leaking IDs");
                Check(Relations(view).Count(slot => Text(slot, "_title") == knownUpgrade.DisplayName) == 1,
                    "upgrade deduplicated and not filtered by live core level");
                UIItemSlot lockedUpgradeSlot = Relations(view).Single(slot => Text(slot, "_value") == "업그레이드 경로 있음 · 미발견");
                Check(Text(lockedUpgradeSlot, "_title") == "?" && !Button(lockedUpgradeSlot).interactable,
                    "unbuilt upgrade explains the path while remaining locked");

                scroll.verticalNormalizedPosition = .37f;
                ClickSlot(view, Relations(view).Single(slot => Text(slot, "_title") == ally.UnitName));
                Check(unitDetail.gameObject.activeSelf && ((GameObject)Get(view, "_teamControls")).activeSelf,
                    "produced unit link changes category and team");
                Check(Relations(view).Any(slot => Text(slot, "_title") == primary.DisplayName) &&
                    Relations(view).Any(slot => Text(slot, "_title") == "?" && !Button(slot).interactable) &&
                    Relations(view).All(slot => Text(slot, "_title") != disabledProducer.DisplayName),
                    "reverse producers include multiple buildings, lock unbuilt ones, and exclude disabled spawn modules");
                ClickNavigation(view, "_backButton");
                Check(buildingDetail.SelectionId == "archive:" + primary.BuildingId &&
                    Mathf.Abs(scroll.verticalNormalizedPosition - .37f) < .01f,
                    "back restores category, selection, and prior list scroll");
                ClickSlot(view, Relations(view).Single(slot => Text(slot, "_title") == knownUpgrade.DisplayName));
                Check(Relations(view).Any(slot => Text(slot, "_title") == primary.DisplayName) &&
                    Relations(view).Any(slot => Text(slot, "_title") == previous2.DisplayName), "upgrade reverse graph retains multiple parents");

                PrepareRefresh(view);
                Check(view.Navigate(ArchiveCategory.Unit, ally.UnitId, true), "all allied units are public");
                var skillSlots = (List<UIItemSlot>)Get(unitDetail, "_skillSlots");
                Check(skillSlots.Count > 0 && Slots(view).All(slot => Text(slot, "_value") != "NEW"),
                    "existing unit skill content is reused and allies do not show NEW");
                Call(skillSlots[0], "OnEnable");
                Button(skillSlots[0]).onClick.Invoke();
                Check(manager.TopPopup.Id == UIId.SkillDetail && screen.Root.activeSelf && Mathf.Approximately(Time.timeScale, 0),
                    "skill popup covers archive without ending its pause");
                Check(!((CanvasGroup)Get(screen, "_inputGroup")).interactable, "covered archive input is disabled");
                Check(manager.CloseTopPopup() && manager.TopPopup == screen && Mathf.Approximately(Time.timeScale, 0),
                    "closing skill popup returns to a still paused archive");
                Check(((CanvasGroup)Get(screen, "_inputGroup")).interactable, "archive input returns after skill closes");

                ClickNavigation(view, "_enemyButton");
                Check(Slots(view).Any(slot => Text(slot, "_title") == knownEnemy.UnitName && Text(slot, "_value") == "NEW") &&
                    Slots(view).Any(slot => Text(slot, "_title") == "?"), "enemy team uses actual encounter discovery");
                Check(!view.Navigate(ArchiveCategory.Unit, unknownEnemy.UnitId), "unseen enemy cannot be opened directly");
                ClickSlot(view, Slots(view).Single(slot => Text(slot, "_title") == knownEnemy.UnitName));
                Check(!controller.IsNew(ArchiveCategory.Unit, knownEnemy.UnitId), "enemy NEW clears on read");
                Close(manager, view);
                Check(Mathf.Approximately(Time.timeScale, 1.5f), "close restores the captured speed");
                Open(manager, view, 2f);
                Check(Text(unitDetail, "_nameText") == knownEnemy.UnitName &&
                    !((UnityEngine.UI.Button)Get(view, "_backButton")).interactable, "reopen preserves selection and clears history");
                Close(manager, view);
                Check(Mathf.Approximately(Time.timeScale, 2f), "reopen restores its own captured speed");

                foreach (float speed in new[] { 1f, 1.5f, 2f })
                {
                    Open(manager, view, speed);
                    Check(manager.OpenPopup(UIId.SkillDetail), "covered pause fixture opens skill popup");
                    Check(screen.Root.activeSelf && Mathf.Approximately(Time.timeScale, 0), "coverage retains pause at " + speed);
                    manager.CloseAllPopups(UICloseReason.ContextLost);
                    Call(view, "OnDisable");
                    Check(!screen.Root.activeSelf && Mathf.Approximately(Time.timeScale, speed), "context loss restores speed " + speed);
                    Call(view, "OnDisable");
                    Check(Mathf.Approximately(Time.timeScale, speed), "repeated disable cannot restore twice");
                }
                Check(save.TryLoad(ArchiveController.SaveKey, out ArchiveProgressSaveData persisted, out error) &&
                    persisted.UnreadKeys.Count == 0, "read flags persisted only in isolated temporary save");
                return $"PASS: {_checks} archive UI checks (real prefabs; isolated data/save; EditMode lifecycle invocation).";
            }
            finally
            {
                if (view != null) Call(view, "OnDisable");
                if (controller != null) Call(controller, "OnDestroy");
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (Object asset in assets) Object.DestroyImmediate(asset);
                Time.timeScale = previousScale;
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory, false);
            }
        }

        private static void Open(InGameUIManager manager, ArchiveView view, float speed)
        {
            PrepareRefresh(view);
            Time.timeScale = speed;
            Check(manager.OpenPopup(UIId.Archive), "archive opens");
            Call(view, "OnEnable");
            Check(Mathf.Approximately(Time.timeScale, 0), "archive pauses at " + speed);
        }

        private static void Close(InGameUIManager manager, ArchiveView view)
        {
            var close = view.GetComponentInChildren<CloseUtility>(true);
            Call(close, "OnEnable");
            ((UnityEngine.UI.Button)Get(close, "_button")).onClick.Invoke();
            Call(close, "OnDisable");
            Call(view, "OnDisable");
            Check(manager.OpenPopupCount == 0, "archive close button closes its screen");
        }

        private static List<UIItemSlot> Slots(ArchiveView view) => (List<UIItemSlot>)Get(view, "_slots");
        private static UIItemSlot[] Relations(ArchiveView view) => ((List<GameObject>)Get(view, "_relationObjects"))
            .Select(item => item.GetComponent<UIItemSlot>()).Where(slot => slot != null).ToArray();
        private static string Text(object target, string field) => ((TMP_Text)Get(target, field)).text;
        private static UnityEngine.UI.Button Button(UIItemSlot slot) => (UnityEngine.UI.Button)Get(slot, "_button");

        private static void ClickNavigation(ArchiveView view, string field)
        {
            PrepareRefresh(view);
            ((UnityEngine.UI.Button)Get(view, field)).onClick.Invoke();
        }

        private static void ClickSlot(ArchiveView view, UIItemSlot slot)
        {
            Check(Button(slot).interactable, "requested slot is interactive");
            Call(slot, "OnEnable");
            PrepareRefresh(view, slot.gameObject);
            Button(slot).onClick.Invoke();
            Object.DestroyImmediate(slot.gameObject);
        }

        // Runtime의 지연 Destroy는 EditMode에서 실행하지 않는다. 표시·입력 바인딩은 원본을 사용한다.
        private static void PrepareRefresh(ArchiveView view, GameObject keep = null)
        {
            foreach (UIItemSlot slot in Slots(view)) if (slot.gameObject != keep) Object.DestroyImmediate(slot.gameObject);
            Slots(view).Clear();
            var relations = (List<GameObject>)Get(view, "_relationObjects");
            foreach (GameObject item in relations) if (item != keep) Object.DestroyImmediate(item);
            relations.Clear();
            var unit = (UnitPopupView)Get(view, "_unitDetail");
            var skills = (List<UIItemSlot>)Get(unit, "_skillSlots");
            foreach (UIItemSlot slot in skills) Object.DestroyImmediate(slot.gameObject);
            skills.Clear();
        }

        private static List<UnitData> UnitDataFrom(AllyUnitSpawnDatabaseSO database) => database.Classes
            .Where(group => group != null).SelectMany(group => group.UnitPrefabs)
            .Where(prefab => prefab != null).Select(prefab => prefab.GetComponent<Unit_RuntimeStatus>().UnitData)
            .Where(data => data != null && data.Team == UnitTeam.Ally).GroupBy(data => data.UnitId).Select(group => group.First()).ToList();
        private static List<UnitData> UnitDataFrom(EnemyUnitSpawnDatabaseSO database) => database.Factions
            .Where(group => group != null).SelectMany(group => group.UnitPrefabs)
            .Where(prefab => prefab != null).Select(prefab => prefab.GetComponent<Unit_RuntimeStatus>().UnitData)
            .Where(data => data != null && data.Team == UnitTeam.Enemy).GroupBy(data => data.UnitId).Select(group => group.First()).ToList();

        private static void AssertReferences(Object target, params string[] excluded)
        {
            Check(target != null, "required component exists");
            var iterator = new SerializedObject(target).GetIterator();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && !excluded.Contains(iterator.name))
                    Check(iterator.objectReferenceValue != null, target.GetType().Name + "." + iterator.name + " assigned");
        }

        private static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException("Archive UI validation failed: " + message);
            _checks++;
        }
        private static object Get(object target, string field) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private sealed class EmptyArtifactReader : IArtifactReader
        {
            public bool IsInitialized => true;
            public IReadOnlyList<ArtifactInstance> Instances => Array.Empty<ArtifactInstance>();
            public ArtifactCatalog ArtifactCatalog => null;
            public event Action<ArtifactInstance, int, int> StackChanged { add { } remove { } }
            public event Action<IReadOnlyList<ArtifactInstance>> Cleared { add { } remove { } }
            public bool Contains(ArtifactData artifact) => false;
            public bool TryGetById(string id, out ArtifactInstance instance) { instance = null; return false; }
            public List<ArtifactInstance> GetExchangeCandidates(ArtifactData rewardArtifact) => new();
        }
    }
}
#endif
