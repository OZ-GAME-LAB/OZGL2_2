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
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.SetActive(false);
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
                Check(manager.ShowHud().IsValid && GetScreen(manager, UIId.Hud).IsVisible,
                    "activating the root and calling ShowHud displays the existing HUD", checks);

                UIRegistration detailRegistration = Registration(manager, UIId.Detail);
                Check(detailRegistration.Instance == null && detailRegistration.Prefab != null,
                    "the example detail popup starts as a prefab-only registration", checks);
                UIScreen detail = GetScreen(manager, UIId.Detail);
                var detailView = detail.GetComponent<DetailPopupView>();
                Check(detailView != null && !detail.IsVisible && Field<bool>(detailRegistration, "Created"),
                    "first detail lookup creates a hidden screen with DetailPopupView", checks);
                Check(ReferenceEquals(GetScreen(manager, UIId.Detail), detail),
                    "repeated detail lookup reuses the same instance", checks);

                UIScreen catalog = GetScreen(manager, UIId.BuildingCatalog);
                UIHandle catalogHandle = manager.Open(UIId.BuildingCatalog);
                Check(catalogHandle.IsValid && catalog.IsVisible,
                    "catalog can act as a parent for a detail popup", checks);
                Canvas parentCanvas = catalog.GetComponent<Canvas>();
                Canvas detailCanvas = detail.GetComponent<Canvas>();
                if (parentCanvas == null || detailCanvas == null)
                    throw new InvalidOperationException("Expected independent catalog and detail canvases.");
                int detailOrder = detailCanvas.sortingOrder;
                // 부모가 기본 상세 창보다 높은 경우에도 Push가 상세 창을 위로 올려야 한다.
                parentCanvas.sortingOrder = detailOrder + 25;
                detailView.SetContent("정렬 테스트", "상세", "부모 위에 표시");
                UIHandle firstDetailHandle = manager.Push(UIId.Detail);
                Check(firstDetailHandle.IsValid && detail.IsVisible && catalog.IsVisible &&
                    detailCanvas.sortingOrder > parentCanvas.sortingOrder,
                    "Push keeps its parent visible and raises detail canvas above a higher-order parent", checks);
                Check(!InputEnabled(catalog) && InputEnabled(detail),
                    "only the top popup accepts input", checks);
                Check(manager.CloseTop() && manager.TopHandle.Equals(catalogHandle) &&
                    catalog.IsVisible && !detail.IsVisible && detailCanvas.sortingOrder == detailOrder,
                    "closing detail returns to its parent and restores the detail canvas order", checks);

                ValidateMandatoryParent(manager, detail, checks);

                catalogHandle = manager.Open(UIId.BuildingCatalog);
                var catalogView = catalog.GetComponent<BuildingCatalogView>();
                slot = FirstCatalogSlot(catalogView);
                slot.gameObject.SetActive(true);
                RebindLifecycle(slot);
                Button slotButton = Field<Button>(slot, "_button");
                close = detail.GetComponentInChildren<CloseUtility>(true);
                if (close == null) throw new InvalidOperationException("Detail popup has no CloseUtility.");
                RebindLifecycle(close);
                Button closeButton = Field<Button>(close, "_button");
                int artifactClicks = 0;
                int skillClicks = 0;

                slot.Bind(null, "유물 테스트", "희귀", false, true, () =>
                {
                    artifactClicks++;
                    detailView.SetContent("유물 테스트", "희귀", "유물 효과 설명");
                    manager.Push(UIId.Detail);
                });
                validationEvents.SetSelectedGameObject(slotButton.gameObject);
                Click(slotButton);
                Check(artifactClicks == 1 && skillClicks == 0 && detail.IsVisible &&
                    Text(detailView, "_title") == "유물 테스트" && Text(detailView, "_description") == "유물 효과 설명",
                    "the saved catalog slot opens detail using artifact example data", checks);
                Click(closeButton);
                Check(!detail.IsVisible && manager.TopHandle.Equals(catalogHandle) &&
                    validationEvents.currentSelectedGameObject == slotButton.gameObject,
                    "the saved CloseUtility closes its owner and returns focus to the clicked slot", checks);

                slot.Bind(null, "스킬 테스트", "액티브", false, true, () =>
                {
                    skillClicks++;
                    detailView.SetContent("스킬 테스트", "액티브", "스킬 효과와 로어");
                    manager.Push(UIId.Detail);
                });
                Click(slotButton);
                UIHandle reboundDetailHandle = detail.Handle;
                Check(artifactClicks == 1 && skillClicks == 1 &&
                    Text(detailView, "_title") == "스킬 테스트" && Text(detailView, "_subtitle") == "액티브" &&
                    Text(detailView, "_description") == "스킬 효과와 로어",
                    "rebinding the same slot replaces its callback and detail content with skill example data", checks);
                // UnityEvent를 직접 호출해도 슬롯은 부모의 입력 차단 상태를 확인한다.
                Lifecycle(slotButton, "OnCanvasGroupChanged");
                slotButton.onClick.Invoke();
                Check(artifactClicks == 1 && skillClicks == 1,
                    "a covered parent slot cannot invoke its callback again", checks);
                Check(!manager.Close(firstDetailHandle) && detail.IsVisible,
                    "a handle from an earlier detail opening cannot close the rebound detail", checks);
                Click(closeButton);
                slot.Unbind();
                Click(slotButton);
                Check(artifactClicks == 1 && skillClicks == 1 && !detail.IsVisible,
                    "Unbind removes the old data callback from the reusable slot", checks);

                Check(manager.InitializeScreens() && Field<bool>(detailRegistration, "Created") &&
                    ReferenceEquals(GetScreen(manager, UIId.Detail), detail),
                    "registry reinitialization accepts and reuses its previously created lazy instance", checks);
                Check(manager.ShowHud().IsValid && GetScreen(manager, UIId.Hud).IsVisible,
                    "HUD is available again after registry reinitialization", checks);
                manager.Open(UIId.BuildingCatalog);
                UIHandle afterReinitialize = manager.Push(UIId.Detail);
                Check(afterReinitialize.IsValid && !afterReinitialize.Equals(reboundDetailHandle) &&
                    !manager.Close(reboundDetailHandle) && detail.IsVisible,
                    "reinitialization keeps handle versions distinct from old detail openings", checks);

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
                if (previousEvents != null) EventSystem.current = previousEvents;
                if (previousEvents != null && previousFocus != null && previousFocus.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousFocus);
                _running = false;
            }
        }

        private static void ValidateMandatoryParent(InGameUIManager manager, UIScreen detail, List<string> checks)
        {
            UIScreen reward = GetScreen(manager, UIId.ArtifactReward);
            UIHandle rewardHandle = manager.Open(UIId.ArtifactReward);
            Check(rewardHandle.IsValid && reward.IsVisible && manager.HasModalOpen,
                "required reward opens as a modal parent", checks);
            UIHandle detailHandle = manager.Push(UIId.Detail);
            Check(detailHandle.IsValid && reward.IsVisible && detail.IsVisible && manager.HasModalOpen &&
                detail.GetComponent<Canvas>().sortingOrder > reward.GetComponent<Canvas>().sortingOrder,
                "detail can be pushed above a required reward while retaining its modal block", checks);
            Check(manager.CloseTop() && manager.TopHandle.Equals(rewardHandle) && reward.IsVisible && InputEnabled(reward),
                "closing the modal's detail restores the required reward and its input", checks);
            Check(!manager.CloseTop() && reward.IsVisible,
                "CloseTop cannot dismiss the required reward itself", checks);
            Check(manager.Close(rewardHandle, UICloseReason.Completed) && !reward.IsVisible,
                "the owning presenter can still close the required reward with Completed", checks);
        }

        private static UIScreen GetScreen(InGameUIManager manager, UIId id)
        {
            if (!manager.TryGetScreen(id, out UIScreen screen) || screen == null)
                throw new InvalidOperationException("Screen is not registered: " + id);
            return screen;
        }

        private static UIRegistration Registration(InGameUIManager manager, UIId id)
        {
            foreach (UIRegistration entry in Field<UIRegistration[]>(manager, "_screens"))
                if (entry.Id == id) return entry;
            throw new InvalidOperationException("Screen registration is missing: " + id);
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

        private static string Text(DetailPopupView view, string field) => Field<TMP_Text>(view, field).text;

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
