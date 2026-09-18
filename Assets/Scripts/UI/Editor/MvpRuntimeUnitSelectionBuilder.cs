using System;
using Game.UI.Samples;
using TMPro;
using Units;
using Units.UnitDatas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Game.UI.Editor.MvpHudBuilder;

namespace Game.UI.Editor
{
    public static class MvpRuntimeUnitSelectionBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/MvpRuntimeUnitSelectionTest.unity";
        private const string DataFolder = "Assets/Data/UI/Tests";

        [MenuItem("Game/UI/Open Runtime Unit Selection Scene")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scenes before opening the sample.");
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Game/UI/Create Missing Runtime Unit Selection Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpUnitUiBuilder.UnitPrefabPath);
                if (prefab == null) throw new InvalidOperationException("Existing unit info prefab is required.");
                EnsureFont();
                var camera = new GameObject("Unit Selection Camera", typeof(Camera), typeof(Physics2DRaycaster)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

                var panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<UnitInfoPanel>();
                var binding = panel.gameObject.AddComponent<RuntimeUnitInfoBinding>();
                Assign(binding, "_panel", panel);
                var ground = new GameObject("Empty Selection Surface", typeof(BoxCollider2D), typeof(RuntimeUnitSelectionTarget));
                ground.transform.position = new Vector3(0, 0, 1);
                ground.GetComponent<BoxCollider2D>().size = new Vector2(100, 100);
                Assign(ground.GetComponent<RuntimeUnitSelectionTarget>(), "_binding", binding);
                var groundFields = new SerializedObject(ground.GetComponent<RuntimeUnitSelectionTarget>());
                groundFields.FindProperty("_clearsSelection").boolValue = true;
                groundFields.ApplyModifiedPropertiesWithoutUndo();

                var ally = CreateUnit(EnsureData("RuntimeSelectionAlly", "검증용 전사", UnitTeam.Ally, 100),
                    new Vector3(-4, 1, 0), new Color32(117, 224, 190, 255), binding);
                var enemy = CreateUnit(EnsureData("RuntimeSelectionEnemy", "검증용 경비병", UnitTeam.Enemy, 150),
                    new Vector3(-.8f, 1, 0), new Color32(245, 143, 133, 255), binding);
                var controls = CreateCanvas("Unit Selection Test Controls", 1);
                controls.SetActive(false);
                var card = Box(controls.transform, "Controls", Vector2.zero, Vector2.zero,
                    new Vector2(40, 40), new Vector2(1190, 320), new Color32(25, 36, 48, 255));
                Label(card, "Title", "실제 유닛 클릭 · 정보 UI 테스트", 34, Color.white, 28, 215, 1094, 50);
                Label(card, "Help", "왼쪽 클릭: 선택 / 빈 공간: 해제 / UI 클릭: 선택 유지", 24,
                    new Color32(145, 169, 182, 255), 28, 170, 1094, 40);
                var status = Label(card, "Status", "초기화 대기", 22, Color.white, 28, 122, 1094, 40);
                var damage = MakeButton(card, "Damage", "피해 -25", 28);
                var heal = MakeButton(card, "Heal", "회복 +20", 306);
                var shield = MakeButton(card, "Shield", "보호막 +20", 584);
                var respawn = MakeButton(card, "Respawn", "유닛 재사용", 862);
                var sample = controls.AddComponent<MvpRuntimeUnitSelectionSample>();
                Assign(sample, "_panel", panel, "_binding", binding, "_statusText", status,
                    "_damageButton", damage, "_healButton", heal, "_shieldButton", shield, "_respawnButton", respawn);
                var sampleFields = new SerializedObject(sample);
                var units = sampleFields.FindProperty("_units");
                var sources = sampleFields.FindProperty("_sources");
                units.arraySize = sources.arraySize = 2;
                units.GetArrayElementAtIndex(0).objectReferenceValue = ally.GetComponent<Unit_Core>();
                units.GetArrayElementAtIndex(1).objectReferenceValue = enemy.GetComponent<Unit_Core>();
                sources.GetArrayElementAtIndex(0).objectReferenceValue = ally;
                sources.GetArrayElementAtIndex(1).objectReferenceValue = enemy;
                sampleFields.ApplyModifiedPropertiesWithoutUndo();
                controls.SetActive(true);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[UI/MvpRuntimeUnitSelectionBuilder] Created " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        private static RuntimeUnitInfoSource CreateUnit(UnitData data, Vector3 position, Color color, RuntimeUnitInfoBinding binding)
        {
            var unit = new GameObject(data.UnitName, typeof(SpriteRenderer), typeof(CircleCollider2D));
            unit.transform.position = position;
            var renderer = unit.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (renderer.sprite == null) throw new InvalidOperationException("Built-in placeholder sprite was not found.");
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = color;
            renderer.sortingOrder = 1;
            unit.transform.localScale = Vector3.one * 3;
            unit.GetComponent<CircleCollider2D>().radius = renderer.sprite.bounds.extents.x;
            var status = unit.AddComponent<Unit_RuntimeStatus>();
            Assign(status, "_unitData", data);
            unit.AddComponent<Unit_Life>();
            unit.AddComponent<Unit_Core>();
            var source = unit.AddComponent<RuntimeUnitInfoSource>();
            var target = unit.AddComponent<RuntimeUnitSelectionTarget>();
            Assign(target, "_source", source, "_binding", binding);
            var label = new GameObject("Unit Label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(unit.transform, false);
            label.transform.localScale = Vector3.one / 3;
            label.transform.localPosition = new Vector3(0, -.4f, 0);
            label.rectTransform.sizeDelta = new Vector2(3, 1);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/NotoSansKR/NotoSansKR SDF.asset");
            label.fontSize = 3;
            label.alignment = TextAlignmentOptions.Center;
            label.text = (data.Team == UnitTeam.Ally ? "아군 · " : "적군 · ") + data.UnitName;
            label.color = color;
            label.GetComponent<MeshRenderer>().sortingOrder = 2;
            return source;
        }

        private static Button MakeButton(Transform parent, string name, string text, float x)
        {
            var button = Button(parent, name, text, 250, 64);
            Place((RectTransform)button.transform, x, 30, 250, 64);
            return button;
        }

        private static UnitData EnsureData(string id, string name, UnitTeam team, float hp)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/UI")) AssetDatabase.CreateFolder("Assets/Data", "UI");
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/Data/UI", "Tests");
            string path = DataFolder + "/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected asset at " + path);
            var data = ScriptableObject.CreateInstance<UnitData>();
            var fields = new SerializedObject(data);
            fields.FindProperty("_unitId").stringValue = "ui-test-" + id;
            fields.FindProperty("_unitName").stringValue = name;
            fields.FindProperty("_team").intValue = (int)team;
            var types = new[] { UnitStatType.MaxHp, UnitStatType.AttackPower, UnitStatType.Defense, UnitStatType.AttackSpeed, UnitStatType.MoveSpeed };
            var values = new[] { hp, 12f, 5f, 1f, 2f };
            var stats = fields.FindProperty("_stats");
            stats.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++)
            {
                var stat = stats.GetArrayElementAtIndex(i);
                stat.FindPropertyRelative("_statType").intValue = (int)types[i];
                stat.FindPropertyRelative("_value").floatValue = values[i];
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(data, path);
            return data;
        }
    }
}
