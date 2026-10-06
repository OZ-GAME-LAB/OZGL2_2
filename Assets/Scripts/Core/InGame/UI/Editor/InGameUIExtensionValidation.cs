#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI.InGame.Editor
{
    /// <summary>새 프리팹의 공용 슬롯과 상세 팝업을 실제 게임 데이터 없이 검사한다.</summary>
    public static class InGameUIExtensionValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;
        public static string LastResult { get; private set; } = "Not run";

        public static string Run(string prefabPath = InGameUIMigration.RootPrefabPath)
        {
            if (_running) throw new InvalidOperationException("UI extension validation is already running.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this isolated validation in Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new ArgumentException("UI prefab was not found: " + prefabPath);

            _running = true;
            LastResult = "Running";
            Scene preview = EditorSceneManager.NewPreviewScene();
            EventSystem previousEvents = EventSystem.current;
            GameObject previousFocus = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
            EventSystem validationEvents = null;
            InGameUIManager manager = null;
            UIItemSlot slot = null;
            CloseUtility close = null;
            var checks = new List<string>();
            var temporaryFonts = new List<UnityEngine.Object>();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.SetActive(false);
                InGameUIValidation.PrepareFontsForRoots(new[] { instance }, temporaryFonts);
                manager = instance.GetComponent<InGameUIManager>();
                if (manager == null) throw new InvalidOperationException("The prefab has no InGameUIManager.");

                var eventRoot = new GameObject("Extension Validation EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventRoot, preview);
                validationEvents = eventRoot.GetComponent<EventSystem>();
                RebindLifecycle(validationEvents);
                EventSystem.current = validationEvents;

                Check(!instance.activeInHierarchy && manager.InitializeScreens(),
                    "screen registry initializes while the UI root is disabled", checks);
                instance.SetActive(true);
                Check(manager.ShowHud() && GetScreen(manager, UIId.Hud).IsVisible,
                    "activating the root and calling ShowHud displays the existing HUD", checks);

                Check(instance.GetComponentsInChildren<Canvas>(true).Length == 2,
                    "assembled UI has exactly two shared canvases", checks);
                UIScreen detail = GetScreen(manager, UIId.Detail);
                var detailView = detail.GetComponentInChildren<TextPopupView>(true);
                Check(detailView != null && !detail.IsVisible,
                    "the preplaced detail popup stays hidden until opened", checks);
                Check(ReferenceEquals(GetScreen(manager, UIId.Detail), detail),
                    "repeated detail lookup reuses the same instance", checks);

                UIScreen catalog = GetScreen(manager, UIId.BuildingCatalog);
                Check(manager.OpenPopup(UIId.BuildingCatalog) && catalog.IsVisible,
                    "catalog can act as a parent for a detail popup", checks);
                Check(catalog.GetComponent<Canvas>() == null && detail.GetComponent<Canvas>() == null &&
                    catalog.transform.parent == detail.transform.parent &&
                    catalog.GetComponentInParent<Canvas>() == detail.GetComponentInParent<Canvas>(),
                    "catalog and detail share PopupCanvas and have no private Canvas", checks);
                detailView.SetContent("정렬 테스트", "상세", "부모 위에 표시");
                Check(manager.OpenPopup(UIId.Detail) && detail.IsVisible && catalog.IsVisible &&
                    detail.transform.GetSiblingIndex() > catalog.transform.GetSiblingIndex(),
                    "OpenPopup keeps its parent visible and places detail after its parent", checks);
                Check(!InputEnabled(catalog) && InputEnabled(detail),
                    "only the top popup accepts input", checks);
                Check(manager.CloseTopPopup() && manager.TopPopup == catalog && catalog.IsVisible && !detail.IsVisible,
                    "closing detail returns to its parent without Canvas order changes", checks);

                ValidateMandatoryParent(manager, detail, checks);

                manager.OpenPopup(UIId.BuildingCatalog);
                var catalogView = catalog.GetComponent<BuildingCatalogView>();
                slot = FirstCatalogSlot(catalogView);
                slot.gameObject.SetActive(true);
                RebindLifecycle(slot);
                Button slotButton = Field<Button>(slot, "_button");
                close = detailView.GetComponentInChildren<CloseUtility>(true);
                if (close == null) throw new InvalidOperationException("Detail popup has no CloseUtility.");
                RebindLifecycle(close);
                Button closeButton = Field<Button>(close, "_button");
                int artifactClicks = 0;
                int skillClicks = 0;

                slot.Bind(null, "유물 테스트", "희귀", false, true, () =>
                {
                    artifactClicks++;
                    detailView.SetContent("유물 테스트", "희귀", "유물 효과 설명");
                    manager.OpenPopup(UIId.Detail);
                });
                validationEvents.SetSelectedGameObject(slotButton.gameObject);
                Click(slotButton);
                Check(artifactClicks == 1 && skillClicks == 0 && detail.IsVisible &&
                    Text(detailView, "_title") == "유물 테스트" && Text(detailView, "_description") == "유물 효과 설명",
                    "the saved catalog slot opens detail using artifact example data", checks);
                Click(closeButton);
                Check(!detail.IsVisible && manager.TopPopup == catalog &&
                    validationEvents.currentSelectedGameObject == null,
                    "the saved CloseUtility closes its owner without restoring stale focus", checks);

                slot.Bind(null, "스킬 테스트", "액티브", false, true, () =>
                {
                    skillClicks++;
                    detailView.SetContent("스킬 테스트", "액티브", "스킬 효과와 로어");
                    manager.OpenPopup(UIId.Detail);
                });
                Click(slotButton);
                Check(artifactClicks == 1 && skillClicks == 1 &&
                    Text(detailView, "_title") == "스킬 테스트" && Text(detailView, "_subtitle") == "액티브" &&
                    Text(detailView, "_description") == "스킬 효과와 로어",
                    "rebinding the same slot replaces its callback and detail content with skill example data", checks);
                // UnityEvent를 직접 호출해도 슬롯은 부모의 입력 차단 상태를 확인한다.
                Lifecycle(slotButton, "OnCanvasGroupChanged");
                slotButton.onClick.Invoke();
                Check(artifactClicks == 1 && skillClicks == 1,
                    "a covered parent slot cannot invoke its callback again", checks);
                Check(manager.OpenPopup(UIId.Detail) && manager.OpenPopupCount == 2,
                    "opening the rebound detail again keeps one stack entry", checks);
                Click(closeButton);
                slot.Unbind();
                Click(slotButton);
                Check(artifactClicks == 1 && skillClicks == 1 && !detail.IsVisible,
                    "Unbind removes the old data callback from the reusable slot", checks);

                Check(manager.InitializeScreens() &&
                    ReferenceEquals(GetScreen(manager, UIId.Detail), detail),
                    "registry reinitialization reuses the same preplaced detail instance", checks);
                Check(manager.ShowHud() && GetScreen(manager, UIId.Hud).IsVisible,
                    "HUD is available again after registry reinitialization", checks);
                manager.OpenPopup(UIId.BuildingCatalog);
                Check(manager.OpenPopup(UIId.Detail) && detail.IsVisible && manager.OpenPopupCount == 2,
                    "preplaced parent/detail stacking works after registry reinitialization", checks);

                LastResult = "PASS (" + checks.Count + ")\n" +
                    "Edit Mode / isolated Preview Scene / saved replacement prefabs\n" + string.Join("\n", checks);
                SaveReport(LastResult);
                Debug.Log("[InGameUIExtensionValidation] " + LastResult);
                return LastResult;
            }
            catch (Exception exception)
            {
                LastResult = "FAIL after " + checks.Count + " checks: " + exception.Message + "\n" +
                    string.Join("\n", checks);
                SaveReport(LastResult);
                throw;
            }
            finally
            {
                if (slot != null) { slot.Unbind(); Lifecycle(slot, "OnDisable"); }
                if (close != null) Lifecycle(close, "OnDisable");
                if (manager != null) Lifecycle(manager, "OnDisable");
                if (validationEvents != null) Lifecycle(validationEvents, "OnDisable");
                EditorSceneManager.ClosePreviewScene(preview);
                for (int i = temporaryFonts.Count - 1; i >= 0; i--)
                    if (temporaryFonts[i] != null) UnityEngine.Object.DestroyImmediate(temporaryFonts[i]);
                if (previousEvents != null) EventSystem.current = previousEvents;
                if (previousEvents != null && previousFocus != null && previousFocus.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousFocus);
                _running = false;
            }
        }

        private static void ValidateMandatoryParent(InGameUIManager manager, UIScreen detail, List<string> checks)
        {
            UIScreen reward = GetScreen(manager, UIId.ArtifactReward);
            Check(manager.ReplacePopup(UIId.ArtifactReward) && reward.IsVisible && manager.HasBlockingPopup,
                "required reward opens as a modal parent", checks);
            Check(manager.OpenPopup(UIId.Detail) && reward.IsVisible && detail.IsVisible && manager.HasBlockingPopup &&
                detail.transform.GetSiblingIndex() > reward.transform.GetSiblingIndex() && !InputEnabled(GetScreen(manager, UIId.Hud)),
                "detail can be pushed above a required reward while retaining its modal block", checks);
            Check(manager.CloseTopPopup() && manager.TopPopup == reward && reward.IsVisible && InputEnabled(reward),
                "closing the modal's detail restores the required reward and its input", checks);
            Check(!manager.CloseTopPopup() && reward.IsVisible,
                "CloseTop cannot dismiss the required reward itself", checks);
            Check(manager.ClosePopup(UIId.ArtifactReward, UICloseReason.Completed) && !reward.IsVisible,
                "the owning presenter can still close the required reward with Completed", checks);
        }

        private static UIScreen GetScreen(InGameUIManager manager, UIId id)
        {
            if (!manager.TryGetScreen(id, out UIScreen screen) || screen == null)
                throw new InvalidOperationException("Screen is not registered: " + id);
            return screen;
        }

        private static UIItemSlot FirstCatalogSlot(BuildingCatalogView view)
        {
            var data = new SerializedObject(view);
            SerializedProperty cards = data.FindProperty("_cards");
            if (cards == null || cards.arraySize == 0)
                throw new InvalidOperationException("The saved catalog has no cards.");
            var slot = (UIItemSlot)cards.GetArrayElementAtIndex(0).FindPropertyRelative("Slot").objectReferenceValue;
            if (slot == null) throw new InvalidOperationException("The saved catalog card has no UIItemSlot.");
            return slot;
        }

        private static bool InputEnabled(UIScreen screen)
        {
            CanvasGroup group = screen.Root.GetComponent<CanvasGroup>();
            return group != null && group.interactable && group.blocksRaycasts;
        }

        private static void Click(Button button)
        {
            if (button != null) Lifecycle(button, "OnCanvasGroupChanged");
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
                throw new InvalidOperationException("The expected button is not interactable.");
            button.onClick.Invoke();
        }

        private static string Text(TextPopupView view, string field) => Field<TMP_Text>(view, field).text;

        private static T Field<T>(object owner, string field)
        {
            FieldInfo info = owner.GetType().GetField(field, PrivateInstance);
            if (info == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " is missing.");
            return (T)info.GetValue(owner);
        }

        private static void RebindLifecycle(MonoBehaviour component)
        {
            // 일반 MonoBehaviour의 Edit Mode 콜백 연결 여부에 의존하지 않는다.
            Lifecycle(component, "OnDisable");
            Lifecycle(component, "OnEnable");
        }

        private static void Lifecycle(MonoBehaviour component, string method) =>
            component.GetType().GetMethod(method, PrivateInstance)?.Invoke(component, null);

        private static void Check(bool condition, string message, List<string> checks)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add("PASS: " + message);
        }

        private static void SaveReport(string result)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(project, "PersonalDocs", "UIValidation");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "extension-tests.txt"), result + "\n", new UTF8Encoding(false));
        }
    }
}
#endif
