using System;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static class MvpRuntimeHudBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/MvpRuntimeHudTest.unity";

        [MenuItem("Game/UI/Create Missing Runtime HUD Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Single-scene creation can unload ScriptableObjects loaded before it.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MvpHudBuilder.PrefabPath);
                var catalog = AssetDatabase.LoadAssetAtPath<CurrencyCatalog>(
                    "Assets/Data/Economy/S.O/CurrencyCatalog.asset");
                if (prefab == null || catalog == null ||
                    !catalog.TryGetByType(CurrencyType.Gold, out var gold))
                    throw new InvalidOperationException("Common HUD prefab and registered Gold catalog are required.");
                MvpHudBuilder.EnsureFont();
                var camera = new GameObject("Runtime HUD Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 22, 31, 255);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var ui = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<GameUIController>();
                var coreBinding = ui.gameObject.AddComponent<CoreHudBinding>();
                var goldBinding = ui.gameObject.AddComponent<RunGoldHudBinding>();
                var systems = new GameObject("Runtime Systems");
                var currency = systems.AddComponent<RunCurrencyManager>();
                var flow = systems.AddComponent<GameFlowController>();
                var waves = systems.AddComponent<WaveController>();
                var rewardGate = systems.AddComponent<TestWaitingScript>();
                MvpHudBuilder.Assign(currency, "_currencyCatalog", catalog);
                var currencyFields = new SerializedObject(currency);
                var starting = currencyFields.FindProperty("_baseStartingCurrencies");
                starting.arraySize = 1;
                starting.GetArrayElementAtIndex(0).FindPropertyRelative("_currency").objectReferenceValue = gold;
                starting.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = 100;
                currencyFields.ApplyModifiedPropertiesWithoutUndo();
                MvpHudBuilder.Assign(coreBinding, "_ui", ui, "_flow", flow, "_waves", waves);
                MvpHudBuilder.Assign(goldBinding, "_ui", ui, "_currencyManager", currency);

                var controls = MvpHudBuilder.CreateCanvas("Runtime HUD Test Controls", 0);
                controls.SetActive(false);
                var card = MvpHudBuilder.Box(controls.transform, "TestCard", new Vector2(.5f, .5f),
                    new Vector2(.5f, .5f), new Vector2(-630, -215), new Vector2(630, 215),
                    new Color32(25, 36, 48, 250));
                MvpHudBuilder.Label(card, "Title", "재화·코어 HUD 연동 테스트", 38, Color.white, 32, 350, 1196, 60);
                MvpHudBuilder.Label(card, "Description", "실제 매니저 사용 · 전투 판정/보상 수치는 테스트 입력",
                    22, new Color32(145, 169, 182, 255), 32, 306, 1196, 38);
                var status = MvpHudBuilder.Label(card, "Status", "초기화 대기", 22, Color.white, 32, 251, 1196, 45);
                MvpHudBuilder.Label(card, "Help",
                    "웨이브 시작 → 3초 준비 → 적 전멸 → 보상 처리 / 3웨이브 반복 후 결과 단계",
                    22, Color.white, 32, 205, 1196, 38);
                var add = MakeButton(card, "AddGold", "골드 +50", 32, 122);
                var spend = MakeButton(card, "SpendGold", "골드 -30", 340, 122);
                var reject = MakeButton(card, "RejectSpend", "부족한 비용 요청", 648, 122);
                var win = MakeButton(card, "Win", "테스트 적 전멸", 956, 122);
                var reward = MakeButton(card, "Reward", "테스트 보상 +30", 32, 36);
                var lose = MakeButton(card, "Lose", "테스트 아군 전멸", 340, 36);
                var reset = MakeButton(card, "Reset", "재화·코어 리셋", 648, 36);
                var toggle = MakeButton(card, "ToggleHud", "HUD 표시/숨김", 956, 36);
                var sample = controls.AddComponent<MvpRuntimeHudSample>();
                MvpHudBuilder.Assign(sample, "_ui", ui, "_currencyManager", currency, "_gold", gold,
                    "_flow", flow, "_waves", waves, "_rewardGate", rewardGate,
                    "_coreBinding", coreBinding, "_goldBinding", goldBinding, "_statusText", status,
                    "_addGoldButton", add, "_spendGoldButton", spend, "_rejectSpendButton", reject,
                    "_winButton", win, "_rewardButton", reward, "_loseButton", lose,
                    "_resetButton", reset, "_toggleHudButton", toggle);
                controls.SetActive(true);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[UI/MvpRuntimeHudBuilder] Created " + ScenePath);
            }
            finally
            {
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static Button MakeButton(Transform parent, string name, string label, float x, float y)
        {
            var button = MvpHudBuilder.Button(parent, name, label, 274, 60);
            MvpHudBuilder.Place((RectTransform)button.transform, x, y, 274, 60);
            return button;
        }
    }
}
