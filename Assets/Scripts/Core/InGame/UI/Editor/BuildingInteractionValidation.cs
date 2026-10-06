#if UNITY_EDITOR
using System;
using System.Reflection;
using OZGL.KDH;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.InGame.Editor
{
    /// <summary>게임을 실행하지 않고 비용 거부 규칙과 선택 참조 계약을 확인한다.</summary>
    public static class BuildingInteractionValidation
    {
        [MenuItem("Tools/UI/Validate Building Interaction Contract")]
        public static void Run() => Debug.Log(Validate());

        public static string Validate()
        {
            int checks = 0;
            MethodInfo readCosts = typeof(BuildingBuildController).GetMethod("TryReadInteractionCosts",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(readCosts != null, "Domain cost parser exists", ref checks);
            CheckCosts(readCosts, null, true, 0, 0, ref checks);
            CheckCosts(readCosts, new[] { Cost(BuildingResourceType.Gold, 7), Cost(BuildingResourceType.Gem, 3) }, true, 7, 3, ref checks);
            CheckCosts(readCosts, new[] { Cost(BuildingResourceType.Gold, 7), Cost(BuildingResourceType.Gold, 7) }, false, 0, 0, ref checks);
            CheckCosts(readCosts, new[] { Cost(BuildingResourceType.Gem, 0), Cost(BuildingResourceType.Gem, 0) }, false, 0, 0, ref checks);
            CheckCosts(readCosts, new[] { Cost(BuildingResourceType.Gold, -1) }, false, 0, 0, ref checks);
            CheckCosts(readCosts, new[] { Cost((BuildingResourceType)999, 1) }, false, 0, 0, ref checks);
            CheckCosts(readCosts, new[] { Cost(BuildingResourceType.Gold, int.MaxValue) }, true, int.MaxValue, 0, ref checks);

            Scene scene = EditorSceneManager.NewPreviewScene();
            BuildingData first = ScriptableObject.CreateInstance<BuildingData>();
            BuildingData second = ScriptableObject.CreateInstance<BuildingData>();
            try
            {
                var root = new GameObject("Building interaction contract fixture");
                SceneManager.MoveGameObjectToScene(root, scene);
                BuildingBuildController controller = root.AddComponent<BuildingBuildController>();
                // RequireComponent(Collider2D)는 추상 타입이므로 fixture는 구체 Collider를 명시한다.
                var slotObject = new GameObject("Slot", typeof(BoxCollider2D));
                slotObject.transform.SetParent(root.transform);
                BuildingSlot slot = slotObject.AddComponent<BuildingSlot>();
                BuildingInteractionTarget empty = controller.QueryInteraction(slot).Target;
                Require(controller.IsInteractionTargetCurrent(empty), "Fresh empty target matches", ref checks);

                var buildingObject = new GameObject("Occupied");
                buildingObject.transform.SetParent(root.transform);
                Building building = buildingObject.AddComponent<Building>();
                typeof(Building).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(building, first);
                Require(slot.TryOccupy(building), "Fixture occupies the slot", ref checks);
                Require(!controller.IsInteractionTargetCurrent(empty), "Old empty target rejects a new occupant", ref checks);
                BuildingInteractionResult stale = controller.TryExecuteInteraction(empty, BuildingInteractionAction.Build, first);
                Require(stale.Failure == BuildingInteractionFailure.TargetChanged && !stale.MayHaveChangedState &&
                    ReferenceEquals(slot.CurrentBuilding, building), "Stale Build cannot enter the legacy replace path", ref checks);

                BuildingInteractionTarget occupied = controller.QueryInteraction(slot).Target;
                Require(controller.IsInteractionTargetCurrent(occupied), "Fresh occupied target matches", ref checks);
                typeof(Building).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(building, second);
                Require(!controller.IsInteractionTargetCurrent(occupied), "Same instance with changed data invalidates target", ref checks);
                slot.GetComponent<Collider2D>().enabled = false;
                Require(!BuildingBuildController.IsLiveInteractionSlot(slot), "Disabled Collider invalidates interaction", ref checks);

            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
            return "Building interaction contract: " + checks + " checks passed.";
        }

        private static BuildingResourceCost Cost(BuildingResourceType type, int amount) =>
            new BuildingResourceCost { type = type, amount = amount };

        private static void CheckCosts(MethodInfo method, BuildingResourceCost[] costs, bool expected,
            int gold, int gems, ref int checks)
        {
            object[] args = { costs, default(BuildingInteractionCost) };
            bool accepted = (bool)method.Invoke(null, args);
            var total = (BuildingInteractionCost)args[1];
            Require(accepted == expected && total.Gold == gold && total.Gems == gems,
                "Cost validation preserves rejection and quote amounts", ref checks);
        }

        private static void Require(bool condition, string label, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("[BuildingInteractionValidation] " + label);
            checks++;
        }
    }
}
#endif
