#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Cameras;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.UI.InGame.Editor
{
    /// <summary>
    /// 새 UI 프리팹 복사본을 Preview Scene에서 검사한다.
    /// 게임 매니저를 시작하거나 현재 씬, 원본 프리팹, 저장 데이터를 수정하지 않는다.
    /// </summary>
    public static class InGameUIFlowValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;
        public static string LastResult { get; private set; } = "Not run";

        // Unity CLI eval_file 등에서 호출한 뒤 LastResult로 결과를 조회할 수 있다.
        public static void Run(string prefabPath) => RunAsync(prefabPath).Forget(Debug.LogException);

        public static async UniTask<string> RunAsync(string prefabPath)
        {
            if (_running) throw new InvalidOperationException("UI flow validation is already running.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this isolated validation in Edit Mode.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new ArgumentException("UI prefab was not found: " + prefabPath);

            _running = true;
            LastResult = "Running";
            Scene preview = EditorSceneManager.NewPreviewScene();
            EventSystem previousEvents = EventSystem.current;
            GameObject previousFocus = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
            EventSystem validationEvents = null;
            ArtifactRewardPresenter rewardPresenter = null;
            ArtifactRewardView rewardView = null;
            var temporaryAssets = new List<UnityEngine.Object>();
            var temporaryFonts = new List<UnityEngine.Object>();
            var checks = new List<string>();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.name = "InGameUIFlowValidation";
                instance.SetActive(false);
                InGameUIValidation.PrepareFontsForRoots(new[] { instance }, temporaryFonts);
                instance.SetActive(true);

                var eventRoot = new GameObject("Validation EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventRoot, preview);
                validationEvents = eventRoot.GetComponent<EventSystem>();
                RebindLifecycle(validationEvents);
                EventSystem.current = validationEvents;

                var manager = Require<InGameUIManager>(instance);
                Check(manager.InitializeScreens(), "screen registry initializes without game systems", checks);
                Check(manager.ShowHud(), "HUD opens without starting game flow", checks);
                rewardView = Require<ArtifactRewardView>(instance);
                rewardPresenter = Require<ArtifactRewardPresenter>(instance);
                var actions = Require<BuildingActionView>(instance);
                var hud = Require<GameHudView>(instance);

                // Edit Mode의 일반 MonoBehaviour에는 런타임 이벤트 연결을 명시적으로 재현한다.
                // 먼저 해제하여 Editor 환경별 OnEnable 호출 차이에도 중복 등록하지 않는다.
                foreach (UIItemSlot slot in instance.GetComponentsInChildren<UIItemSlot>(true))
                    RebindLifecycle(slot);
                RebindLifecycle(rewardView);
                RebindLifecycle(rewardPresenter);
                RebindLifecycle(actions);
                RebindLifecycle(hud);

                ValidateRewardView(rewardView, temporaryAssets, checks);
                await ValidateRewardInterface(rewardPresenter, rewardView, temporaryAssets, checks);
                ValidateBuildingActions(actions, Require<BuildingInfoView>(instance), checks);
                ValidateHud(hud, checks);
                LastResult = "PASS (" + checks.Count + ")\n" + string.Join("\n", checks);
                Debug.Log("[InGameUIFlowValidation] " + LastResult);
                return LastResult;
            }
            catch (Exception exception)
            {
                LastResult = "FAIL after " + checks.Count + " checks: " + exception.Message;
                throw;
            }
            finally
            {
                if (rewardPresenter != null) rewardPresenter.ResetReward();
                if (rewardView != null) rewardView.ResetReward();
                foreach (UnityEngine.Object asset in temporaryAssets)
                    if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                if (validationEvents != null)
                    validationEvents.GetType().GetMethod("OnDisable", PrivateInstance)?.Invoke(validationEvents, null);
                EditorSceneManager.ClosePreviewScene(preview);
                for (int i = temporaryFonts.Count - 1; i >= 0; i--)
                    if (temporaryFonts[i] != null) UnityEngine.Object.DestroyImmediate(temporaryFonts[i]);
                if (previousEvents != null) EventSystem.current = previousEvents;
                if (previousEvents != null && previousFocus != null && previousFocus.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousFocus);
                _running = false;
            }
        }

        private static void ValidateRewardView(ArtifactRewardView view,
            List<UnityEngine.Object> temporaryAssets, List<string> checks)
        {
            var requests = new List<ArtifactData>();
            Action<ArtifactData> receive = requests.Add;
            var confirm = Field<UnityEngine.UI.Button>(view, "_confirmButton");
            var next = Field<UnityEngine.UI.Button>(view, "_nextPageButton");
            var previous = Field<UnityEngine.UI.Button>(view, "_previousPageButton");
            var clear = Field<UnityEngine.UI.Button>(view, "_clearButton");
            UnityEngine.UI.Button[] cards = RewardButtons(view);
            var fields = new SerializedObject(view);
            fields.FindProperty("_fallbackIcon").objectReferenceValue = null;
            fields.FindProperty("_showCardEffects").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            view.ChoiceRequested += receive;
            try
            {
                ArtifactData[] candidates = RewardCandidates("validation-view", 5, temporaryAssets);
                var mutableCandidates = new List<ArtifactData>(candidates);
                view.ShowSelection(mutableCandidates, true, null);
                Check(view.IsVisible && view.PageCount == 2 && view.CurrentPageIndex == 0,
                    "reward candidates use multiple pages", checks);
                SerializedProperty firstCard = fields.FindProperty("_cards").GetArrayElementAtIndex(0);
                var icon = (UnityEngine.UI.Image)firstCard.FindPropertyRelative("Icon").objectReferenceValue;
                var missingIcon = (GameObject)firstCard.FindPropertyRelative("MissingIcon").objectReferenceValue;
                Check(!icon.enabled && missingIcon.activeSelf,
                    "missing reward icon uses its placeholder without an empty image", checks);
                var rarity = (TMP_Text)firstCard.FindPropertyRelative("Rarity").objectReferenceValue;
                var effect = (TMP_Text)firstCard.FindPropertyRelative("Effect").objectReferenceValue;
                Check(rarity.text == "희귀" && rarity.color == (Color)new Color32(112, 186, 255, 255) &&
                    effect.text == "검증용\n설명",
                    "view formats the source rarity and escaped description newline", checks);
                SerializedProperty secondCard = fields.FindProperty("_cards").GetArrayElementAtIndex(1);
                var missingDescription = (TMP_Text)secondCard.FindPropertyRelative("Effect").objectReferenceValue;
                Check(missingDescription.text == "효과 설명 미등록",
                    "empty source description uses the display fallback", checks);
                mutableCandidates.Clear();
                Check(view.Candidates.Count == candidates.Length &&
                    ReferenceEquals(view.Candidates[0], candidates[0]),
                    "view snapshots the caller list while preserving original artifact references", checks);
                Click(next);
                Click(cards[1]);
                Check(view.SelectedArtifactId == candidates[4].Id && requests.Count == 0,
                    "browsing chooses an item without submitting a reward", checks);
                view.EndSelection();
                view.ShowSelection(candidates, true, "validation retry");
                Check(view.CurrentPageIndex == 1 && view.SelectedArtifactId == candidates[4].Id,
                    "retrying the same candidates preserves page and selection", checks);
                Click(previous);
                Click(confirm);
                Check(requests.Count == 1 && ReferenceEquals(requests[0], candidates[4]),
                    "confirmation returns the original artifact selected on another page", checks);
                confirm.onClick.Invoke();
                Check(requests.Count == 1 && view.IsRequestPending && !next.interactable,
                    "pending reward prevents duplicate submit and page changes", checks);
                Check(view.TryResolveSelection(false, "validation rejection") &&
                    view.SelectedArtifactId == candidates[4].Id,
                    "rejected reward keeps the selected candidate", checks);
                Check(!view.TryResolveSelection(true) && view.IsVisible,
                    "response without a pending selection does not close the view", checks);
                Click(clear);
                view.ShowSelection(candidates, false, null);
                confirm.onClick.Invoke();
                Check(!confirm.interactable && requests.Count == 1,
                    "required reward cannot submit an empty selection", checks);
                Click(cards[0]);
                Click(confirm);
                Check(requests.Count == 2 && ReferenceEquals(requests[1], candidates[0]),
                    "a rejected selection can be replaced with another original artifact", checks);
                view.ResetReward();
                view.ShowSelection(RewardCandidates("validation-next-reward", 1, temporaryAssets), false, null);
                Check(!view.TryResolveSelection(true) && view.IsVisible,
                    "reset removes the pending response before the next selection", checks);
            }
            finally
            {
                view.ResetReward();
                view.ChoiceRequested -= receive;
            }
        }

        private static async UniTask ValidateRewardInterface(ArtifactRewardPresenter presenter,
            ArtifactRewardView view, List<UnityEngine.Object> temporaryAssets, List<string> checks)
        {
            var candidates = new[]
            {
                Artifact("validation-a", "검증 유물 A", temporaryAssets),
                Artifact("validation-b", "검증 유물 B", temporaryAssets),
                Artifact("validation-c", "검증 유물 C", temporaryAssets)
            };
            IArtifactSelectionUI selectionUI = presenter;
            var confirm = Field<UnityEngine.UI.Button>(view, "_confirmButton");
            UnityEngine.UI.Button[] cards = RewardButtons(view);
            UIScreen screen = Field<UIScreen>(view, "_screen");
            int closed = 0;
            Action<UIScreen, UICloseReason> onClosed = (sender, reason) => closed++;
            screen.Closed += onClosed;
            try
            {
                bool emptyRejected = false;
                try { await selectionUI.SelectAsync(Array.Empty<ArtifactData>(), CancellationToken.None); }
                catch (ArgumentException) { emptyRejected = true; }
                Check(emptyRejected && !presenter.IsChoosing, "empty interface candidates do not start a reward", checks);
                var mutableCandidates = new List<ArtifactData>(candidates);
                UniTask<ArtifactData> selected = selectionUI.SelectAsync(mutableCandidates, CancellationToken.None);
                Check(view.IsVisible && presenter.IsChoosing, "interface shows reward selection", checks);
                mutableCandidates[1] = candidates[0];
                mutableCandidates.Clear();
                Check(presenter.CurrentCandidates.Count == candidates.Length &&
                    ReferenceEquals(presenter.CurrentCandidates[1], candidates[1]) &&
                    view.Candidates.Count == candidates.Length && ReferenceEquals(view.Candidates[1], candidates[1]),
                    "caller list mutation cannot replace the pending request candidates", checks);
                Check(!screen.Manager.CloseTopPopup() && view.IsVisible && selected.Status == UniTaskStatus.Pending,
                    "ESC cannot dismiss required reward selection", checks);
                Click(cards[1]);
                Click(confirm);
                confirm.onClick.Invoke();
                Check(selected.Status != UniTaskStatus.Pending, "selection task resolves after confirmation", checks);
                Check(ReferenceEquals(await selected, candidates[1]) && !view.IsVisible && closed == 1,
                    "interface returns the original artifact and closes once despite repeated confirmation", checks);
                presenter.ResetReward();
                presenter.ResetReward();
                Check(closed == 1, "repeated interface Close does not duplicate close notification", checks);

                UniTask<ArtifactData> forfeited = selectionUI.SelectAsync(candidates, CancellationToken.None, allowForfeit: true);
                Click(confirm);
                Check(forfeited.Status != UniTaskStatus.Pending && await forfeited == null,
                    "interface allows explicit forfeit when its contract permits it", checks);
                presenter.ResetReward();

                using (var cancellation = new CancellationTokenSource())
                {
                    UniTask<ArtifactData> pending = selectionUI.SelectAsync(candidates, cancellation.Token);
                    bool overlapRejected = false;
                    try { await selectionUI.SelectAsync(candidates, CancellationToken.None); }
                    catch (InvalidOperationException) { overlapRejected = true; }
                    Check(overlapRejected && pending.Status == UniTaskStatus.Pending,
                        "overlapping interface selection does not replace the first task", checks);
                    int before = closed;
                    cancellation.Cancel();
                    Check(pending.Status != UniTaskStatus.Pending, "token cancellation resolves pending task", checks);
                    Check(await WasCanceled(pending) && !view.IsVisible && !presenter.IsChoosing && closed == before + 1,
                        "token cancellation clears the reward and closes once", checks);
                    presenter.ResetReward();
                    Check(closed == before + 1, "Close after cancellation is idempotent", checks);
                }

                UniTask<ArtifactData> closedTask = selectionUI.SelectAsync(candidates, CancellationToken.None);
                int beforeClose = closed;
                screen.Manager.ClosePopup(screen.Id, UICloseReason.ContextLost);
                Check(closedTask.Status != UniTaskStatus.Pending && await WasCanceled(closedTask) &&
                    closed == beforeClose + 1 && !presenter.IsChoosing,
                    "external screen close cancels its pending task once", checks);
            }
            finally
            {
                presenter.ResetReward();
                screen.Closed -= onClosed;
            }
        }

        private static void ValidateBuildingActions(BuildingActionView view, BuildingInfoView info, List<string> checks)
        {
            var requests = new List<BuildingActionRequest>();
            Action<BuildingActionRequest> receive = requests.Add;
            var fields = new SerializedObject(view);
            SerializedProperty build = fields.FindProperty("_build");
            var button = (UnityEngine.UI.Button)build.FindPropertyRelative("_button").objectReferenceValue;
            var quote = (TMP_Text)build.FindPropertyRelative("_quote").objectReferenceValue;
            var offer = new BuildingActionOffer(BuildingUiAction.Build, "검증 건물", 120, 3, true, optionId: "building-a");
            view.ActionRequested += receive;
            try
            {
                // 실제 Presenter와 동일하게 상세 내용 부모를 먼저 연다.
                // ShowActions는 행동 행만 갱신하며 숨겨진 상세 부모의 수명을 소유하지 않는다.
                info.Popup.Manager.OpenPopup(info.Popup.Id);
                info.ShowBuildingInfo(new BuildingInfoData("slot-a", "검증 건물", "검증 분류", 1));
                view.SetActionsAllowed(true);
                view.ShowActions(new BuildingActionViewData("slot-a", "A 슬롯", build: offer));
                Check(quote.text.Contains("120") && quote.text.Contains("3") && button.interactable,
                    "building view displays provided gold and gem quote", checks);
                Click(button);
                button.onClick.Invoke();
                Check(requests.Count == 1 && requests[0].TargetId == "slot-a" &&
                    requests[0].OptionId == "building-a" && view.IsRequestPending,
                    "building action submits identity once while pending", checks);
                view.ShowActions(new BuildingActionViewData("slot-b", "B 슬롯", build: offer));
                Check(view.TargetId == "slot-b" && !button.interactable,
                    "changing selection keeps the old outstanding command locked", checks);
                Check(view.TryResolveRequest(requests[0].RequestId, true) && view.TargetId == "slot-b" &&
                    !view.IsAwaitingRefresh && button.interactable,
                    "old selection response does not mark the new selection completed", checks);
                Check(!view.TryResolveRequest(requests[0].RequestId, true),
                    "duplicate building response is ignored", checks);
                Click(button);
                Check(view.TryResolveRequest(requests[1].RequestId, true) && view.IsAwaitingRefresh && !button.interactable,
                    "successful current action waits for a fresh game quote", checks);
                view.ShowActions(new BuildingActionViewData("slot-b", "B 슬롯", build: offer));
                view.SetActionsAllowed(false, "검증 페이즈 잠금");
                button.onClick.Invoke();
                Check(requests.Count == 2 && !button.interactable,
                    "phase lock prevents building commands", checks);
            }
            finally
            {
                view.HideActions();
                info.HideBuildingInfo();
                view.ActionRequested -= receive;
            }
        }

        private static void ValidateHud(GameHudView hud, List<string> checks)
        {
            //hud.Initialize();
            var button = Field<UnityEngine.UI.Button>(hud, "_waveStartButton");
            int starts = 0;
            Action onStart = () => starts++;
            hud.WaveStartRequested += onStart;
            try
            {
                hud.SetWaveStartInteractable(true);
                Click(button);
                button.onClick.Invoke();
                Check(starts == 1, "HUD start button prevents duplicate requests", checks);
                GameObject focus = EventSystem.current.currentSelectedGameObject;
                hud.SetGold(1234);
                Check(Field<TMP_Text>(hud, "_goldText").text == 1234.ToString("N0") &&
                    EventSystem.current.currentSelectedGameObject == focus,
                    "HUD balance update changes only the value and preserves focus", checks);
            }
            finally { hud.WaveStartRequested -= onStart; }
        }

        private static async UniTask<bool> WasCanceled(UniTask<ArtifactData> task)
        {
            try { await task; return false; }
            catch (OperationCanceledException) { return true; }
        }

        private static ArtifactData[] RewardCandidates(string prefix, int count,
            List<UnityEngine.Object> temporaryAssets)
        {
            var candidates = new ArtifactData[count];
            for (int i = 0; i < count; i++)
            {
                candidates[i] = Artifact(prefix + "-" + i, "검증 유물 " + i, temporaryAssets);
                var fields = new SerializedObject(candidates[i]);
                fields.FindProperty("_rarity").intValue = (int)ArtifactRarity.Rare;
                fields.FindProperty("_description").stringValue = i == 1 ? "" : "검증용\\n설명";
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            return candidates;
        }

        private static ArtifactData Artifact(string id, string name, List<UnityEngine.Object> temporaryAssets)
        {
            var artifact = ScriptableObject.CreateInstance<ArtifactData>();
            artifact.hideFlags = HideFlags.HideAndDontSave;
            temporaryAssets.Add(artifact);
            var fields = new SerializedObject(artifact);
            fields.FindProperty("_id").stringValue = id;
            fields.FindProperty("_displayName").stringValue = name;
            fields.FindProperty("_description").stringValue = "효과를 실제 적용하지 않는 검증용 데이터";
            fields.ApplyModifiedPropertiesWithoutUndo();
            return artifact;
        }

        private static UnityEngine.UI.Button[] RewardButtons(ArtifactRewardView view)
        {
            var fields = new SerializedObject(view);
            SerializedProperty cards = fields.FindProperty("_cards");
            var buttons = new UnityEngine.UI.Button[cards.arraySize];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = (UnityEngine.UI.Button)cards.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Button").objectReferenceValue;
            return buttons;
        }

        private static T Field<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object
        {
            var fields = new SerializedObject(owner);
            SerializedProperty property = fields.FindProperty(name);
            T result = property != null ? property.objectReferenceValue as T : null;
            if (result == null) throw new InvalidOperationException(owner.GetType().Name + "." + name + " is missing.");
            return result;
        }

        private static T Require<T>(GameObject root) where T : Component
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result == null) throw new InvalidOperationException(typeof(T).Name + " is missing from the UI prefab.");
            return result;
        }

        private static void RebindLifecycle(MonoBehaviour component)
        {
            Type type = component.GetType();
            type.GetMethod("OnDisable", PrivateInstance)?.Invoke(component, null);
            type.GetMethod("OnEnable", PrivateInstance)?.Invoke(component, null);
        }

        private static void Click(UnityEngine.UI.Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                throw new InvalidOperationException("The expected UI button is not interactable.");
            button.onClick.Invoke();
        }

        private static void Check(bool success, string message, List<string> checks)
        {
            if (!success) throw new InvalidOperationException(message);
            checks.Add(message);
        }
    }
}
#endif
