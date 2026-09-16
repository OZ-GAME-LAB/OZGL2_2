using System;
using System.Linq;
using Game.UI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Game.UI.Editor.MvpHudBuilder;

namespace Game.UI.Editor
{
    public static class MvpRuntimeUnitCountBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/MvpRuntimeUnitCountTest.unity";

        [MenuItem("Game/UI/Open Runtime Unit Count Scene")]
        public static void Open()
        {
            EnsureEditMode();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scenes first.");
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Game/UI/Create Missing Runtime Unit Count Scene")]
        public static void Build()
        {
            EnsureEditMode();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null) throw new InvalidOperationException("Unexpected asset at " + ScenePath);
            MvpRuntimeUnitSelectionBuilder.Build();
            EnsureFont();
            // 새 테스트 씬만 복사/변경한다. 원본 선택 씬과 공용 프리팹은 저장하지 않는다.
            if (!AssetDatabase.CopyAsset(MvpRuntimeUnitSelectionBuilder.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy selection sample.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var sample = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MvpRuntimeUnitSelectionSample>(true)).Single();
                var canvas = CreateCanvas("Registered Unit Counts", 2);
                canvas.SetActive(false);
                var card = Box(canvas.transform, "Unit Counts", new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(40, -190), new Vector2(1190, -40), new Color32(25, 36, 48, 255));
                var allies = Label(card, "Ally Count", "아군 생존 --", 32, new Color32(117, 224, 190, 255), 28, 78, 430, 64);
                var enemies = Label(card, "Enemy Count", "적군 생존 --", 32, new Color32(245, 143, 133, 255), 530, 78, 430, 64);
                Label(card, "Scope", "등록된 유닛만 표시 · 0명이어도 승패를 판정하지 않습니다", 22, Color.white, 28, 18, 1094, 44);
                var hud = canvas.AddComponent<RuntimeUnitCountHud>();
                Assign(hud, "_allyText", allies, "_enemyText", enemies);
                Assign(sample, "_countHud", hud);
                canvas.SetActive(true);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[UI/MvpRuntimeUnitCountBuilder] Created " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        }
    }
}
