using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.InGame.Editor
{
    /// <summary>저장된 Test UI의 복사본만 변환한다. 프리팹 제작과 실제 씬 설치는 별도로 실행한다.</summary>
    public static class InGameUIMigration
    {
        public const string ScenePath = "Assets/Scenes/Test/Test.unity";
        public const string FolderPath = "Assets/Prefabs/UI/Test";
        public const string RootPrefabPath = FolderPath + "/InGameUIRoot.prefab";

        private sealed class ScreenEntry
        {
            public UIId Id;
            public UIScreen Screen;
            public string AssetPath;
            public UILayer Layer;
            public bool AllowUserClose;
        }

        private sealed class ReferenceLink
        {
            public Component Owner;
            public string Path;
            public Object Value;
        }

        [MenuItem("Game/UI/InGame/Create Replacement Prefabs")]
        private static void CreateFromMenu() => Debug.Log(CreatePrefab());

        [MenuItem("Game/UI/InGame/Install Replacement In Test")]
        private static void InstallFromMenu() => Debug.Log(InstallInTest());

        public static string CreatePrefab()
        {
            Scene scene = RequireSavedTestScene();
            GameObject original = FindOriginalRoot(scene);
            int overrideCount = PrefabUtility.GetPropertyModifications(original)?.Length ?? 0;
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject staging = null;
            try
            {
                staging = new GameObject("UI Migration Staging");
                staging.SetActive(false);
                SceneManager.MoveGameObjectToScene(staging, preview);
                // 비활성 부모 아래 복제하므로 기존 UI의 OnEnable을 실행하지 않는다.
                GameObject copy = Object.Instantiate(original, staging.transform, false);
                copy.SetActive(false);
                copy.name = "InGameUIRoot";
                if (PrefabUtility.IsPartOfPrefabInstance(copy))
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);

                var replacements = new Dictionary<Object, Object>();
                CreateReplacementComponents(copy, replacements);
                foreach (var pair in replacements)
                    CopySharedFields((Component)pair.Key, (Component)pair.Value, replacements);

                ConvertIcons(copy);
                ConfigureCatalogSlots(copy, replacements);
                ConfigureRewardSlots(copy, replacements);

                var manager = copy.AddComponent<InGameUIManager>();
                var hudPresenter = copy.AddComponent<HudPresenter>();
                ConfigureHudPresenter(copy, hudPresenter, replacements);
                List<ScreenEntry> screens = ConfigureScreens(copy, replacements);
                ConfigureCloseButtons(copy, replacements);
                RemoveLegacyComponents(copy, replacements);
                ClearExternalSceneReferences(copy);

                Assign(manager, "_hud", One<GameHudView>(copy));
                Assign(manager, "_hudPresenter", hudPresenter);
                Assign(manager, "_building", One<BuildingUIPresenter>(copy));
                Assign(manager, "_artifact", One<ArtifactRewardPresenter>(copy));
                Assign(manager, "_flow", One<RunFlowPresenter>(copy));
                Assign(manager, "_decision", One<RunDecisionView>(copy));
                AssignArray(manager, "_shortcuts", copy.GetComponentsInChildren<BuildingShortcut>(true));

                EnsureFolder(FolderPath);
                // 자식 프리팹 경계 밖을 가리키는 참조는 최종 조립본의 override로 다시 연결한다.
                List<ReferenceLink> links = CaptureReferences(copy);
                foreach (ScreenEntry entry in screens.Where(e => e.Id != UIId.Hud))
                    SaveNestedPrefab(entry.Screen.gameObject, entry.AssetPath);
                ScreenEntry hud = screens.Single(e => e.Id == UIId.Hud);
                SaveNestedPrefab(hud.Screen.gameObject, hud.AssetPath);
                RestoreReferences(links);
                RegisterScreens(manager, screens);
                ValidateNewRoot(copy);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, RootPrefabPath, out bool success);
                if (!success || saved == null) throw new IOException("UI 루트 프리팹 저장에 실패했습니다.");
                return $"Created {RootPrefabPath}; copied {overrideCount} source overrides; " +
                    $"{screens.Count} registered screens. Original MainUI and BootStrap were not changed.";
            }
            finally
            {
                if (staging != null) Object.DestroyImmediate(staging);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static string InstallInTest()
        {
            Scene scene = RequireSavedTestScene();
            GameObject original = FindOriginalRoot(scene);
            var existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<InGameUIManager>(true)).ToArray();
            if (existing.Length != 0)
                throw new InvalidOperationException("Test 씬에 새 UI가 이미 있습니다. 중복 설치하지 않습니다.");
            BootStrap bootstrap = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BootStrap>(true)).Single();
            var bootstrapData = new SerializedObject(bootstrap);
            SerializedProperty managerProperty = Required(bootstrapData, "_inGameUIManager");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            if (prefab == null) throw new InvalidOperationException("CreatePrefab을 먼저 실행하세요.");

            bool previousActive = original.activeSelf;
            Object previousManager = managerProperty.objectReferenceValue;
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.SetActive(false);
                original.SetActive(false);
                managerProperty.objectReferenceValue = instance.GetComponent<InGameUIManager>();
                bootstrapData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(original);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrap);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Test 씬 저장에 실패했습니다.");
                return "Installed InGameUIRoot in Test. MainUI is retained and disabled; " +
                    "BootStrap._inGameUIManager points to the new inactive root.";
            }
            catch
            {
                original.SetActive(previousActive);
                bootstrapData.Update();
                Required(bootstrapData, "_inGameUIManager").objectReferenceValue = previousManager;
                bootstrapData.ApplyModifiedPropertiesWithoutUndo();
                if (instance != null) Object.DestroyImmediate(instance);
                throw;
            }
        }

        private static Scene RequireSavedTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Mode를 종료한 뒤 실행하세요.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("저장된 Assets/Scenes/Test/Test.unity를 활성 씬으로 여세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("열린 씬에 저장되지 않은 변경이 있습니다. 먼저 저장하세요.");
            return scene;
        }

        private static GameObject FindOriginalRoot(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects().Where(root => root.name == "MainUI").ToArray();
            if (roots.Length != 1 || roots[0].GetComponent<TeamBuildingUiStartup>() == null)
                throw new InvalidOperationException("Test 씬의 원본 MainUI/TeamBuildingUiStartup을 찾지 못했습니다.");
            return roots[0];
        }

        private static void CreateReplacementComponents(GameObject root, Dictionary<Object, Object> map)
        {
            AddReplacements<GameUIController, GameHudView>(root, map);
            AddReplacements<RuntimeBuildingUiBinding, BuildingUIPresenter>(root, map);
            AddReplacements<BuildingCatalogPanel, BuildingCatalogView>(root, map);
            AddReplacements<BuildingInfoPanel, BuildingInfoView>(root, map);
            AddReplacements<BuildingActionPanel, BuildingActionView>(root, map);
            AddReplacements<RuntimeBuildingSlotButton, BuildingShortcut>(root, map);
            AddReplacements<PlayerBuildingPopupLayout, BuildingPopupLayout>(root, map);
            AddReplacements<CoreGameLoopUiBinding, RunFlowPresenter>(root, map);
            AddReplacements<CoreRunDecisionBinding, RunDecisionView>(root, map);
            AddReplacements<ArtifactRewardPanel, ArtifactRewardView>(root, map);
            AddReplacements<ArtifactRewardBinding, ArtifactRewardPresenter>(root, map);
            AddReplacements<RunSettlementPanel, SettlementView>(root, map);
            AddReplacements<PlayerPopup, UIScreen>(root, map);
        }

        private static void AddReplacements<TOld, TNew>(GameObject root, Dictionary<Object, Object> map)
            where TOld : Component where TNew : Component
        {
            foreach (TOld old in root.GetComponentsInChildren<TOld>(true))
                map.Add(old, old.gameObject.AddComponent<TNew>());
        }

        private static void CopySharedFields(Component source, Component target, Dictionary<Object, Object> map)
        {
            var sourceData = new SerializedObject(source);
            var targetData = new SerializedObject(target);
            SerializedProperty field = sourceData.GetIterator();
            bool enterChildren = true;
            while (field.Next(enterChildren))
            {
                enterChildren = false;
                if (field.name == "m_Script" || field.name == "m_GameObject" || field.name == "m_Name" ||
                    field.name == "m_CorrespondingSourceObject" || field.name == "m_PrefabInstance" ||
                    field.name == "m_PrefabAsset" || field.name == "m_EditorClassIdentifier") continue;
                // 후보 카드만 별도 변환한다. 나머지 직렬화 값은 원래 값 그대로 복사한다.
                if (source is BuildingCatalogPanel &&
                    (field.propertyPath == "_cards" || field.propertyPath.StartsWith("_cards.", StringComparison.Ordinal))) continue;
                SerializedProperty destination = targetData.FindProperty(field.propertyPath);
                if (destination == null || destination.propertyType != field.propertyType) continue;
                // Card/ActionRow는 새 클래스의 타입이 다르므로 구조 전체가 아닌 같은 이름의 값만 복사한다.
                if (field.isArray && field.propertyType != SerializedPropertyType.String)
                {
                    destination.arraySize = field.arraySize;
                    enterChildren = true;
                    continue;
                }
                if (field.propertyType == SerializedPropertyType.Generic)
                {
                    enterChildren = true;
                    continue;
                }
                if (field.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Object value = field.objectReferenceValue;
                    destination.objectReferenceValue = value != null && map.TryGetValue(value, out Object replacement)
                        ? replacement : value;
                }
                else if (field.propertyType == SerializedPropertyType.Enum)
                    destination.intValue = field.intValue;
                else targetData.CopyFromSerializedProperty(field);
            }
            targetData.ApplyModifiedPropertiesWithoutUndo();
            RemapReferences(target, map);
        }

        private static void RemapReferences(Component target, Dictionary<Object, Object> map)
        {
            var data = new SerializedObject(target);
            SerializedProperty field = data.GetIterator();
            while (field.Next(true))
            {
                if (field.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = field.objectReferenceValue;
                if (value != null && map.TryGetValue(value, out Object replacement))
                    field.objectReferenceValue = replacement;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConvertIcons(GameObject root)
        {
            foreach (PlayerUiIcon old in root.GetComponentsInChildren<PlayerUiIcon>(true))
            {
                // Graphic은 같은 오브젝트에 두 개를 붙일 수 없어 임시 복사본에 값을 보관한다.
                var temporary = new GameObject("Icon Settings", typeof(RectTransform));
                temporary.transform.SetParent(root.transform, false);
                var snapshot = temporary.AddComponent<UIIcon>();
                var emptyMap = new Dictionary<Object, Object>();
                CopySharedFields(old, snapshot, emptyMap);
                List<ReferenceLink> links = CaptureReferences(root).Where(link => link.Value == old).ToList();
                GameObject owner = old.gameObject;
                Object.DestroyImmediate(old);
                var replacement = owner.AddComponent<UIIcon>();
                CopySharedFields(snapshot, replacement, emptyMap);
                foreach (ReferenceLink link in links) link.Value = replacement;
                RestoreReferences(links);
                Object.DestroyImmediate(temporary);
            }
        }

        private static void ConfigureCatalogSlots(GameObject root, Dictionary<Object, Object> map)
        {
            foreach (BuildingCatalogPanel old in root.GetComponentsInChildren<BuildingCatalogPanel>(true))
            {
                var source = new SerializedObject(old);
                var target = new SerializedObject(map[old]);
                SerializedProperty cards = Required(source, "_cards");
                SerializedProperty newCards = Required(target, "_cards");
                newCards.arraySize = cards.arraySize;
                for (int i = 0; i < cards.arraySize; i++)
                {
                    SerializedProperty card = cards.GetArrayElementAtIndex(i);
                    var button = (Button)card.FindPropertyRelative("Button").objectReferenceValue;
                    var slot = button.gameObject.AddComponent<UIItemSlot>();
                    Assign(slot, "_button", button);
                    Assign(slot, "_title", card.FindPropertyRelative("Name").objectReferenceValue);
                    Assign(slot, "_value", card.FindPropertyRelative("Price").objectReferenceValue);
                    SerializedProperty newCard = newCards.GetArrayElementAtIndex(i);
                    newCard.FindPropertyRelative("Slot").objectReferenceValue = slot;
                    newCard.FindPropertyRelative("Summary").objectReferenceValue =
                        card.FindPropertyRelative("Summary").objectReferenceValue;
                }
                target.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureRewardSlots(GameObject root, Dictionary<Object, Object> map)
        {
            foreach (ArtifactRewardPanel old in root.GetComponentsInChildren<ArtifactRewardPanel>(true))
            {
                var target = new SerializedObject(map[old]);
                SerializedProperty cards = Required(target, "_cards");
                for (int i = 0; i < cards.arraySize; i++)
                {
                    SerializedProperty card = cards.GetArrayElementAtIndex(i);
                    var button = (Button)card.FindPropertyRelative("Button").objectReferenceValue;
                    var slot = button.gameObject.AddComponent<UIItemSlot>();
                    Assign(slot, "_button", button);
                    Assign(slot, "_icon", card.FindPropertyRelative("Icon").objectReferenceValue);
                    Assign(slot, "_title", card.FindPropertyRelative("Name").objectReferenceValue);
                    Assign(slot, "_value", card.FindPropertyRelative("Rarity").objectReferenceValue);
                    Assign(slot, "_selection", card.FindPropertyRelative("Selection").objectReferenceValue);
                    card.FindPropertyRelative("Slot").objectReferenceValue = slot;
                }
                target.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureHudPresenter(GameObject root, HudPresenter presenter,
            Dictionary<Object, Object> map)
        {
            var startup = new SerializedObject(One<TeamBuildingUiStartup>(root));
            var oldHud = new SerializedObject(One<CoreHudBinding>(root));
            Assign(presenter, "_ui", map[One<GameUIController>(root)]);
            Assign(presenter, "_quarterText", Required(oldHud, "_quarterText").objectReferenceValue);
            Assign(presenter, "_gemText", Required(startup, "_gemText").objectReferenceValue);
            Assign(presenter, "_hint", Required(startup, "_hint").objectReferenceValue);
        }

        private static List<ScreenEntry> ConfigureScreens(GameObject root, Dictionary<Object, Object> map)
        {
            var screens = new List<ScreenEntry>();
            GameUIController oldHud = One<GameUIController>(root);
            var hud = (GameHudView)map[oldHud];
            var hudScreen = hud.gameObject.AddComponent<UIScreen>();
            Assign(hudScreen, "_root", hud.gameObject);
            AssignArray(hudScreen, "_inputRoots", new[]
            {
                (RectTransform)hud.transform.Find("TopBar"),
                (RectTransform)hud.transform.Find("BottomBar")
            });
            AddScreen(screens, UIId.Hud, hudScreen, "InGameHUD", UILayer.Hud, false);

            var oldHudData = new SerializedObject(oldHud);
            UIScreen wave = WrapPanel(hud.transform, oldHudData, "_waveRewardPanel", "WaveReward",
                Required(oldHudData, "_continueButton").objectReferenceValue);
            UIScreen result = WrapPanel(hud.transform, oldHudData, "_runResultPanel", "RunResult",
                Required(oldHudData, "_restartButton").objectReferenceValue);
            UIScreen message = WrapPanel(hud.transform, oldHudData, "_messagePanel", "Message", null);
            Assign(hud, "_waveRewardScreen", wave);
            Assign(hud, "_runResultScreen", result);
            Assign(hud, "_messageScreen", message);
            AddScreen(screens, UIId.WaveReward, wave, "WaveReward", UILayer.Modal, false);
            AddScreen(screens, UIId.RunResult, result, "RunResult", UILayer.Modal, false);
            AddScreen(screens, UIId.Message, message, "Message", UILayer.Modal, true);

            CoreRunDecisionBinding oldDecision = One<CoreRunDecisionBinding>(root);
            var decisionData = new SerializedObject(oldDecision);
            UIScreen decision = WrapPanel(hud.transform, decisionData, "_panelRoot", "RunDecision",
                Required(decisionData, "_finishButton").objectReferenceValue);
            Assign(map[oldDecision], "_screen", decision);
            AddScreen(screens, UIId.RunDecision, decision, "RunDecision", UILayer.Modal, false);

            var catalog = One<BuildingCatalogView>(root);
            var info = One<BuildingInfoView>(root);
            AddScreen(screens, UIId.BuildingCatalog, catalog.Popup, "BuildingCatalog", UILayer.Popup, true);
            AddScreen(screens, UIId.BuildingInfo, Ref<UIScreen>(info, "_playerPopup"),
                "BuildingInfo", UILayer.Popup, true);

            ArtifactRewardPanel oldReward = One<ArtifactRewardPanel>(root);
            var rewardScreen = oldReward.gameObject.AddComponent<UIScreen>();
            Assign(rewardScreen, "_root", Ref<GameObject>(oldReward, "_panelRoot"));
            Assign(rewardScreen, "_initialFocus", Ref<Button>(oldReward, "_confirmButton"));
            Assign(map[oldReward], "_screen", rewardScreen);
            AddScreen(screens, UIId.ArtifactReward, rewardScreen, "ArtifactReward", UILayer.Modal, false);
            return screens;
        }

        private static UIScreen WrapPanel(Transform parent, SerializedObject source, string property,
            string name, Object focus)
        {
            var panel = (GameObject)Required(source, property).objectReferenceValue;
            if (panel == null) throw new InvalidOperationException(property + " 패널 참조가 없습니다.");
            Transform previousParent = panel.transform.parent;
            int sibling = panel.transform.GetSiblingIndex();
            var wrapper = new GameObject(name + " Screen", typeof(RectTransform));
            wrapper.layer = panel.layer;
            var rect = (RectTransform)wrapper.transform;
            rect.SetParent(previousParent != null ? previousParent : parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            if (previousParent is RectTransform parentRect) rect.pivot = parentRect.pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(sibling);
            panel.transform.SetParent(rect, false);
            var screen = wrapper.AddComponent<UIScreen>();
            Assign(screen, "_root", panel);
            Assign(screen, "_initialFocus", focus);
            return screen;
        }

        private static void AddScreen(List<ScreenEntry> screens, UIId id, UIScreen screen,
            string assetName, UILayer layer, bool allowClose)
        {
            screens.Add(new ScreenEntry { Id = id, Screen = screen,
                AssetPath = FolderPath + "/" + assetName + ".prefab",
                Layer = layer, AllowUserClose = allowClose });
        }

        private static void ConfigureCloseButtons(GameObject root, Dictionary<Object, Object> map)
        {
            foreach (PlayerPopup old in root.GetComponentsInChildren<PlayerPopup>(true))
            {
                var data = new SerializedObject(old);
                SerializedProperty buttons = Required(data, "_closeButtons");
                for (int i = 0; i < buttons.arraySize; i++)
                    AddCloseUtility((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue, (UIScreen)map[old]);
            }
            foreach (BuildingInfoPanel old in root.GetComponentsInChildren<BuildingInfoPanel>(true))
            {
                PlayerPopup popup = Ref<PlayerPopup>(old, "_playerPopup");
                if (popup != null)
                    AddCloseUtility(Ref<Button>(old, "_closeButton"), (UIScreen)map[popup]);
            }
        }

        private static void AddCloseUtility(Button button, UIScreen screen)
        {
            if (button == null) return;
            if (!button.TryGetComponent(out CloseUtility close)) close = button.gameObject.AddComponent<CloseUtility>();
            Assign(close, "_button", button);
            Assign(close, "_screen", screen);
        }

        private static void RemoveLegacyComponents(GameObject root, Dictionary<Object, Object> map)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null && !map.ContainsKey(component) && !IsRemovedBinding(component))
                    RemapReferences(component, map);
            foreach (Object old in map.Keys) Object.DestroyImmediate(old);
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (IsRemovedBinding(component)) Object.DestroyImmediate(component);
        }

        private static bool IsRemovedBinding(Component component) =>
            component is TeamBuildingUiStartup || component is PlayerUiNavigation ||
            component is CoreHudBinding || component is RunGoldHudBinding;

        private static void ClearExternalSceneReferences(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var data = new SerializedObject(component);
                SerializedProperty field = data.GetIterator();
                while (field.Next(true))
                {
                    if (!IsDataReference(field)) continue;
                    Object value = field.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value)) continue;
                    Transform owner = value is GameObject go ? go.transform :
                        value is Component reference ? reference.transform : null;
                    if (owner != null && !owner.IsChildOf(root.transform)) field.objectReferenceValue = null;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static List<ReferenceLink> CaptureReferences(GameObject root)
        {
            var links = new List<ReferenceLink>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var data = new SerializedObject(component);
                SerializedProperty field = data.GetIterator();
                while (field.Next(true))
                    if (IsDataReference(field) && field.objectReferenceValue != null)
                        links.Add(new ReferenceLink { Owner = component, Path = field.propertyPath,
                            Value = field.objectReferenceValue });
            }
            return links;
        }

        private static bool IsDataReference(SerializedProperty field)
        {
            if (field.propertyType != SerializedPropertyType.ObjectReference) return false;
            // Transform과 프리팹 소유 관계는 Unity가 관리한다. UI 참조만 복구한다.
            return field.name != "m_Script" && field.name != "m_GameObject" &&
                field.name != "m_Father" && !field.propertyPath.StartsWith("m_Children", StringComparison.Ordinal) &&
                field.name != "m_CorrespondingSourceObject" && field.name != "m_PrefabInstance" &&
                field.name != "m_PrefabAsset";
        }

        private static void RestoreReferences(List<ReferenceLink> links)
        {
            foreach (ReferenceLink link in links)
            {
                if (link.Owner == null) continue;
                var data = new SerializedObject(link.Owner);
                SerializedProperty field = data.FindProperty(link.Path);
                if (field == null) continue;
                field.objectReferenceValue = link.Value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RegisterScreens(InGameUIManager manager, List<ScreenEntry> screens)
        {
            var data = new SerializedObject(manager);
            SerializedProperty entries = Required(data, "_screens");
            entries.arraySize = screens.Count;
            for (int i = 0; i < screens.Count; i++)
            {
                ScreenEntry screen = screens[i];
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Id").intValue = (int)screen.Id;
                entry.FindPropertyRelative("Instance").objectReferenceValue = screen.Screen;
                // 조립본에 이미 배치된 화면은 Instance만 등록한다. Prefab은 지연 생성 화면용이다.
                entry.FindPropertyRelative("Prefab").objectReferenceValue = null;
                entry.FindPropertyRelative("Parent").objectReferenceValue = screen.Screen.transform.parent;
                entry.FindPropertyRelative("Layer").intValue = (int)screen.Layer;
                entry.FindPropertyRelative("AllowUserClose").boolValue = screen.AllowUserClose;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateNewRoot(GameObject root)
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) throw new InvalidOperationException("변환한 UI에 Missing Script가 있습니다.");
                if (component.GetType().Namespace == "Game.UI")
                    throw new InvalidOperationException("아직 변환하지 않은 UI 컴포넌트: " + component.GetType().Name);
            }
        }

        private static void SaveNestedPrefab(GameObject root, string path)
        {
            if (!path.StartsWith(FolderPath + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("허용된 UI Test 폴더 밖에는 저장하지 않습니다.");
            GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path,
                InteractionMode.AutomatedAction, out bool success);
            if (!success || asset == null) throw new IOException("프리팹 저장 실패: " + path);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static T One<T>(GameObject root) where T : Component
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            if (values.Length != 1)
                throw new InvalidOperationException(typeof(T).Name + " 컴포넌트가 정확히 1개 있어야 합니다.");
            return values[0];
        }

        private static T Ref<T>(Object owner, string name) where T : Object =>
            (T)Required(new SerializedObject(owner), name).objectReferenceValue;

        private static SerializedProperty Required(SerializedObject owner, string name) =>
            owner.FindProperty(name) ?? throw new InvalidOperationException(owner.targetObject.GetType().Name + "." + name + " 필드가 없습니다.");

        private static void Assign(Object owner, string name, Object value)
        {
            var data = new SerializedObject(owner);
            Required(data, name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(Object owner, string name, T[] values) where T : Object
        {
            var data = new SerializedObject(owner);
            SerializedProperty array = Required(data, name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
