#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.UI.InGame.Editor
{
    /// <summary>메모리 객체와 UI 프리팹 복사본으로 선택·지급·취소 계약을 검사한다. 에셋을 저장하지 않는다.</summary>
    public static class ArtifactRequestValidation
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static string LastResult { get; private set; } = "Not run";
        private static bool _running;

        public static async UniTask<string> RunAsync(string prefabPath = "Assets/Prefabs/UI/Test/InGameUIRoot.prefab")
        {
            if (_running || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run one artifact request validation at a time in Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new ArgumentException("UI prefab not found: " + prefabPath);
            _running = true;
            LastResult = "Running";
            Scene preview = EditorSceneManager.NewPreviewScene();
            var assets = new List<UnityEngine.Object>();
            var fonts = new List<UnityEngine.Object>();
            var checks = new List<string>();
            EventSystem previousEvents = EventSystem.current;
            GameObject previousFocus = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
            EventSystem events = null;
            ArtifactRewardPresenter presenter = null;
            ArtifactManager manager = null;
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                instance.SetActive(false);
                InGameUIValidation.PrepareFontsForRoots(new[] { instance }, fonts);
                instance.SetActive(true);
                var eventRoot = new GameObject("Artifact validation EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventRoot, preview);
                events = eventRoot.GetComponent<EventSystem>();
                Rebind(events);
                EventSystem.current = events;
                var ui = instance.GetComponentInChildren<InGameUIManager>(true);
                Check(ui != null && ui.InitializeScreens() && ui.ShowHud(), "isolated UI registry opens", checks);
                presenter = instance.GetComponentInChildren<ArtifactRewardPresenter>(true);
                var view = instance.GetComponentInChildren<ArtifactRewardView>(true);
                if (presenter == null || view == null) throw new InvalidOperationException("Artifact UI is missing.");
                foreach (UIItemSlot slot in instance.GetComponentsInChildren<UIItemSlot>(true)) Rebind(slot);
                Rebind(view);
                Rebind(presenter);

                T Asset<T>() where T : ScriptableObject
                {
                    var asset = ScriptableObject.CreateInstance<T>();
                    asset.hideFlags = HideFlags.HideAndDontSave;
                    assets.Add(asset);
                    return asset;
                }
                var candidates = new ArtifactData[4];
                for (int i = 0; i < candidates.Length; i++)
                {
                    candidates[i] = Asset<ArtifactData>();
                    Set(candidates[i], "_id", "request-validation-" + i);
                    Set(candidates[i], "_displayName", "검증 유물 " + i);
                    Set(candidates[i], "_description", "검증용 유물");
                }
                var catalog = Asset<ArtifactCatalog>();
                Set(catalog, "_artifacts", new List<ArtifactData>(candidates));
                Call(catalog, "OnValidate");
                var systems = new GameObject("Artifact validation systems");
                systems.SetActive(false);
                SceneManager.MoveGameObjectToScene(systems, preview);
                var flow = systems.AddComponent<GameFlowController>();
                var nodes = new NodeController(null);
                Set(nodes, "<Nodes>k__BackingField", new Node[] { new Node(1, Asset<WaveSO>(), PostBattleEventType.None) });
                Set(nodes, "<CurrentQuarter>k__BackingField", 1);
                Set(nodes, "<CurrentWave>k__BackingField", 0);
                Set(flow, "_nodeController", nodes);
                var wave = systems.AddComponent<WaveController>();
                Set(wave, "_controller", flow);
                ArtifactManager NewManager()
                {
                    var result = systems.AddComponent<ArtifactManager>();
                    Set(result, "_artifactCatalog", catalog);
                    Set(result, "_rewardTable", Asset<ArtifactRewardTable>());
                    result.Initialize(wave, systems.AddComponent<EffectManager>());
                    return result;
                }
                manager = NewManager();
                var confirm = Get<UnityEngine.UI.Button>(view, "_confirmButton");
                var next = Get<UnityEngine.UI.Button>(view, "_nextPageButton");
                var previous = Get<UnityEngine.UI.Button>(view, "_previousPageButton");
                var cards = Buttons(view);

                Check(manager.TryAdd(candidates[3]), "fixture fills the last candidate to maximum stacks", checks);
                UniTask<bool> retry = manager.TestSelectAndApplyAsync(candidates, CancellationToken.None);
                Check(retry.Status == UniTaskStatus.Pending && !confirm.interactable,
                    "required selection waits without allowing an empty confirmation", checks);
                Click(next);
                Click(cards[0]);
                Click(confirm);
                Check(retry.Status == UniTaskStatus.Pending && view.IsVisible &&
                    view.CurrentPageIndex == 1 && view.SelectedArtifactId == candidates[3].Id &&
                    manager.SelectionCandidates.Count == 4 && !manager.IsRewardApplied,
                    "real application failure retains candidates, page and selected card", checks);
                Click(previous);
                Click(cards[0]);
                Click(confirm);
                Check(await Completed(retry) && manager.Instances.Count == 2 && manager.IsRewardApplied && !view.IsVisible,
                    "another choice grants once and completes the manager request", checks);
                Check(await manager.SelectAndApplyAsync(default, CancellationToken.None) && !view.IsVisible &&
                    manager.Instances.Count == 2, "completed reward does not reopen or grant again", checks);

                using (var cancellation = new CancellationTokenSource())
                {
                    UniTask<bool> pending = manager.TestSelectAndApplyAsync(candidates, cancellation.Token);
                    ArtifactSaveData saved = manager.CaptureSaveData();
                    bool overlapRejected = !await manager.SelectAndApplyAsync(default, CancellationToken.None);
                    cancellation.Cancel();
                    Check(await Canceled(pending) && overlapRejected && !view.IsVisible && !manager.IsSelectingReward &&
                        manager.SelectionCandidates.Count == 4, "cancel releases UI and preserves unpaid candidates", checks);
                    manager.RestoreSaveData(saved);
                    UniTask<bool> resumed = manager.SelectAndApplyAsync(default, CancellationToken.None);
                    Check(view.IsVisible && presenter.CurrentCandidates.Count == candidates.Length &&
                        SameCandidates(presenter.CurrentCandidates, candidates) &&
                        SameCandidates(view.Candidates, candidates),
                        "save restore requests the original artifact references in the same order", checks);
                    Click(cards[2]);
                    Click(confirm);
                    Check(await Completed(resumed) && manager.Instances.Count == 3, "restored request applies one selected reward", checks);
                }

                UniTask<ArtifactData> externalClose = presenter.SelectAsync(candidates, CancellationToken.None);
                Check(ui.ClosePopup(UIId.ArtifactReward, UICloseReason.Replaced),
                    "fixture closes a pending selection externally", checks);
                Check(await Canceled(externalClose) && !presenter.IsChoosing && !view.IsVisible,
                    "external popup close cancels the selection task", checks);
                UniTask<ArtifactData> disabled = presenter.SelectAsync(candidates, CancellationToken.None);
                presenter.enabled = false;
                Call(presenter, "OnDisable");
                Check(await Canceled(disabled) && !view.IsVisible, "presenter disable cancels its request", checks);
                presenter.enabled = true;

                UniTask<ArtifactData> forfeit = presenter.SelectAsync(candidates, CancellationToken.None, true);
                Click(confirm);
                Check(await Completed(forfeit) == null && !view.IsVisible, "explicitly allowed forfeit returns null", checks);
                Check(await manager.TestSelectAndApplyAsync(Array.Empty<ArtifactData>(), CancellationToken.None) &&
                    !manager.IsSelectingReward, "empty reward completes without calling UI", checks);

                bool failed = false;
                try { await manager.TestSelectAndApplyAsync(candidates, CancellationToken.None); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed && !manager.IsSelectingReward && !manager.IsRewardApplied &&
                    manager.SelectionCandidates.Count == 4, "UI exception stops without consuming candidates", checks);

                var appliedThenCanceled = NewManager();
                var cancellationChoice = new FixedChoiceUI(candidates[0]);
                using (var cancellation = new CancellationTokenSource())
                {
                    Action<ArtifactInstance, int, int> cancelAfterApply = (artifact, before, after) => cancellation.Cancel();
                    appliedThenCanceled.StackChanged += cancelAfterApply;
                    try
                    {
                        var canceled = appliedThenCanceled.TestSelectAndApplyAsync(candidates, cancellation.Token);
                        Check(await Canceled(canceled) && appliedThenCanceled.IsRewardApplied &&
                            appliedThenCanceled.Instances.Count == 1 && !appliedThenCanceled.IsSelectingReward,
                            "cancellation after application preserves the granted reward", checks);
                    }
                    finally { appliedThenCanceled.StackChanged -= cancelAfterApply; }
                }
                Check(await appliedThenCanceled.SelectAndApplyAsync(default, CancellationToken.None) &&
                    cancellationChoice.Calls == 1 && appliedThenCanceled.Instances[0].StackCount == 1,
                    "resuming after application cancellation neither reopens nor grants twice", checks);

                var appliedThenFailed = NewManager();
                var failingChoice = new FixedChoiceUI(candidates[0]);
                Action<ArtifactInstance, int, int> throwAfterApply = (artifact, before, after) =>
                    throw new InvalidOperationException("Expected application listener failure.");
                appliedThenFailed.StackChanged += throwAfterApply;
                bool applicationFailed = false;
                try { await appliedThenFailed.TestSelectAndApplyAsync(candidates, CancellationToken.None); }
                catch (InvalidOperationException) { applicationFailed = true; }
                finally { appliedThenFailed.StackChanged -= throwAfterApply; }
                Check(applicationFailed && failingChoice.Calls == 1 && !appliedThenFailed.IsSelectingReward &&
                    appliedThenFailed.Instances.Count == 1 && appliedThenFailed.Instances[0].StackCount == 1,
                    "exception after inventory application stops without automatic retry", checks);
                Check(!await appliedThenFailed.SelectAndApplyAsync(default, CancellationToken.None) &&
                    failingChoice.Calls == 1 && appliedThenFailed.Instances[0].StackCount == 1,
                    "partially applied exception cannot issue another reward", checks);
                LastResult = "PASS (" + checks.Count + ")\n" + string.Join("\n", checks);
                return LastResult;
            }
            catch (Exception exception)
            {
                LastResult = "FAIL after " + checks.Count + " checks: " + exception.Message;
                throw;
            }
            finally
            {
                if (presenter != null) presenter.ResetReward();
                if (manager != null) manager.TryEndRun();
                if (events != null) Call(events, "OnDisable");
                EditorSceneManager.ClosePreviewScene(preview);
                for (int i = fonts.Count - 1; i >= 0; i--)
                    if (fonts[i] != null) UnityEngine.Object.DestroyImmediate(fonts[i]);
                foreach (var asset in assets) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                if (previousEvents != null)
                {
                    EventSystem.current = previousEvents;
                    if (previousFocus != null && previousFocus.activeInHierarchy)
                        previousEvents.SetSelectedGameObject(previousFocus);
                }
                _running = false;
            }
        }

        private sealed class RejectingUI : IArtifactSelectionUI
        {
            public UniTask<ArtifactData> SelectAsync(IReadOnlyList<ArtifactData> candidates, CancellationToken token,
                bool allowForfeit = false, string message = null) =>
                throw new InvalidOperationException("Empty candidates must not request UI.");
        }

        private sealed class ThrowingUI : IArtifactSelectionUI
        {
            public UniTask<ArtifactData> SelectAsync(IReadOnlyList<ArtifactData> candidates, CancellationToken token,
                bool allowForfeit = false, string message = null) =>
                throw new InvalidOperationException("Expected UI failure.");
        }

        private sealed class FixedChoiceUI : IArtifactSelectionUI
        {
            private readonly ArtifactData _selected;
            public int Calls { get; private set; }
            public FixedChoiceUI(ArtifactData selected) => _selected = selected;
            public UniTask<ArtifactData> SelectAsync(IReadOnlyList<ArtifactData> candidates, CancellationToken token,
                bool allowForfeit = false, string message = null)
            {
                Calls++;
                token.ThrowIfCancellationRequested();
                return UniTask.FromResult(_selected);
            }
        }

        private static async UniTask<bool> Canceled<T>(UniTask<T> task)
        {
            if (task.Status == UniTaskStatus.Pending)
                throw new InvalidOperationException("Expected cancellation has not completed.");
            try { await task; return false; }
            catch (OperationCanceledException) { return true; }
        }

        private static UniTask<T> Completed<T>(UniTask<T> task)
        {
            if (task.Status == UniTaskStatus.Pending)
                throw new InvalidOperationException("Expected request has not completed.");
            return task;
        }

        private static UnityEngine.UI.Button[] Buttons(ArtifactRewardView view)
        {
            var fields = new SerializedObject(view);
            var cards = fields.FindProperty("_cards");
            var buttons = new UnityEngine.UI.Button[cards.arraySize];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = (UnityEngine.UI.Button)cards.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Button").objectReferenceValue;
            return buttons;
        }

        private static bool SameCandidates(IReadOnlyList<ArtifactData> actual, IReadOnlyList<ArtifactData> expected)
        {
            if (actual == null || actual.Count != expected.Count) return false;
            for (int i = 0; i < actual.Count; i++)
                if (!ReferenceEquals(actual[i], expected[i])) return false;
            return true;
        }

        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
        private static void Call(object owner, string name) => owner.GetType().GetMethod(name, Fields)?.Invoke(owner, null);
        private static void Rebind(MonoBehaviour component) { Call(component, "OnDisable"); Call(component, "OnEnable"); }
        private static void Click(UnityEngine.UI.Button button)
        {
            if (!button.gameObject.activeInHierarchy || !button.IsInteractable())
                throw new InvalidOperationException("Expected button is not interactable.");
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
