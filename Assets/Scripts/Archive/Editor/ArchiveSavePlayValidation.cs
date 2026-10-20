#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Editor;
using Game.UI.InGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Object = UnityEngine.Object;

namespace Game.Archive.Editor
{
    public static class ArchiveSavePlayValidation
    {
        public static string Status { get; private set; }
        private static readonly List<string> Results = new();
        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static object Get(object target, string name) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Check(bool value, string name)
        {
            Results.Add((value ? "PASS " : "FAIL ") + name);
            if (!value) throw new Exception(name);
        }
        private static async UniTask Until(Func<bool> predicate)
        {
            float end = Time.realtimeSinceStartup + 30;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > end) throw new TimeoutException("Archive play check timeout");
                await UniTask.NextFrame();
            }
        }
        private static void Click(Button button)
        {
            Check(button.IsActive() && button.IsInteractable(), "EventSystem button is active and interactable");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }
        public static async UniTask Run()
        {
            Results.Clear();
            Status = "Running";
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                Check(Application.isPlaying && SessionState.GetBool("ProgressPlayChecks.Enabled", false), "isolated Play fixture prepared");
                await Until(() => SceneLoadingManager.Instance != null && !SceneLoadingManager.Instance.IsLoading);
                await SceneLoadingManager.Instance.LoadAsync("Test_OutGame");
                var coordinator = Find<OutGameSaveCoordinator>();
                var save = Find<SaveManager>();
                Check(Find<ArchiveController>() != null &&
                    (ArchiveController)Get(coordinator, "_archive") == Find<ArchiveController>(), "outgame uses its own archive provider");
                Check(save.DirectoryPath == SessionState.GetString("ProgressPlayChecks.Directory", ""), "user saves excluded");
                var seed = coordinator.CaptureSaveData();
                seed.Tutorial.InGameHandled = seed.Tutorial.OutGameHandled = true;
                Check(coordinator.TrySave(seed, out var error), "skip tutorial only in test data: " + error);
                Find<OutGameStartController>().StartRun();
                await Until(() => SceneManager.GetActiveScene().name == "Test_InGame" && Find<BootStrap>() != null &&
                    Find<BootStrap>().IsUIConnected && !SceneLoadingManager.Instance.IsLoading);
                await UniTask.NextFrame(); await UniTask.NextFrame();
                coordinator = Find<OutGameSaveCoordinator>(); save = Find<SaveManager>();
                var archive = Find<ArchiveController>();
                var artifacts = Find<ArtifactManager>();
                var ui = Find<InGameUIManager>();
                var view = Find<ArchiveView>();
                Check(archive != null && (ArchiveController)Get(coordinator, "_archive") == archive &&
                    (ArchiveController)Get(view, "_controller") == archive, "actual Bootstrap and UI references connected");
                string receipt = coordinator.LastSettledRunId;
                int balance = Find<PersistentCurrencyManager>().Balance;
                var artifact = artifacts.ArtifactCatalog.Artifacts.First(a => a != null && !artifacts.Contains(a));
                int before = save.SuccessfulWriteCount;
                Check(artifacts.TryAdd(artifact) && archive.IsNew(ArchiveCategory.Artifact, artifact.Id), "actual artifact gain discovers NEW");
                Check(save.SuccessfulWriteCount == before, "gain does not synchronously write file");
                await UniTask.NextFrame(); await UniTask.NextFrame();
                Check(save.SuccessfulWriteCount == before + 1 && !archive.IsDirty, "gain persists once at frame end");
                var open = ui.GetComponentsInChildren<OpenUtility>(true).First(o => o.isActiveAndEnabled &&
                    (UIId)Get(o, "_target") == UIId.Archive && o.GetComponentInParent<UIScreen>().IsHud);
                Time.timeScale = 1.5f;
                Click(open.GetComponent<Button>());
                await UniTask.NextFrame();
                Check(ui.TopPopup.Id == UIId.Archive && Mathf.Approximately(Time.timeScale, 0), "HUD opens archive and pauses");
                before = save.SuccessfulWriteCount;
                string previousFile = File.ReadAllText(coordinator.SavePath);
                var slots = (List<UIItemSlot>)Get(view, "_slots");
                var slot = slots.First(s => ((TMPro.TMP_Text)Get(s, "_title")).text == artifact.DisplayName);
                Click((Button)Get(slot, "_button"));
                await UniTask.NextFrame(); await UniTask.NextFrame();
                Check(!archive.IsNew(ArchiveCategory.Artifact, artifact.Id) && archive.IsDirty &&
                    save.SuccessfulWriteCount == before && File.ReadAllText(coordinator.SavePath) == previousFile, "read clears NEW without disk IO");
                var close = view.GetComponentInChildren<CloseUtility>(true);
                Click((Button)Get(close, "_button"));
                await UniTask.NextFrame(); await UniTask.NextFrame();
                Check(ui.OpenPopupCount == 0 && Mathf.Approximately(Time.timeScale, 1.5f) &&
                    save.SuccessfulWriteCount == before + 1, "close writes reads once and restores speed");
                before = save.SuccessfulWriteCount;
                Click(open.GetComponent<Button>());
                await UniTask.NextFrame();
#if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                Check(keyboard != null, "keyboard available for ESC input");
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Escape));
                await UniTask.NextFrame(); await UniTask.NextFrame();
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                await UniTask.NextFrame(); await UniTask.NextFrame();
#else
                ui.HandleEscape();
                await UniTask.NextFrame();
#endif
                Check(ui.OpenPopupCount == 0 && Mathf.Approximately(Time.timeScale, 1.5f) &&
                    save.SuccessfulWriteCount == before, "ESC unchanged close adds no write");
                Check(coordinator.LastSettledRunId == receipt && Find<PersistentCurrencyManager>().Balance == balance,
                    "archive UI does not change payout or receipt");
                Time.timeScale = 1;
                Find<GameFlowController>().QuitRun();
                await Until(() => SceneManager.GetActiveScene().name == "Test_MainScreen" && !SceneLoadingManager.Instance.IsLoading);
                await SceneLoadingManager.Instance.LoadAsync("Test_OutGame");
                coordinator = Find<OutGameSaveCoordinator>();
                Check(coordinator.CaptureSaveData().Archive.DiscoveredKeys.Contains("0:" + artifact.Id) &&
                    !coordinator.CaptureSaveData().Archive.UnreadKeys.Contains("0:" + artifact.Id) &&
                    Find<ArchiveController>().IsDiscovered(ArchiveCategory.Artifact, artifact.Id), "outgame provider restores discovered and read");
                Check(coordinator.TrySave(out error), "outgame save preserves archive: " + error);
                Find<OutGameStartController>().StartRun();
                await Until(() => SceneManager.GetActiveScene().name == "Test_InGame" && Find<BootStrap>() != null &&
                    Find<BootStrap>().IsUIConnected && !SceneLoadingManager.Instance.IsLoading);
                archive = Find<ArchiveController>();
                Check(archive.IsDiscovered(ArchiveCategory.Artifact, artifact.Id) && !archive.IsNew(ArchiveCategory.Artifact, artifact.Id),
                    "ingame reentry retains archive read state");
                Check(!Find<SaveManager>().HasSaveFile(ArchiveSaveMigration.LegacyKey), "runtime never writes legacy archive file");
                Status = string.Join("\n", Results);
            }
            catch (Exception exception) { Status = string.Join("\n", Results) + "\nERROR " + exception; }
            finally
            {
                Application.runInBackground = background;
                Directory.CreateDirectory("PersonalDocs/ArchiveSaveValidation");
                File.WriteAllText("PersonalDocs/ArchiveSaveValidation/play.txt", Status);
            }
        }
    }
}
#endif
