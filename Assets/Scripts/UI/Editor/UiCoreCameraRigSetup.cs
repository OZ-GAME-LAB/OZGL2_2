using System;
using System.IO;
using System.Linq;
using Game.Cameras;
using Game.Core;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.Editor
{
    /// <summary>UI 소유 테스트 씬에 최신 Core가 요구하는 카메라 참조를 연결한다.</summary>
    public static class UiCoreCameraRigSetup
    {
        private static readonly string[] ScenePaths =
        {
            PlayerUiBuilder.ScenePath,
            RuntimeBuildingUiSetup.ScenePath,
            RuntimeBuildingWorldUiSetup.ScenePath,
            TeamBuildingUiSetup.ScenePath,
            MvpRuntimeHudBuilder.ScenePath,
            MvpBuildingPhaseBuilder.ScenePath
        };

        public static void ConnectExistingScenes()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run camera migration only in a separate batch Editor.");

            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("A loaded scene has unsaved changes.");

            foreach (string path in ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var flow = roots.SelectMany(root => root.GetComponentsInChildren<GameFlowController>(true)).Single();
                var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                var camera = cameras.SingleOrDefault(candidate => candidate.CompareTag("MainCamera"))
                    ?? cameras.Single();
                if (!Ensure(scene, flow, camera)) continue;
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new IOException("Failed to save UI camera reference: " + path);
                Debug.Log("[UI/CoreCamera] Connected " + path);
            }
        }

        public static bool Ensure(Scene scene, GameFlowController flow, Camera worldCamera)
        {
            if (!scene.IsValid() || !scene.isLoaded || flow == null || worldCamera == null ||
                flow.gameObject.scene != scene || worldCamera.gameObject.scene != scene)
                throw new ArgumentException("Core and world camera must belong to the loaded UI scene.");

            var flowFields = new SerializedObject(flow);
            var cameraField = flowFields.FindProperty("_cameraController");
            if (cameraField == null)
                throw new InvalidOperationException("Core camera reference contract changed.");
            if (cameraField.objectReferenceValue != null) return false;
            if (scene.GetRootGameObjects().Any(root => root.name == "UI Core Camera Rig"))
                throw new InvalidOperationException("An unlinked UI camera rig already exists in " + scene.path);

            var rig = new GameObject("UI Core Camera Rig");
            SceneManager.MoveGameObjectToScene(rig, scene);
            rig.SetActive(false);
            var brain = worldCamera.GetComponent<CinemachineBrain>();
            if (brain == null) brain = worldCamera.gameObject.AddComponent<CinemachineBrain>();
            var baseCamera = CreateCamera(rig.transform, "Base View", worldCamera, 10);
            var battlefieldCamera = CreateCamera(rig.transform, "Battlefield View", worldCamera, 0);
            var focusCamera = CreateCamera(rig.transform, "Focus View", worldCamera, -1);
            var focusTarget = new GameObject("Focus Target").transform;
            focusTarget.SetParent(rig.transform, false);
            var controller = rig.AddComponent<InGameCameraController>();
            controller.enabled = false; // UI 테스트 씬의 월드 클릭은 기존 입력 소유자가 처리한다.
            var controllerFields = new SerializedObject(controller);
            SetReference(controllerFields, "_brain", brain);
            SetReference(controllerFields, "_worldCamera", worldCamera);
            SetReference(controllerFields, "_baseCamera", baseCamera);
            SetReference(controllerFields, "_battlefieldCamera", battlefieldCamera);
            SetReference(controllerFields, "_focusCamera", focusCamera);
            SetReference(controllerFields, "_gameFlowController", flow);
            SetReference(controllerFields, "_focusTarget", focusTarget);
            controllerFields.ApplyModifiedPropertiesWithoutUndo();
            cameraField.objectReferenceValue = controller;
            flowFields.ApplyModifiedPropertiesWithoutUndo();
            rig.SetActive(true);
            return true;
        }

        private static CinemachineCamera CreateCamera(Transform parent, string name, Camera source, int priority)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var camera = item.AddComponent<CinemachineCamera>();
            camera.Lens = LensSettings.FromCamera(source);
            camera.Priority = priority;
            return camera;
        }

        private static void SetReference(SerializedObject fields, string name, UnityEngine.Object value)
        {
            var property = fields.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Camera field changed: " + name);
            property.objectReferenceValue = value;
        }
    }
}
