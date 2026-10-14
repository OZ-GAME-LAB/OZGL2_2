using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using OZGL.KDH;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    /// <summary>현재 씬의 플레이어 UI 연결만 검사한다. 자동 연결, 저장, 게임 상태 변경은 하지 않는다.</summary>
    public static class PlayerTeamUiConnectionCheck
    {
        public sealed class Issue
        {
            public string Code { get; }
            public string Message { get; }
            public Object Context { get; }
            public Issue(string code, string message, Object context)
            { Code = code; Message = message; Context = context; }
        }

        private static readonly Dictionary<Type, string[]> Required = new Dictionary<Type, string[]>
        {
            { typeof(TeamBuildingUiStartup), new[] { "_ui", "_wallet", "_waves", "_flow", "_effects", "_coreProgress", "_gold", "_core", "_gameLoop", "_runDecision", "_contentGate", "_gemText", "_hint" } },
            { typeof(CoreGameLoopUiBinding), new[] { "_ui", "_flow", "_waves", "_wallet", "_contentGate" } },
            { typeof(RuntimeBuildingUiBinding), new[] { "_catalog", "_info", "_actions", "_controller", "_database", "_wallet", "_flow", "_feedbackText" } },
            { typeof(RunGoldHudBinding), new[] { "_ui", "_currencyManager" } },
            { typeof(CoreHudBinding), new[] { "_ui", "_flow", "_waves", "_quarterText" } },
            { typeof(CoreRunDecisionBinding), new[] { "_flow", "_waves", "_panelRoot", "_descriptionText", "_finishButton", "_continueButton" } },
            { typeof(ArtifactRewardBinding), new[] { "_panel" } },
            { typeof(PlayerUiNavigation), new[] { "_flow", "_catalog" } },
            { typeof(GameUIController), new[] { "_goldText", "_waveText", "_phaseText", "_waveStartButton", "_waveRewardPanel", "_waveRewardText", "_continueButton", "_runResultPanel", "_runResultTitleText", "_runRewardText", "_restartButton", "_messagePanel", "_messageText" } }
        };

        [MenuItem("Game/UI/Check Team UI Connections")]
        public static void CheckActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var issues = Inspect(scene);
            if (issues.Count == 0)
                Debug.Log("[UI/연결 점검] 필수 연결 정상. 실제 전투 동작까지 보장하는 검사는 아닙니다.");
            else
                foreach (var issue in issues)
                    Debug.LogWarning("[UI/연결 점검/" + issue.Code + "] " + issue.Message, issue.Context);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("플레이어 UI 연결 점검",
                    issues.Count == 0 ? "필수 연결 정상입니다. Play Mode에서 버튼과 팝업 동작도 확인하세요."
                        : $"확인이 필요한 항목 {issues.Count}개입니다.\nConsole 메시지를 클릭하면 해당 오브젝트를 찾을 수 있습니다.\n씬과 코드는 변경하지 않았습니다.",
                    "확인");
        }

        public static List<Issue> Inspect(Scene scene)
        {
            var issues = new List<Issue>();
            if (!scene.IsValid() || !scene.isLoaded)
            { issues.Add(new Issue("SCENE", "검사할 씬을 먼저 열어주세요.", null)); return issues; }
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            var owners = components.OfType<TeamBuildingUiStartup>().ToArray();
            if (owners.Length != 1)
            {
                issues.Add(new Issue("OWNER_COUNT", $"통합 UI가 {owners.Length}개입니다. 기존 UI와 프리팹이 중복되지 않도록 하나만 사용하세요.", owners.FirstOrDefault()));
                return issues;
            }
            var owner = owners[0];
            if (!owner.isActiveAndEnabled)
                issues.Add(new Issue("INACTIVE", "통합 UI가 비활성 상태입니다. 매니저 연결 후 활성화하세요.", owner));
            foreach (var entry in Required)
            {
                var matches = owner.GetComponentsInChildren(entry.Key, true);
                if (matches.Length != 1)
                { issues.Add(new Issue("COMPONENT_COUNT", entry.Key.Name + $" 컴포넌트가 {matches.Length}개입니다. 1개가 필요합니다.", owner)); continue; }
                var component = matches[0];
                foreach (var field in entry.Value) Require(component, field, scene, issues);
                if (component is Behaviour behaviour && !behaviour.enabled)
                    issues.Add(new Issue("DISABLED", component.GetType().Name + " 컴포넌트가 꺼져 있습니다.", component));
                Match(component, "_flow", Reference(owner, "_flow"), issues);
                Match(component, "_waves", Reference(owner, "_waves"), issues);
                Match(component, "_wallet", Reference(owner, "_wallet"), issues);
                Match(component, "_currencyManager", Reference(owner, "_wallet"), issues);
                Match(component, "_contentGate", Reference(owner, "_contentGate"), issues);
                Match(component, "_ui", Reference(owner, "_ui"), issues);
            }
            if (components.OfType<GameUIController>().Count(h => h.isActiveAndEnabled) > 1)
                issues.Add(new Issue("HUD_COUNT", "활성 HUD가 여러 개입니다. UI 중복 입력 여부를 확인하세요.", owner));
            if (components.OfType<EventSystem>().Count(e => e.isActiveAndEnabled) != 1)
                issues.Add(new Issue("EVENT_SYSTEM", "현재 씬에는 활성 EventSystem이 1개 필요합니다.", owner));
            if (components.OfType<ArtifactManager>().Count(a => a.isActiveAndEnabled) != 1)
                issues.Add(new Issue("ARTIFACT_MANAGER", "활성 ArtifactManager가 1개 필요합니다. 유물 선택 대상을 확인하세요.", owner));

            foreach (var transform in owner.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    issues.Add(new Issue("MISSING_SCRIPT", "스크립트가 누락되었습니다: " + transform.name, transform));
            foreach (var component in owner.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null))
            {
                var field = new SerializedObject(component).GetIterator();
                while (field.Next(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference &&
                        field.objectReferenceValue == null && field.objectReferenceInstanceIDValue != 0)
                        issues.Add(new Issue("BROKEN_REFERENCE", component.GetType().Name + "." + field.propertyPath + " 참조가 끊어졌습니다.", component));
            }
            var binding = owner.GetComponent<RuntimeBuildingUiBinding>();
            if (binding != null) CheckSlots(binding, scene, issues);
            return issues;
        }

        private static void CheckSlots(RuntimeBuildingUiBinding binding, Scene scene, List<Issue> issues)
        {
            var slots = new SerializedObject(binding).FindProperty("_slots");
            if (slots == null || slots.arraySize == 0)
            { issues.Add(new Issue("SLOTS", "건설 공간 목록이 비어 있습니다.", binding)); return; }
            var seen = new HashSet<Object>();
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as BuildingSlot;
                if (slot == null || slot.gameObject.scene != scene || !seen.Add(slot))
                { issues.Add(new Issue("SLOT_REFERENCE", $"건설 공간 {i}번이 비어 있거나 중복/다른 씬 참조입니다.", binding)); continue; }
                var target = slot.GetComponent<RuntimeBuildingSelectionTarget>();
                if (target == null || Reference(target, "_binding") != binding || Reference(target, "_slot") != slot ||
                    Reference(target, "_flow") != Reference(binding, "_flow"))
                    issues.Add(new Issue("SLOT_INPUT", slot.name + "의 클릭 대상과 새 UI 연결을 확인하세요.", slot));
            }
            var controller = Reference(binding, "_controller") as Component;
            if (controller != null && Reference(controller, "database") != Reference(binding, "_database"))
                issues.Add(new Issue("DATABASE", "건물 컨트롤러와 UI의 데이터베이스가 다릅니다.", binding));
        }

        private static void Require(Component component, string field, Scene scene, List<Issue> issues)
        {
            var property = new SerializedObject(component).FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            { issues.Add(new Issue("SCHEMA", component.GetType().Name + "." + field + " 검사 계약을 갱신해야 합니다.", component)); return; }
            var value = property.objectReferenceValue;
            if (value == null)
            { issues.Add(new Issue("MISSING", component.GetType().Name + "." + field + "가 비어 있습니다. Inspector에서 연결하세요.", component)); return; }
            var obj = value is Component c ? c.gameObject : value as GameObject;
            if (obj != null && (EditorUtility.IsPersistent(obj) || obj.scene != scene))
                issues.Add(new Issue("FOREIGN_REFERENCE", component.GetType().Name + "." + field + "는 현재 씬의 오브젝트를 연결해야 합니다.", component));
        }

        private static Object Reference(Object component, string field) =>
            new SerializedObject(component).FindProperty(field)?.objectReferenceValue;

        private static void Match(Component component, string field, Object expected, List<Issue> issues)
        {
            var property = new SerializedObject(component).FindProperty(field);
            if (property != null && property.objectReferenceValue != null && expected != null && property.objectReferenceValue != expected)
                issues.Add(new Issue("MISMATCH", component.GetType().Name + "." + field + "가 Startup과 다른 대상을 가리킵니다.", component));
        }

        public static void RunBatchTests()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/UnityUIValidation/"))
                throw new InvalidOperationException("Use an isolated validation project.");
            var scene = EditorSceneManager.OpenScene(TeamBuildingUiSetup.ScenePath);
            var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TeamBuildingUiStartup>(true)).Single();
            int checks = 0;
            void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks++; }
            void Has(string code) => Check(Inspect(scene).Any(i => i.Code == code), "Expected issue: " + code);
            bool wasDirty = scene.isDirty;
            var initial = Inspect(scene);
            Check(initial.Count == 0, string.Join("\n", initial.Select(i => i.Code + ": " + i.Message)));
            Check(scene.isDirty == wasDirty, "Read-only check changed scene dirty state.");
            var snapshot = EditorJsonUtility.ToJson(owner);
            Inspect(scene);
            Check(snapshot == EditorJsonUtility.ToJson(owner), "Inspector check changed serialized data.");
            void Change(Object target, string field, Object value, Action test)
            {
                var serialized = new SerializedObject(target);
                var property = serialized.FindProperty(field);
                var previous = property.objectReferenceValue;
                property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
                try { test(); }
                finally { serialized.Update(); serialized.FindProperty(field).objectReferenceValue = previous; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            }
            Change(owner, "_wallet", null, () => Has("MISSING"));
            var duplicate = new GameObject("Temporary UI connection test");
            duplicate.SetActive(false);
            try
            {
                duplicate.AddComponent<TeamBuildingUiStartup>(); Has("OWNER_COUNT");
                Object.DestroyImmediate(duplicate.GetComponent<TeamBuildingUiStartup>());
                var alternateWallet = duplicate.AddComponent<RunCurrencyManager>();
                Change(owner.GetComponent<CoreGameLoopUiBinding>(), "_wallet", alternateWallet, () => Has("MISMATCH"));
            }
            finally { Object.DestroyImmediate(duplicate); }
            var building = owner.GetComponent<RuntimeBuildingUiBinding>();
            var slot = Reference(building, "_slots.Array.data[0]") as BuildingSlot;
            Change(slot.GetComponent<RuntimeBuildingSelectionTarget>(), "_binding", null, () => Has("SLOT_INPUT"));
            var input = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Single();
            input.enabled = false;
            try { Has("EVENT_SYSTEM"); } finally { input.enabled = true; }
            owner.gameObject.SetActive(false);
            try { Has("INACTIVE"); } finally { owner.gameObject.SetActive(true); }
            var foreignScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var foreign = new GameObject("Temporary foreign wallet").AddComponent<RunCurrencyManager>();
                SceneManager.MoveGameObjectToScene(foreign.gameObject, foreignScene);
                Change(owner, "_wallet", foreign, () => Has("FOREIGN_REFERENCE"));
            }
            finally { EditorSceneManager.CloseScene(foreignScene, true); }
            SceneManager.SetActiveScene(scene);
            Check(Inspect(scene).Count == 0, "Fixture did not restore.");
            Directory.CreateDirectory("Logs/PlayerTeamUiPrefab");
            File.WriteAllText("Logs/PlayerTeamUiPrefab/connections.txt", $"PASS: {checks} connection checks. No scene saved.\n");
            Debug.Log($"[UI/ConnectionCheck] {checks} checks passed.");
        }
    }
}
