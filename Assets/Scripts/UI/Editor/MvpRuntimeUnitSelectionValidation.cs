using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.UI.Samples;
using TMPro;
using Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>실제 Physics2DRaycaster/GraphicRaycaster와 포인터 이벤트 전달 경로를 Play Mode에서 검사한다.</summary>
    public static class MvpRuntimeUnitSelectionValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            float previousTimeScale = Time.timeScale;
            try
            {
                await UniTask.NextFrame();
                var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeUnitSelectionSample>();
                var binding = UnityEngine.Object.FindFirstObjectByType<RuntimeUnitInfoBinding>();
                var panel = UnityEngine.Object.FindFirstObjectByType<UnitInfoPanel>();
                var events = EventSystem.current;
                var camera = Camera.main;
                Check(sample != null && sample.IsReady, "sample initialization completed");
                Check(events != null && camera != null && camera.GetComponent<Physics2DRaycaster>() != null, "real event system and 2D raycaster configured");
                var sampleFields = new SerializedObject(sample);
                var sources = sampleFields.FindProperty("_sources");
                var ally = (RuntimeUnitInfoSource)sources.GetArrayElementAtIndex(0).objectReferenceValue;
                var enemy = (RuntimeUnitInfoSource)sources.GetArrayElementAtIndex(1).objectReferenceValue;
                var allyCore = ally.GetComponent<Unit_Core>();
                var allyLife = ally.GetComponent<Unit_Life>();
                var enemyCore = enemy.GetComponent<Unit_Core>();
                var allyTarget = ally.GetComponent<RuntimeUnitSelectionTarget>();
                var enemyTarget = enemy.GetComponent<RuntimeUnitSelectionTarget>();
                var panelFields = new SerializedObject(panel);
                var health = Field<TMP_Text>(panelFields, "_healthText");
                var name = Field<TMP_Text>(panelFields, "_nameText");
                var close = Field<Button>(panelFields, "_closeButton");
                var damage = Field<Button>(sampleFields, "_damageButton");
                var heal = Field<Button>(sampleFields, "_healButton");
                var shield = Field<Button>(sampleFields, "_shieldButton");
                var respawn = Field<Button>(sampleFields, "_respawnButton");
                Canvas.ForceUpdateCanvases();
                Physics2D.SyncTransforms();
                Check(!panel.HasSelection, "starts with no selected unit");
                var allyPoint = (Vector2)camera.WorldToScreenPoint(ally.transform.position);
                var enemyPoint = (Vector2)camera.WorldToScreenPoint(enemy.transform.position);
                var emptyPoint = (Vector2)camera.WorldToScreenPoint(new Vector3(-6, 3, 0));
                Check(Hit(events, allyPoint).gameObject == ally.gameObject, "ally collider is frontmost world hit");
                Check(Hit(events, enemyPoint).gameObject == enemy.gameObject, "enemy collider is frontmost world hit");
                Click(events, allyPoint);
                Check(binding.SelectionId == ally.SelectionId && name.text == "검증용 전사" && health.text == "체력 100 / 100", "left click selects actual ally");
                Click(events, enemyPoint, PointerEventData.InputButton.Right);
                Check(binding.SelectionId == ally.SelectionId, "right click does not change selection");
                Click(events, enemyPoint, dragging: true);
                Check(binding.SelectionId == ally.SelectionId, "drag is not a selection click");
                Check(ExecuteEvents.GetEventHandler<IDragHandler>(enemy.gameObject) == enemyTarget.gameObject, "event system can track drag threshold");
                var dragPress = Press(events, enemyPoint);
                ExecuteEvents.Execute(enemyTarget.gameObject, dragPress, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(enemyTarget.gameObject, dragPress, ExecuteEvents.pointerClickHandler);
                Check(binding.SelectionId == ally.SelectionId, "actual drag callback cancels pending click");
                var fastDrag = Press(events, enemyPoint);
                fastDrag.position += Vector2.right * (events.pixelDragThreshold + 1);
                ExecuteEvents.Execute(enemyTarget.gameObject, fastDrag, ExecuteEvents.pointerClickHandler);
                Check(binding.SelectionId == ally.SelectionId, "release-frame movement rejects click even before drag callback");
                var smallMovement = Press(events, enemyPoint);
                smallMovement.position += Vector2.right * (events.pixelDragThreshold * .5f);
                ExecuteEvents.Execute(enemyTarget.gameObject, smallMovement, ExecuteEvents.pointerClickHandler);
                Check(binding.SelectionId == enemy.SelectionId, "movement below drag threshold remains a click");
                Click(events, allyPoint);
                Click(events, enemyPoint);
                Check(binding.SelectionId == enemy.SelectionId && health.text == "체력 150 / 150", "left click switches to actual enemy");
                Click(events, emptyPoint, PointerEventData.InputButton.Right);
                Check(binding.SelectionId == enemy.SelectionId, "right click on empty space does not clear");
                Click(events, emptyPoint);
                Check(!panel.HasSelection && binding.SelectionId == null, "left click on explicit empty surface clears");
                Click(events, Point(damage));
                Check(allyLife.CurrentHp == 100 && enemy.GetComponent<Unit_Life>().CurrentHp == 150, "test control without selection does not damage any unit");
                Click(events, allyPoint);
                Click(events, Point(damage));
                Check(binding.SelectionId == ally.SelectionId && allyLife.CurrentHp == 75 && health.text == "체력 75 / 100", "UI damage button preserves selection and updates actual life");
                Click(events, Point(heal));
                Check(health.text == "체력 95 / 100", "UI heal button is routed through normal pointer handler");

                Vector3 enemyPosition = enemy.transform.position;
                try
                {
                    // UI 버튼 바로 뒤에도 2D 유닛이 있어야 클릭 관통 여부를 실제로 검증할 수 있다.
                    enemy.transform.position = camera.ScreenToWorldPoint(new Vector3(Point(shield).x, Point(shield).y, 10));
                    Physics2D.SyncTransforms();
                    Check(Hit(events, Point(shield)).module is GraphicRaycaster, "overlay UI takes precedence over a unit behind it");
                    Click(events, Point(shield));
                    Check(binding.SelectionId == ally.SelectionId && allyLife.CurrentShield == 20, "UI does not select the enemy underneath");
                    var card = (RectTransform)panel.transform.Find("UnitCard");
                    Vector2 cardPoint = RectTransformUtility.WorldToScreenPoint(null, card.TransformPoint(card.rect.center));
                    enemy.transform.position = camera.ScreenToWorldPoint(new Vector3(cardPoint.x, cardPoint.y, 10));
                    Physics2D.SyncTransforms();
                    Check(Hit(events, cardPoint).module is GraphicRaycaster, "info card blocks world raycasts");
                    Click(events, cardPoint);
                    Check(binding.SelectionId == ally.SelectionId, "non-button UI card does not clear or change selection");
                }
                finally { enemy.transform.position = enemyPosition; Physics2D.SyncTransforms(); }

                // 활성화 직후에는 ForceUpdateCanvases 이후에도 Graphic.depth가 -1일 수 있다.
                // 실제 사용자가 다음 입력을 할 때처럼 한 번 렌더링된 뒤 닫기 버튼을 검사한다.
                await UniTask.NextFrame();
                var closeHit = Hit(events, Point(close));
                var closeHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(closeHit.gameObject);
                var closeGraphic = close.targetGraphic;
                Check(closeHandler == close.gameObject, "close hit expected " + close.name + "; hit=" + closeHit.gameObject.name +
                    "; handler=" + (closeHandler != null ? closeHandler.name : "none") + "; point=" + Point(close) +
                    "; active=" + closeGraphic.isActiveAndEnabled + "; raycast=" + closeGraphic.raycastTarget +
                    "; depth=" + closeGraphic.depth + "; cull=" + closeGraphic.canvasRenderer.cull +
                    "; filter=" + closeGraphic.Raycast(Point(close), null));
                Click(events, Point(close));
                Check(!panel.HasSelection, "normal close button clears selected binding");
                allyCore.TakeDamage(new DamageResult(null, null, 30, DamageSourceType.BasicAttack));
                Check(!panel.HasSelection, "health event after close does not reopen view");
                Click(events, allyPoint);
                Check(health.text == "체력 85 / 100", "reselect reads shield absorption and current health");
                var held = Press(events, enemyPoint);
                enemy.enabled = false;
                enemy.enabled = true;
                ExecuteEvents.Execute(enemyTarget.gameObject, held, ExecuteEvents.pointerClickHandler);
                Check(binding.SelectionId == ally.SelectionId, "source lifetime change between down/up rejects stale click");
                held = Press(events, enemyPoint);
                enemyTarget.enabled = false;
                enemyTarget.enabled = true;
                ExecuteEvents.Execute(enemyTarget.gameObject, held, ExecuteEvents.pointerClickHandler);
                Check(binding.SelectionId == ally.SelectionId, "disabled target cancels held click");
                enemyTarget.OnPointerDown(null);
                enemyTarget.OnPointerClick(null);
                Check(binding.SelectionId == ally.SelectionId, "null event leaves selection unchanged");
                var mismatched = Press(events, enemyPoint);
                mismatched.pointerId = 42;
                enemyTarget.OnPointerClick(mismatched);
                Check(binding.SelectionId == ally.SelectionId, "other pointer cannot complete this press");
                enemy.enabled = false;
                Click(events, enemyPoint);
                Check(binding.SelectionId == ally.SelectionId, "unavailable source does not clear valid selection");
                enemy.enabled = true;
                panel.gameObject.SetActive(false);
                Click(events, enemyPoint);
                panel.gameObject.SetActive(true);
                Check(binding.SelectionId == ally.SelectionId, "hidden info binding rejects world selection");

                Time.timeScale = 0;
                Click(events, enemyPoint);
                Check(binding.SelectionId == enemy.SelectionId, "event-driven UI selection works when simulation is paused");
                Time.timeScale = previousTimeScale;
                var sprite = enemy.GetComponent<SpriteRenderer>();
                int previousOrder = sprite.sortingOrder;
                try
                {
                    enemy.transform.position = ally.transform.position;
                    sprite.sortingOrder = ally.GetComponent<SpriteRenderer>().sortingOrder + 1;
                    Physics2D.SyncTransforms();
                    Check(Hit(events, allyPoint).gameObject == enemy.gameObject, "overlapping units respect SpriteRenderer sorting order");
                    Click(events, allyPoint);
                    Check(binding.SelectionId == enemy.SelectionId, "only frontmost overlapped unit is selected");
                }
                finally { enemy.transform.position = enemyPosition; sprite.sortingOrder = previousOrder; Physics2D.SyncTransforms(); }

                string oldId = enemy.SelectionId;
                Click(events, Point(respawn));
                Check(!panel.HasSelection && enemy.SelectionId != oldId, "respawn button invalidates previous lifetime");
                Check(allyLife.CurrentHp == 100 && enemy.GetComponent<Unit_Life>().CurrentHp == 150, "sample reset calls real initialization");
                for (int i = 0; i < 3; i++)
                {
                    Click(events, allyPoint);
                    Click(events, enemyPoint);
                    Click(events, emptyPoint);
                }
                Check(!panel.HasSelection, "repeated selection/switch/clear stays consistent");
                Click(events, allyPoint);
                allyCore.TakeDamage(new DamageResult(null, null, 999, DamageSourceType.BasicAttack));
                Check(health.text == "체력 0 / 100" && panel.HasSelection, "dead selected unit remains inspectable");
                ally.gameObject.SetActive(false);
                Check(!panel.HasSelection, "despawn clears selection");
                Click(events, allyPoint);
                Check(!panel.HasSelection, "despawned collider cannot be selected");
                Click(events, Point(respawn));
                Click(events, allyPoint);
                Check(health.text == "체력 100 / 100", "reused unit can be selected again");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(MvpRuntimeUnitSelectionBuilder.ScenePath) != null, "dedicated sample scene saved");
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "no missing script: " + transform.name);
                Canvas.ForceUpdateCanvases();
                foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                {
                    label.ForceMeshUpdate();
                    Check(label.font.HasCharacters(label.text, out uint[] missing, false, true), "glyphs: " + label.name);
                    Check(!label.isTextOverflowing, "text fits: " + label.name);
                }
                Debug.Log($"[UI/MvpRuntimeUnitSelectionValidation] PASS: {_checks} Play Mode checks; actual 2D/UI raycasts and dispatched pointer handlers, not hardware input simulation.");
            }
            finally { Time.timeScale = previousTimeScale; }
        }

        private static RaycastResult Hit(EventSystem events, Vector2 point)
        {
            // 같은 프레임에 ShowUnitInfo로 활성화한 Graphic도 depth/레이아웃을 반영한 뒤 검사한다.
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = point }, hits);
            if (hits.Count == 0) throw new InvalidOperationException("No raycast hit at " + point);
            return hits[0];
        }

        private static PointerEventData Press(EventSystem events, Vector2 point, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
        {
            var hit = Hit(events, point);
            var data = new PointerEventData(events)
            {
                position = point, pressPosition = point, button = button, pointerId = -1,
                pointerCurrentRaycast = hit, pointerPressRaycast = hit, eligibleForClick = true
            };
            data.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
            return data;
        }

        private static void Click(EventSystem events, Vector2 point, PointerEventData.InputButton button = PointerEventData.InputButton.Left, bool dragging = false)
        {
            var data = Press(events, point, button);
            data.dragging = dragging;
            if (data.pointerPress == null) return;
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
        }

        private static Vector2 Point(Button button)
        {
            var rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Runtime unit selection: " + description);
        }
    }
}
