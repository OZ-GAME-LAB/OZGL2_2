#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Editor
{
    public static class ProgressSetup
    {
        public const string Folder = "Assets/Data/Progress";

        [MenuItem("Game/Progress/Connect Unlocks")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode가 필요합니다.");
            var original = SceneManager.GetActiveScene();
            if (original.isDirty) throw new InvalidOperationException("현재 씬을 먼저 저장해주세요.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Progress");
            var definitions = new[]
            {
                Create("Abundance", "altar.abundance", true, UnlockCondition.TotalDamage, 1, ""),
                Create("Conquest", "altar.conquest", false, UnlockCondition.TotalDamage, 5000, "누적 피해량 5,000 달성"),
                Create("Arcane", "altar.arcane", false, UnlockCondition.SkillKills, 500, "스킬피해로 적 500마리 누적 처치"),
                Create("Guardian", "altar.guardian", false, UnlockCondition.BossQuarter, 4, "4분기 보스웨이브 클리어")
            };
            string[] names = { "Abundance", "Conquest", "Arcane", "Guardian" };
            for (int i = 0; i < names.Length; i++)
            {
                var altar = AssetDatabase.LoadAssetAtPath<AltarData>("Assets/Data/OutGame/Altar/Altar_" + names[i] + ".asset");
                var serialized = new SerializedObject(altar);
                serialized.FindProperty("_unlock").objectReferenceValue = definitions[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(altar);
            }
            string[] prefabs = { "Assets/Prefabs/Core/SaveSystem.prefab", "Assets/Prefabs/Core/Systems.prefab",
                "Assets/Prefabs/UI/OutGame/OutGameSetup.prefab" };
            foreach (string path in prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Connect(root, definitions);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // 현재 인스턴스의 override도 확인하고 필요한 참조만 연결합니다.
            bool changed = false;
            foreach (var root in original.GetRootGameObjects()) changed |= Connect(root, definitions);
            if (changed) EditorSceneManager.SaveScene(original);
            return "해금 4종, 제단 4종, Core의 UnlockManager와 Bootstrap 연결 완료";
        }

        private static UnlockData Create(string name, string id, bool defaultUnlocked, UnlockCondition condition, int required, string description)
        {
            string path = Folder + "/Unlock_" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<UnlockData>(path);
            if (data != null) return data; // 재실행 시 사용자가 조정한 조건을 유지합니다.
            data = ScriptableObject.CreateInstance<UnlockData>();
            var so = new SerializedObject(data);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_defaultUnlocked").boolValue = defaultUnlocked;
            so.FindProperty("_condition").enumValueIndex = (int)condition;
            so.FindProperty("_requiredValue").intValue = required;
            so.FindProperty("_description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        public static bool Connect(GameObject root, UnlockData[] definitions)
        {
            Component bootstrap = root.GetComponentInChildren<BootStrap>(true);
            if (bootstrap == null) bootstrap = root.GetComponentInChildren<OutGameBootstrap>(true);
            if (bootstrap == null) return false;

            bool changed = false;
            var core = bootstrap is BootStrap ? bootstrap.transform : bootstrap.transform.Find("Core");
            if (core == null)
            {
                core = new GameObject("Core").transform;
                core.SetParent(bootstrap.transform, false);
                changed = true;
            }
            var manager = core.GetComponent<UnlockManager>();
            if (manager == null) { manager = core.gameObject.AddComponent<UnlockManager>(); changed = true; }
            var so = new SerializedObject(manager);
            var array = so.FindProperty("_definitions");
            if (array.arraySize != definitions.Length ||
                !definitions.Select((d, i) => array.GetArrayElementAtIndex(i).objectReferenceValue == d).All(same => same))
            {
                array.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(manager)) PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
                changed = true;
            }
            var entry = new SerializedObject(bootstrap);
            var reference = entry.FindProperty("_unlockManager");
            if (reference.objectReferenceValue != manager)
            {
                reference.objectReferenceValue = manager;
                entry.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(bootstrap)) PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrap);
                changed = true;
            }
            return changed;
        }
    }
}
#endif
