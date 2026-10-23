using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Units;
using Units.Effects;
using Units.Skills;

namespace Game.QA.Editor
{
    public static class QASceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Test/QA.unity";
        private const string PrefabPath = "Assets/Prefabs/QA/QABattleRoot.prefab";

        [MenuItem("Tools/QA/Run Scene Verification")]
        public static void Verify()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the QA scene and enter Play mode first.");
            var controller = UnityEngine.Object.FindFirstObjectByType<QABattleController>();
            if (controller.GetComponents<QAVerificationRunner>().Any(runner => !runner.Finished))
                throw new InvalidOperationException("QA verification is already running.");
            controller.gameObject.AddComponent<QAVerificationRunner>().Run();
        }

        [MenuItem("Tools/QA/Create Battle QA Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building QA assets.");
            if (File.Exists(ScenePath) || File.Exists(PrefabPath)) throw new InvalidOperationException("QA assets already exist; edit them without rebuilding.");
            Directory.CreateDirectory("Assets/Prefabs/QA"); AssetDatabase.Refresh();
            // Preserve any in-memory scene edits by creating QA additively.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("QABattleRoot");
            var systems = new GameObject("CombatSystems"); systems.transform.SetParent(root.transform);
            var runtime = systems.AddComponent<RuntimeUnitManager>();
            var modifiers = systems.AddComponent<UnitStatModifierManager>();
            systems.AddComponent<DamageResolver>(); systems.AddComponent<HealResolver>();
            systems.AddComponent<SkillEffectResolver>(); systems.AddComponent<RuntimeEffectManager>(); systems.AddComponent<ProjectileManager>();
            var session = new GameObject("SessionUnits").transform; session.SetParent(root.transform);
            var cameraObject = new GameObject("BattleCamera"); cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0, 0, -30);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .11f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            cameraObject.AddComponent<AudioListener>();
            // BattleCamera renders to the panel's texture. A separate camera must still
            // target Display 1 so GameView does not show "No cameras rendering" over the UI.
            var displayObject = new GameObject("DisplayCamera"); displayObject.transform.SetParent(root.transform);
            var displayCamera = displayObject.AddComponent<Camera>();
            displayCamera.clearFlags = CameraClearFlags.SolidColor; displayCamera.backgroundColor = new Color(.10f, .12f, .15f);
            displayCamera.cullingMask = 0; displayCamera.targetDisplay = 0; displayCamera.depth = -100;
            displayCamera.allowHDR = false; displayCamera.allowMSAA = false; displayCamera.useOcclusionCulling = false;
            // Layered scene space stays visible inside the panel's camera RenderTexture.
            DrawGrid(root.transform);
            var events = new GameObject("QAEventSystem"); events.transform.SetParent(root.transform);
            events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>();
            var controller = root.AddComponent<QABattleController>();
            var panel = new GameObject("DebugPanel").AddComponent<QABattlePanel>(); panel.transform.SetParent(root.transform);
            var font = CreateFont();
            panel.Construct(controller, font);
            var ally = AssetDatabase.LoadAssetAtPath<AllyUnitSpawnDatabaseSO>("Assets/Data/Units/UnitDatas/UnitDB/AllyDB.asset");
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyUnitSpawnDatabaseSO>("Assets/Data/Units/UnitDatas/UnitDB/EnemyDB.asset");
            var group = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/EmptyGroup.prefab");
            if (ally == null || enemy == null || group == null) throw new InvalidOperationException("Required unit databases/group prefab not found.");
            controller.Configure(runtime, modifiers, ally, enemy, group, session, camera, panel);
            // Prefab-local references are all assigned before saving. Existing assets remain untouched.
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) throw new InvalidOperationException("QA prefab save failed.");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("[QA] Created " + ScenePath + " and " + PrefabPath);
        }

        private static TMP_FontAsset CreateFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansKR/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("Korean font source not found.");
            var font = TMP_FontAsset.CreateFontAsset(source, 40, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "QAKoreanFont";
            AssetDatabase.CreateAsset(font, "Assets/Prefabs/QA/QAKoreanFont.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) if (texture != null) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        private static void DrawGrid(Transform root)
        {
            var material = new Material(Shader.Find("Sprites/Default")) { name = "QAGridMaterial" };
            AssetDatabase.CreateAsset(material, "Assets/Prefabs/QA/QAGridMaterial.mat");
            var grid = new GameObject("BattleGrid").transform; grid.SetParent(root);
            for (int i = -18; i <= 18; i += 2) Line(grid, material, new Vector3(i, -12, 2), new Vector3(i, 12, 2), .018f);
            for (int i = -12; i <= 12; i += 2) Line(grid, material, new Vector3(-18, i, 2), new Vector3(18, i, 2), .018f);
            Line(grid, material, new Vector3(0, -12, 2), new Vector3(0, 12, 2), .045f);
        }
        private static void Line(Transform parent, Material material, Vector3 start, Vector3 end, float width)
        {
            var line = new GameObject("GridLine").AddComponent<LineRenderer>(); line.transform.SetParent(parent);
            line.sharedMaterial = material; line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = new Color(.23f, .27f, .32f, .6f);
            line.positionCount = 2; line.SetPosition(0, start); line.SetPosition(1, end); line.sortingOrder = -100;
        }
    }
}
