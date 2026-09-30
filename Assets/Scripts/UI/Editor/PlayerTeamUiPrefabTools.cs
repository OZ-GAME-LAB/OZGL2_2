using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    /// <summary>UI 소유 계층만 프리팹으로 추출한다. 팀 매니저와 원본 씬은 저장하지 않는다.</summary>
    public static class PlayerTeamUiPrefabTools
    {
        public const string PrefabPath = "Assets/Prefabs/UI/Player/PlayerTeamUI.prefab";

        [MenuItem("Game/UI/Replace Team UI With Prefab")]
        public static void ReplaceInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before replacing UI.");
            var scene = SceneManager.GetActiveScene();
            var source = FindOwner(scene);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source) == PrefabPath)
            {
                Selection.activeGameObject = source;
                Debug.Log("[UI/Prefab] Already using PlayerTeamUI; existing overrides preserved.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("PlayerTeamUI prefab is missing.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Team UI With Prefab");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create UI prefab instance");
                var map = CreateMap(source, instance);
                // Keep scene-authored layout/values as instance overrides. Never apply them to the asset.
                foreach (var pair in map)
                {
                    if (pair.Key is GameObject oldObject && pair.Value is GameObject newObject)
                    {
                        Undo.RecordObject(newObject, "Preserve UI object settings");
                        newObject.name = oldObject.name;
                        newObject.layer = oldObject.layer;
                        newObject.tag = oldObject.tag;
                        if (oldObject != source) newObject.SetActive(oldObject.activeSelf);
                    }
                    else if (pair.Key is Component oldComponent && pair.Value is Component newComponent)
                    {
                        Undo.RecordObject(newComponent, "Preserve UI component settings");
                        CopyComponentData(oldComponent, newComponent);
                        RemapReferences(newComponent, map);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(newComponent);
                    }
                }
                // Remap callers outside our UI, e.g. existing building-slot selection targets.
                foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || map.ContainsKey(component) || map.ContainsValue(component)) continue;
                    RemapReferences(component, map);
                }
                instance.name = source.name;
                instance.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
                bool active = source.activeSelf;
                AssertReferenceEquivalence(map);
                Undo.DestroyObjectImmediate(source);
                instance.SetActive(active);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                ValidateReferences(instance, false);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = instance;
                Debug.Log("[UI/Prefab] UI replaced with scene connections retained. Scene not saved; Ctrl+Z restores it.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        // Export and Play Mode verification run only in the existing isolated validation copy.
        public static void ExportAndValidateBatch()
        {
            if (!Application.isBatchMode ||
                !Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Use an isolated UnityUIValidation project.");
            var scene = EditorSceneManager.OpenScene(TeamBuildingUiSetup.ScenePath);
            var source = FindOwner(scene);
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException("Existing prefab will not be overwritten.");
            GameObject clone = null;
            try
            {
                clone = Object.Instantiate(source);
                clone.name = "PlayerTeamUI";
                clone.SetActive(false); // A bare prefab must not subscribe before scene dependencies are connected.
                foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component == null || (component.GetType().Namespace != "Game.UI" &&
                        component.GetType().Namespace != "TMPro" && component.GetType().Namespace != "UnityEngine.UI"))
                        throw new InvalidOperationException("Unexpected non-UI behaviour in exported hierarchy.");
                int externalCount = ClearExternalReferences(clone);
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath, out bool success);
                if (!success || prefab == null) throw new IOException("Failed to save PlayerTeamUI prefab.");
                ValidateReferences(prefab, true);
                if (prefab.activeSelf) throw new InvalidOperationException("Unwired prefab must be inactive.");
                Directory.CreateDirectory("Logs/PlayerTeamUiPrefab");
                File.WriteAllText("Logs/PlayerTeamUiPrefab/export.txt",
                    "Source: " + TeamBuildingUiSetup.ScenePath + "\nPrefab: " + PrefabPath +
                    "\nScene references intentionally left for instance wiring: " + externalCount +
                    "\nUI objects: " + prefab.GetComponentsInChildren<Transform>(true).Length +
                    "\nNo team managers, missing scripts, or dangling references included.\n");
            }
            finally { if (clone != null) Object.DestroyImmediate(clone); }

            ReplaceInActiveScene();
            Undo.PerformUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(FindOwner(scene)))
                throw new InvalidOperationException("Undo failed to restore original UI.");
            Undo.PerformRedo();
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(FindOwner(scene)) != PrefabPath)
                throw new InvalidOperationException("Redo failed to restore prefab instance.");
            ValidateReferences(FindOwner(scene), false);
            File.AppendAllText("Logs/PlayerTeamUiPrefab/export.txt", "Replacement, reference remapping, Undo and Redo passed.\n");
            // Save ONLY the isolated scene so the existing full Play Mode suite runs on the prefab instance.
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save isolated validation scene.");
            TeamBuildingUiValidation.RunStoredBatch();
        }

        private static GameObject FindOwner(Scene scene)
        {
            var owners = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TeamBuildingUiStartup>(true)).ToArray();
            if (owners.Length != 1 || owners[0].transform.parent != null)
                throw new InvalidOperationException("Expected one existing root TeamBuildingUiStartup. No scene was replaced.");
            return owners[0].gameObject;
        }

        private static Dictionary<Object, Object> CreateMap(GameObject source, GameObject target)
        {
            var map = new Dictionary<Object, Object>();
            void Visit(Transform oldNode, Transform newNode)
            {
                if (oldNode != source.transform && oldNode.name != newNode.name)
                    throw new InvalidOperationException("UI hierarchy differs. Preserve custom UI and wire manually: " + oldNode.name);
                if (oldNode.childCount != newNode.childCount)
                    throw new InvalidOperationException("UI child count differs: " + oldNode.name);
                map.Add(oldNode.gameObject, newNode.gameObject);
                var oldComponents = oldNode.GetComponents<Component>();
                var newComponents = newNode.GetComponents<Component>();
                if (oldComponents.Length != newComponents.Length)
                    throw new InvalidOperationException("UI components differ: " + oldNode.name);
                for (int i = 0; i < oldComponents.Length; i++)
                {
                    if (oldComponents[i] == null || newComponents[i] == null ||
                        oldComponents[i].GetType() != newComponents[i].GetType())
                        throw new InvalidOperationException("Missing or changed UI component: " + oldNode.name);
                    map.Add(oldComponents[i], newComponents[i]);
                }
                for (int i = 0; i < oldNode.childCount; i++) Visit(oldNode.GetChild(i), newNode.GetChild(i));
            }
            Visit(source.transform, target.transform);
            return map;
        }

        private static bool IsDataReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) return false;
            if (property.propertyPath.StartsWith("m_Children", StringComparison.Ordinal)) return false;
            switch (property.propertyPath)
            {
                case "m_GameObject": case "m_Script": case "m_CorrespondingSourceObject":
                case "m_PrefabInstance": case "m_PrefabAsset": case "m_Father": return false;
                default: return true;
            }
        }

        private static void CopyComponentData(Component original, Component replacement)
        {
            var source = new SerializedObject(original);
            var target = new SerializedObject(replacement);
            var property = source.GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false; // Copy complete arrays/structs once, not their individual children.
                switch (property.propertyPath)
                {
                    case "m_GameObject": case "m_Script": case "m_CorrespondingSourceObject":
                    case "m_PrefabInstance": case "m_PrefabAsset": case "m_Father":
                    case "m_Children": case "m_RootOrder": continue;
                }
                if (target.FindProperty(property.propertyPath) != null)
                    target.CopyFromSerializedProperty(property);
            }
            target.ApplyModifiedProperties();
        }

        private static void RemapReferences(Component component, Dictionary<Object, Object> map)
        {
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (!IsDataReference(property) || property.objectReferenceValue == null ||
                    !map.TryGetValue(property.objectReferenceValue, out var replacement)) continue;
                if (!changed) Undo.RecordObject(component, "Reconnect UI reference");
                property.objectReferenceValue = replacement;
                changed = true;
            }
            if (!changed) return;
            serialized.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static int ClearExternalReferences(GameObject owner)
        {
            int count = 0;
            foreach (var component in owner.GetComponentsInChildren<Component>(true))
            {
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (!IsDataReference(property)) continue;
                    var reference = property.objectReferenceValue;
                    var target = reference is Component c ? c.gameObject : reference as GameObject;
                    if (target == null || EditorUtility.IsPersistent(target) || target.transform.IsChildOf(owner.transform)) continue;
                    property.objectReferenceValue = null;
                    count++;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return count;
        }

        private static void AssertReferenceEquivalence(Dictionary<Object, Object> map)
        {
            foreach (var pair in map)
            {
                if (!(pair.Key is Component original) || !(pair.Value is Component replacement)) continue;
                var fields = new SerializedObject(original).GetIterator();
                var copied = new SerializedObject(replacement);
                while (fields.Next(true))
                {
                    if (!IsDataReference(fields)) continue;
                    var expected = fields.objectReferenceValue;
                    if (expected != null && map.TryGetValue(expected, out var mapped)) expected = mapped;
                    var actual = copied.FindProperty(fields.propertyPath);
                    if (actual == null || actual.objectReferenceValue != expected)
                        throw new InvalidOperationException("Lost UI reference: " + original.name + "." + fields.propertyPath);
                }
            }
        }

        private static void ValidateReferences(GameObject owner, bool isAsset)
        {
            foreach (var transform in owner.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("Missing script: " + transform.name);
                foreach (var component in transform.GetComponents<Component>())
                {
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (!IsDataReference(property)) continue;
                        var reference = property.objectReferenceValue;
                        if (reference == null && property.objectReferenceInstanceIDValue != 0)
                            throw new InvalidOperationException("Dangling reference: " + component.name + "." + property.propertyPath);
                        if (isAsset && reference != null && !EditorUtility.IsPersistent(reference))
                            throw new InvalidOperationException("Scene reference inside prefab: " + property.propertyPath);
                    }
                }
            }
        }
    }
}
