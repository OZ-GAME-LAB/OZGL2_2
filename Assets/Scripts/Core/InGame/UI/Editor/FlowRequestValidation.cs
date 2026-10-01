#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.InGame.EditorTools
{
    /// <summary>게임 지급/씬 이동 없이 요청 UI의 입력 결과와 취소 수명만 검사한다.</summary>
    public static class FlowRequestValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string LastResult { get; private set; }

        [MenuItem("Game/UI/InGame/Validate Flow Request Contracts")]
        private static void FromMenu() => ValidateAsync().Forget(Debug.LogException);

        public static async UniTask ValidateAsync()
        {
            var checks = new List<string>();
            Scene activeScene = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject host = new GameObject("Flow request contract validation");
            SceneManager.MoveGameObjectToScene(host, preview);
            try
            {
                var manager = host.AddComponent<InGameUIManager>();
                UIScreen continueScreen = Screen(host.transform, UIId.WaveReward);
                UIScreen decisionScreen = Screen(host.transform, UIId.RunDecision);
                UIScreen settlementScreen = Screen(host.transform, UIId.RunResult);
                Set(manager, "_screenInstances", new[] { continueScreen, decisionScreen, settlementScreen });

                var continueView = continueScreen.gameObject.AddComponent<ContinueView>();
                var continueText = Label(continueScreen.Root.transform, "Message");
                var continueButton = Button(continueScreen.Root.transform, "Continue");
                Set(continueView, "_screen", continueScreen);
                Set(continueView, "_messageText", continueText);
                Set(continueView, "_continueButton", continueButton);

                var decision = decisionScreen.gameObject.AddComponent<RunDecisionView>();
                var decisionText = Label(decisionScreen.Root.transform, "Description");
                var finishButton = Button(decisionScreen.Root.transform, "Finish");
                var nextButton = Button(decisionScreen.Root.transform, "Continue");
                Set(decision, "_screen", decisionScreen);
                Set(decision, "_descriptionText", decisionText);
                Set(decision, "_finishButton", finishButton);
                Set(decision, "_continueButton", nextButton);

                var settlement = settlementScreen.gameObject.AddComponent<SettlementView>();
                Set(settlement, "_screen", settlementScreen);
                Set(settlement, "_root", settlementScreen.Root);
                foreach (string field in new[] { "_title", "_totems", "_progress", "_artifacts", "_score", "_bloodstone", "_gaugeText", "_notice" })
                    Set(settlement, field, Label(settlementScreen.Root.transform, field));
                var fill = new GameObject("Gauge", typeof(RectTransform));
                fill.transform.SetParent(settlementScreen.Root.transform, false);
                Set(settlement, "_gaugeFill", fill.GetComponent<RectTransform>());
                var mainButton = Button(settlementScreen.Root.transform, "Main");
                Set(settlement, "_mainButton", mainButton);

                Check(manager.InitializeScreens(), "UI requests initialize without game managers", checks);
                await ValidateContinue(manager, continueView, continueScreen, continueText, continueButton, checks);
                await ValidateDecision(manager, decision, decisionScreen, decisionText, finishButton, nextButton, checks);
                await ValidateSettlement(manager, settlement, settlementScreen, mainButton, checks);
                Check(SceneManager.GetActiveScene() == activeScene,
                    "confirming prepared settlement data does not load a scene", checks);
                Check(manager.OpenPopupCount == 0, "all requests finish with an empty popup stack", checks);
                LastResult = "PASS (" + checks.Count + ")\n" + string.Join("\n", checks);
                Debug.Log("[FlowRequestValidation] " + LastResult);
            }
            catch (Exception exception)
            {
                LastResult = "FAIL after " + checks.Count + " checks: " + exception.Message;
                throw;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static async UniTask ValidateContinue(InGameUIManager manager, ContinueView view,
            UIScreen screen, TMP_Text text, UnityEngine.UI.Button button, List<string> checks)
        {
            IContinueUI contract = view;
            UniTask pending = contract.ShowAsync("골드 +12 · 보석 +3", CancellationToken.None);
            var oldClosed = Read<Action<UIScreen, UICloseReason>>(screen, "Closed");
            Check(view.IsPending && screen.IsVisible && text.text == "골드 +12 · 보석 +3",
                "continue request opens with the supplied message", checks);
            await RejectDuplicate(contract.ShowAsync("replacement", CancellationToken.None), checks);
            Check(text.text == "골드 +12 · 보석 +3", "duplicate request preserves the original message", checks);
            button.onClick.Invoke();
            button.onClick.Invoke();
            await pending;
            Check(!view.IsPending && !screen.IsVisible && !button.interactable,
                "confirmation cleans up before returning and ignores a second click", checks);

            pending = contract.ShowAsync("new request", CancellationToken.None);
            oldClosed?.Invoke(screen, UICloseReason.ContextLost);
            Check(view.IsPending && pending.Status == UniTaskStatus.Pending,
                "an old screen callback cannot cancel a new continue request", checks);
            manager.ClosePopup(screen.Id, UICloseReason.ContextLost);
            await ExpectCanceled(pending, checks);
            Check(!view.IsPending && !screen.IsVisible, "external closure clears the continue request", checks);

            using (var cancellation = new CancellationTokenSource())
            {
                pending = contract.ShowAsync("cancel", cancellation.Token);
                cancellation.Cancel();
                await ExpectCanceled(pending, checks);
                Check(!view.IsPending && !screen.IsVisible, "token cancellation closes the continue request", checks);
                await ExpectCanceled(contract.ShowAsync("already canceled", cancellation.Token), checks);
                Check(!screen.IsVisible, "an already canceled request does not open a popup", checks);
            }

            pending = contract.ShowAsync("disabled", CancellationToken.None);
            Disable(view);
            await ExpectCanceled(pending, checks);
            Check(!view.IsPending && !screen.IsVisible, "disabled continue view cancels its request", checks);
            view.enabled = true;
        }

        private static async UniTask ValidateDecision(InGameUIManager manager, RunDecisionView view,
            UIScreen screen, TMP_Text text, UnityEngine.UI.Button finish, UnityEngine.UI.Button proceed,
            List<string> checks)
        {
            IRunDecisionUI contract = view;
            UniTask<RunDecision> pending = contract.ChooseAsync(3, CancellationToken.None);
            var oldClosed = Read<Action<UIScreen, UICloseReason>>(screen, "Closed");
            Check(text.text == "3분기 돌파!\n승리로 마무리하거나 다음 분기에 도전할 수 있습니다.",
                "decision uses the supplied quarter and preserves the display copy", checks);
            await RejectDuplicate(contract.ChooseAsync(4, CancellationToken.None), checks);
            Check(!manager.CloseTopPopup() && view.IsPending,
                "required decision cannot be dismissed as a user cancellation", checks);
            proceed.onClick.Invoke();
            finish.onClick.Invoke();
            Check(await pending == RunDecision.Continue && !view.IsPending && !screen.IsVisible,
                "continue choice returns once after closing the view", checks);

            pending = contract.ChooseAsync(4, CancellationToken.None);
            oldClosed?.Invoke(screen, UICloseReason.Replaced);
            Check(pending.Status == UniTaskStatus.Pending,
                "an old screen callback cannot cancel a new decision", checks);
            finish.onClick.Invoke();
            Check(await pending == RunDecision.Finish && !view.IsPending,
                "finish choice is distinct from continue", checks);

            using (var cancellation = new CancellationTokenSource())
            {
                pending = contract.ChooseAsync(4, cancellation.Token);
                cancellation.Cancel();
                await ExpectCanceled(pending, checks);
            }
            pending = contract.ChooseAsync(4, CancellationToken.None);
            manager.CloseAllPopups(UICloseReason.Replaced);
            await ExpectCanceled(pending, checks);
            pending = contract.ChooseAsync(4, CancellationToken.None);
            Disable(view);
            await ExpectCanceled(pending, checks);
            Check(!view.IsPending && !screen.IsVisible, "decision cancels cleanly on token, replacement and disable", checks);
            view.enabled = true;
        }

        private static async UniTask ValidateSettlement(InGameUIManager manager, SettlementView view,
            UIScreen screen, UnityEngine.UI.Button button, List<string> checks)
        {
            IRunSettlementUI contract = view;
            var data = new RunSettlementViewData(true, "토템 ×1.2", "3분기", "희귀 2개", 1234, 120, 4, 10);
            UniTask pending = contract.ShowAndWaitAsync(data, CancellationToken.None);
            var oldClosed = Read<Action<UIScreen, UICloseReason>>(screen, "Closed");
            Check(Read<TMP_Text>(view, "_score").text == 1234.ToString("N0") &&
                Read<TMP_Text>(view, "_bloodstone").text == $"획득 혈석  {120:N0}" &&
                Mathf.Approximately(Read<RectTransform>(view, "_gaugeFill").anchorMax.x, .4f),
                "settlement displays prepared score, bloodstones and gauge without calculating rewards", checks);
            await RejectDuplicate(contract.ShowAndWaitAsync(data, CancellationToken.None), checks);
            button.onClick.Invoke();
            button.onClick.Invoke();
            await pending;
            Check(!view.IsPending && !screen.IsVisible, "settlement confirmation finishes after cleanup", checks);

            pending = contract.ShowAndWaitAsync(data, CancellationToken.None);
            oldClosed?.Invoke(screen, UICloseReason.ContextLost);
            Check(pending.Status == UniTaskStatus.Pending,
                "an old screen callback cannot cancel a new settlement", checks);
            manager.ClosePopup(screen.Id, UICloseReason.ContextLost);
            await ExpectCanceled(pending, checks);
            using (var cancellation = new CancellationTokenSource())
            {
                pending = contract.ShowAndWaitAsync(data, cancellation.Token);
                cancellation.Cancel();
                await ExpectCanceled(pending, checks);
            }
            pending = contract.ShowAndWaitAsync(data, CancellationToken.None);
            Disable(view);
            await ExpectCanceled(pending, checks);
            Check(!view.IsPending && !screen.IsVisible, "settlement cancels cleanly on closure, token and disable", checks);
        }

        private static async UniTask RejectDuplicate(UniTask task, List<string> checks)
        {
            try { await task; }
            catch (InvalidOperationException) { Check(true, "overlapping request is rejected", checks); return; }
            throw new InvalidOperationException("Overlapping request was accepted.");
        }

        private static async UniTask RejectDuplicate<T>(UniTask<T> task, List<string> checks)
        {
            try { await task; }
            catch (InvalidOperationException) { Check(true, "overlapping request is rejected", checks); return; }
            throw new InvalidOperationException("Overlapping request was accepted.");
        }

        private static async UniTask ExpectCanceled(UniTask task, List<string> checks)
        {
            try { await task; }
            catch (OperationCanceledException) { Check(true, "request returns cancellation", checks); return; }
            throw new InvalidOperationException("Canceled request returned success.");
        }

        private static async UniTask ExpectCanceled<T>(UniTask<T> task, List<string> checks)
        {
            try { await task; }
            catch (OperationCanceledException) { Check(true, "request returns cancellation", checks); return; }
            throw new InvalidOperationException("Canceled request returned a selection.");
        }

        private static UIScreen Screen(Transform parent, UIId id)
        {
            var owner = new GameObject(id.ToString(), typeof(RectTransform));
            owner.transform.SetParent(parent, false);
            var display = new GameObject("Display", typeof(RectTransform), typeof(CanvasGroup));
            display.transform.SetParent(owner.transform, false);
            var screen = owner.AddComponent<UIScreen>();
            Set(screen, "_id", id);
            Set(screen, "_root", display);
            Set(screen, "_inputGroup", display.GetComponent<CanvasGroup>());
            Set(screen, "_canCloseByUser", false);
            Set(screen, "_blocksHudInput", true);
            return screen;
        }

        private static TMP_Text Label(Transform parent, string name)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);
            return label.GetComponent<TMP_Text>();
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(parent, false);
            return button.GetComponent<UnityEngine.UI.Button>();
        }

        private static void Disable(MonoBehaviour target)
        {
            target.enabled = false;
            // 일반 MonoBehaviour는 Edit Mode에서 수명 콜백을 자동 실행하지 않는다.
            target.GetType().GetMethod("OnDisable", Fields)?.Invoke(target, null);
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Fields).SetValue(target, value);

        private static T Read<T>(object target, string field) =>
            (T)target.GetType().GetField(field, Fields).GetValue(target);

        private static void Check(bool condition, string message, List<string> checks)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        }
    }
}
#endif
