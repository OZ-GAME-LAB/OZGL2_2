using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.UI.Samples;
using OZGL.KDH;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>기존 프리뷰를 보존하고 UI 소유의 별도 건설 연동 검증 씬을 만든다.</summary>
    public static class RuntimeBuildingUiSetup
    {
        public const string ScenePath = "Assets/Scenes/UI/PlayerBuildingIntegration.unity";
        public const string DataFolder = "Assets/Data/UI/BuildingIntegration";

        [MenuItem("Game/UI/Create Building Integration Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) { Debug.Log("[UI] Existing building integration scene preserved."); return; }
            if (!File.Exists(PlayerUiBuilder.ScenePath)) throw new InvalidOperationException("Create PlayerUI first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard pending scene edits yourself first.");
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/Data/UI", "BuildingIntegration");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(PlayerUiBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var roots = scene.GetRootGameObjects();
                T Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var preview = Find<PlayerUiPreviewBindings>();
                var sample = Find<MvpRuntimeHudSample>();
                var flow = Find<GameFlowController>();
                var wallet = Find<RunCurrencyManager>();
                var coreProgress = Find<BuildingCoreProgress>();
                var catalog = Find<BuildingCatalogPanel>();
                var info = Find<BuildingInfoPanel>();
                var actions = Find<BuildingActionPanel>();
                Set(preview, "_useRuntimeBuildings", true);
                var navigation = Find<PlayerUiNavigation>();
                var nav = new SerializedObject(navigation); nav.FindProperty("_catalogButtons").arraySize = 0; nav.ApplyModifiedPropertiesWithoutUndo();
                var prefabPath = DataFolder + "/PreviewBuilding.prefab";
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (visual == null)
                {
                    var temp = new GameObject("UI integration building", typeof(SpriteRenderer));
                    temp.GetComponent<SpriteRenderer>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    visual = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
                    UnityEngine.Object.DestroyImmediate(temp);
                }
                var mine = Data("Resource", "ui-integration-mine", "골드 생산소", 30, 0, true, visual);
                var support = Data("Support", "ui-integration-support", "지원 건물", 20, 2, false, visual);
                var database = AssetDatabase.LoadAssetAtPath<BuildingDatabase>(DataFolder + "/Database.asset");
                if (database == null)
                {
                    database = ScriptableObject.CreateInstance<BuildingDatabase>();
                    var fields = new SerializedObject(database); var list = fields.FindProperty("buildings"); list.arraySize = 2;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = mine; list.GetArrayElementAtIndex(1).objectReferenceValue = support;
                    fields.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(database, DataFolder + "/Database.asset");
                }
                var owner = new GameObject("UI Building Integration - real building API, fixture data");
                owner.SetActive(false);
                var controller = owner.AddComponent<BuildingBuildController>();
                MvpHudBuilder.Assign(controller, "database", database,
                    "worldCamera", roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single());
                MvpHudBuilder.Assign(sample, "_buildingController", controller, "_buildingCoreProgress", coreProgress);
                // UI 지도 버튼이 입력을 소유한다. 원본 임시 메뉴의 월드 클릭 입력만 차단한다.
                Set(controller, "slotMask", 0);
                var binding = owner.AddComponent<RuntimeBuildingUiBinding>();
                var hint = PlayerUiBuilder.Ref<TMP_Text>(preview, "_hint");
                MvpHudBuilder.Assign(binding, "_catalog", catalog, "_info", info, "_actions", actions,
                    "_controller", controller, "_database", database, "_wallet", wallet, "_flow", flow, "_feedbackText", hint);
                var buttons = roots.SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray();
                var bindingFields = new SerializedObject(binding); var slots = bindingFields.FindProperty("_slots"); slots.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    var slotObject = new GameObject("Build Slot " + (i + 1), typeof(BoxCollider2D));
                    slotObject.transform.SetParent(owner.transform); slotObject.transform.position = new Vector3(-5 + i, 0, 0);
                    var slot = slotObject.AddComponent<BuildingSlot>(); slots.GetArrayElementAtIndex(i).objectReferenceValue = slot;
                    var button = buttons.Single(b => b.name == "BuildPlot" + i);
                    var view = button.gameObject.AddComponent<RuntimeBuildingSlotButton>();
                    MvpHudBuilder.Assign(view, "_slot", slot, "_binding", binding, "_flow", flow, "_button", button,
                        "_label", button.transform.Find("Caption").GetComponent<TMP_Text>(), "_emptyIcon", button.transform.Find("Plus").gameObject);
                }
                bindingFields.ApplyModifiedPropertiesWithoutUndo();
                var launch = buttons.Single(b => b.name == "Build" && b.transform.parent.name == "BottomBar");
                var launchView = launch.gameObject.AddComponent<RuntimeBuildingSlotButton>();
                MvpHudBuilder.Assign(launchView, "_binding", binding, "_flow", flow, "_button", launch);
                Set(launchView, "_selectFirstEmptySlot", true);
                owner.SetActive(true);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Integration scene save failed.");
                Debug.Log("[UI] Created " + ScenePath + ". Test prices only; team map/build settings preserved.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static BuildingData Data(string file, string id, string name, int gold, int gems, bool producer, GameObject visual)
        {
            string path = DataFolder + "/" + file + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data != null) return data;
            data = ScriptableObject.CreateInstance<BuildingData>();
            var so = new SerializedObject(data);
            so.FindProperty("buildingId").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = "실제 건물 API 연결 검증용 데이터입니다. 정식 밸런스가 아닙니다.";
            so.FindProperty("buildingType").enumValueIndex = (int)(producer ? BuildingType.Producer : BuildingType.Support);
            so.FindProperty("prefab").objectReferenceValue = visual;
            so.FindProperty("icon").objectReferenceValue = visual.GetComponent<SpriteRenderer>().sprite;
            var costs = so.FindProperty("buildCost"); costs.arraySize = gems == 0 ? 1 : 2;
            costs.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex = (int)BuildingResourceType.Gold;
            costs.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = gold;
            if (gems != 0)
            {
                costs.GetArrayElementAtIndex(1).FindPropertyRelative("type").enumValueIndex = (int)BuildingResourceType.Gem;
                costs.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = gems;
            }
            so.FindProperty("production.enabled").boolValue = producer;
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(data, path); return data;
        }

        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (value is bool flag) p.boolValue = flag; else p.intValue = (int)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
