using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.UI.Editor
{
    /// <summary>실제 로컬 코어의 단계 전환 + 모의 건물 견적/응답을 사용하는 Play Mode 검사.</summary>
    public static class MvpBuildingPhaseValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            await UniTask.NextFrame();
            await UniTask.NextFrame();
            var sample = Object.FindFirstObjectByType<MvpRuntimeHudSample>();
            var mock = Object.FindFirstObjectByType<MvpBuildingPhaseSample>();
            var panel = Object.FindFirstObjectByType<BuildingActionPanel>();
            var binding = Object.FindFirstObjectByType<CoreBuildingActionBinding>();
            var flow = Object.FindFirstObjectByType<GameFlowController>();
            var waves = Object.FindFirstObjectByType<WaveController>();
            var gate = Object.FindFirstObjectByType<TestWaitingScript>();
            var wallet = Object.FindFirstObjectByType<RunCurrencyManager>();
            Check(sample != null && sample.IsReady && mock != null && panel != null && binding != null,
                "isolated scene starts with actual core and dedicated UI binding");
            var build = Field<Button>(panel, "_build._button");
            var upgrade = Field<Button>(panel, "_upgrade._button");
            var dismantle = Field<Button>(panel, "_dismantle._button");
            var status = Field<TMP_Text>(panel, "_status");
            var requests = new List<BuildingActionRequest>();
            Action<BuildingActionRequest> receive = requests.Add;
            float spawnTime = flow.SpawnTime;
            float stagingTime = flow.StagingTime;
            bool autoContinue = flow.AutoContinue;
            int gold = wallet.GetBalance(CurrencyType.Gold);
            Action<GamePhase> observePreparation = null;
            GameObject replacement = null;
            try
            {
                flow.SpawnTime = 0.02f;
                flow.StagingTime = 0.04f;
                mock.enabled = false;
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                Check(!build.interactable, "no request receiver remains disabled even in preparation");
                build.onClick.Invoke();
                Check(!panel.IsRequestPending, "missing receiver cannot create a pending request");
                panel.ActionRequested += receive;
                binding.Initialize(panel, flow);
                binding.Initialize(panel, flow);
                Check(flow.CanEnterBuildMode() && build.interactable, "preparation reads authoritative build permission");
                bool nullRejected = false;
                try { binding.Initialize(null, flow); } catch (ArgumentNullException) { nullRejected = true; }
                Check(nullRejected && build.interactable, "invalid panel reinitialization preserves valid connection");
                nullRejected = false;
                try { binding.Initialize(panel, null); } catch (ArgumentNullException) { nullRejected = true; }
                Check(nullRejected && build.interactable, "invalid core reinitialization preserves valid connection");

                build.onClick.Invoke();
                build.onClick.Invoke();
                Check(requests.Count == 1 && panel.IsRequestPending && !build.interactable,
                    "one accepted UI request despite double click and repeated initialization");
                Check(requests[0].Action == BuildingUiAction.Build && requests[0].TargetId == "sample-slot" &&
                    requests[0].OptionId == "warrior", "request retains target and building option IDs");
                Check(!panel.TryResolveRequest(Guid.NewGuid(), true), "wrong completion ID cannot unlock pending request");
                Check(panel.TryResolveRequest(requests[0].RequestId, false, "모의 실패") && build.interactable,
                    "matching rejection permits retry only in preparation");
                panel.ShowActions(MvpBuildingPhaseSample.CreateBuilding());
                Check(upgrade.interactable && dismantle.interactable && !build.interactable, "building offers remain independent");
                upgrade.onClick.Invoke();
                Check(requests.Count == 2 && requests[1].Action == BuildingUiAction.Upgrade, "upgrade request forwarded once");
                panel.TryResolveRequest(requests[1].RequestId, false);
                dismantle.onClick.Invoke();
                Check(requests.Count == 3 && requests[2].Action == BuildingUiAction.Dismantle, "dismantle request forwarded once");
                panel.TryResolveRequest(requests[2].RequestId, false);
                Field<Button>(mock, "_unavailableButton").onClick.Invoke(); // disabled mock must not handle clicks
                Check(panel.TargetId == "sample-building", "disabled sample has no lingering button handler");
                panel.ShowActions(new BuildingActionViewData("blocked", "미개방 공간",
                    new BuildingActionOffer(BuildingUiAction.Build, "전사 훈련소", 30, false, "아직 개방되지 않은 공간입니다.", "warrior")));
                binding.Refresh();
                Check(!build.interactable && Field<TMP_Text>(panel, "_build._reason").text.Contains("개방"),
                    "phase permission never overrides unavailable quote");
                panel.ShowActions(MvpBuildingPhaseSample.CreateBuilding());
                await UniTask.Delay(200);
                Capture(1280, 720, "preparation");
                Capture(1920, 1080, "preparation");

                int preparationEvents = 0;
                observePreparation = phase =>
                {
                    if (phase != GamePhase.Preparation) return;
                    preparationEvents++;
                    Check(!flow.CanEnterBuildMode(), "core announces preparation before transition unlock");
                    Check(!build.interactable && !upgrade.interactable && !dismantle.interactable,
                        "UI does not unlock early inside preparation event");
                };
                flow.PhaseChanged += observePreparation;
                flow.TrySpawnUnits().Forget();
                Check(flow.CurPhase == GamePhase.BattlePreparing, "actual spawn transition entered");
                AssertLocked(panel, build, upgrade, dismantle, requests, "전투 준비");
                await WaitForPhase(flow, GamePhase.Battle);
                AssertLocked(panel, build, upgrade, dismantle, requests, "전투 중");
                await UniTask.Delay(200);
                Capture(1280, 720, "battle");
                Capture(1920, 1080, "battle");
                waves.SetSuccess();
                Check(flow.CurPhase == GamePhase.BattleResolving, "victory uses actual staging phase");
                AssertLocked(panel, build, upgrade, dismantle, requests, "전투 정산");
                await WaitForPhase(flow, GamePhase.Reward);
                AssertLocked(panel, build, upgrade, dismantle, requests, "보상 처리");
                gate.ChooseResultBtn();
                await WaitForPhase(flow, GamePhase.Preparation);
                Check(preparationEvents == 1 && upgrade.interactable && dismantle.interactable,
                    "normal reward completion unlocks on next frame without polling");

                // 요청 소유권은 건물 담당자에게 남긴다. 단계 변경/재연결이 요청을 취소하면 안 된다.
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                build.onClick.Invoke();
                var pending = requests[requests.Count - 1];
                binding.enabled = false;
                Check(panel.IsRequestPending && !build.interactable, "disable locks without discarding in-flight request");
                binding.enabled = true;
                binding.Initialize(panel, flow);
                Check(panel.IsRequestPending && !build.interactable, "enable/rebind cannot duplicate pending request");
                flow.TrySpawnUnits().Forget();
                await WaitForPhase(flow, GamePhase.Battle);
                panel.TryResolveRequest(pending.RequestId, false, "늦은 실패");
                Check(!panel.IsRequestPending && !build.interactable, "late failure during battle cannot unlock action");
                waves.SetFail();
                await WaitForPhase(flow, GamePhase.Finished);
                AssertLocked(panel, build, upgrade, dismantle, requests, "종료");

                flow.ResetRun();
                Check(panel.TargetId == null, "reset None phase clears stale selected target");
                await WaitForPhase(flow, GamePhase.Preparation);
                Check(!build.interactable && !upgrade.interactable, "reset requires a fresh selection quote");
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                build.onClick.Invoke();
                pending = requests[requests.Count - 1];
                flow.ResetRun();
                await WaitForPhase(flow, GamePhase.Preparation);
                Check(panel.TargetId == null && panel.IsRequestPending, "reset clears selection but preserves request ownership");
                panel.TryResolveRequest(pending.RequestId, true, "이전 플레이 성공");
                Check(panel.TargetId == null && !panel.IsAwaitingRefresh && !build.interactable,
                    "late old-run success cannot resurrect stale target");
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                build.onClick.Invoke();
                panel.TryResolveRequest(requests[requests.Count - 1].RequestId, true);
                binding.Refresh();
                Check(panel.IsAwaitingRefresh && !build.interactable, "successful response still waits for fresh quote");
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                Check(build.interactable, "explicit authoritative quote refresh permits next request");

                flow.AutoContinue = false;
                waves.JumpToLastQuarterForTest();
                waves.JumpToLastWaveForTest();
                flow.TrySpawnUnits().Forget();
                await WaitForPhase(flow, GamePhase.Battle);
                waves.SetSuccess();
                await WaitForPhase(flow, GamePhase.QuarterComplete);
                Check(flow.CanChooseRunDecision, "actual final-quarter decision is pending");
                AssertLocked(panel, build, upgrade, dismantle, requests, "분기 완료");
                flow.ChooseFinishRun();
                await WaitForPhase(flow, GamePhase.Finished);
                AssertLocked(panel, build, upgrade, dismantle, requests, "종료");

                // 다음 프레임 예약 작업의 취소와 새 실행으로의 누출을 검사한다.
                flow.ResetRun();
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                binding.enabled = false;
                flow.TrySpawnUnits().Forget();
                await WaitForPhase(flow, GamePhase.Battle);
                Check(!build.interactable, "cancelled preparation refresh cannot unlock later battle");
                binding.enabled = true;
                AssertLocked(panel, build, upgrade, dismantle, requests, "전투 중");
                flow.ResetRun();
                await WaitForPhase(flow, GamePhase.Preparation);
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                flow.enabled = false;
                binding.Refresh(); // Core has no enabled-state event; caller must explicitly refresh.
                AssertLocked(panel, build, upgrade, dismantle, requests, "비활성화");
                flow.enabled = true;
                binding.Refresh();
                Check(build.interactable, "explicit refresh observes source re-enable");
                panel.gameObject.SetActive(false);
                flow.TrySpawnUnits().Forget();
                await WaitForPhase(flow, GamePhase.Battle);
                panel.gameObject.SetActive(true);
                AssertLocked(panel, build, upgrade, dismantle, requests, "전투 중");
                flow.ResetRun();
                await WaitForPhase(flow, GamePhase.Preparation);

                flow.PhaseChanged -= observePreparation;
                observePreparation = null;
                replacement = new GameObject("Building phase replacement core test");
                var replacementFlow = replacement.AddComponent<GameFlowController>();
                binding.Initialize(panel, replacementFlow);
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                flow.ResetRun();
                await WaitForPhase(flow, GamePhase.Preparation);
                Check(!build.interactable && panel.TargetId == "sample-slot", "old core event is detached after rebind");
                Object.DestroyImmediate(replacement);
                replacement = null;
                Check(panel.TargetId == null && !build.interactable && status.text.Contains("종료"),
                    "source destruction automatically locks and clears stale selection");
                binding.Initialize(panel, flow);
                panel.ShowActions(MvpBuildingPhaseSample.CreateSlot());
                Check(build.interactable, "binding recovers with a valid replacement source");
                Check(wallet.GetBalance(CurrencyType.Gold) == gold, "UI requests and phase tests never spend or refund gold");

                panel.ActionRequested -= receive;
                mock.enabled = true;
                Field<Button>(mock, "_buildingButton").onClick.Invoke();
                upgrade.onClick.Invoke();
                Check(!panel.IsRequestPending && status.text.Contains("실제 건물 시스템"), "interactive mock is explicit, synchronous, and non-mutating");
                Field<Button>(mock, "_unavailableButton").onClick.Invoke();
                Check(!build.interactable && panel.TargetId == "sample-locked", "interactive locked-space sample works");
                Field<Button>(mock, "_slotButton").onClick.Invoke();
                Check(build.interactable && panel.TargetId == "sample-slot", "interactive selection restores slot quote");
                Check(wallet.GetBalance(CurrencyType.Gold) == gold, "interactive mock also leaves real economy unchanged");
                Debug.Log("[UI/MvpBuildingPhaseValidation] PASS " + _checks + " checks (custom Play Mode assertions; local core + mock building offers).");
            }
            finally
            {
                if (flow != null)
                {
                    if (observePreparation != null) flow.PhaseChanged -= observePreparation;
                    flow.enabled = true;
                    flow.SpawnTime = spawnTime;
                    flow.StagingTime = stagingTime;
                    flow.AutoContinue = autoContinue;
                }
                if (panel != null) panel.ActionRequested -= receive;
                if (replacement != null) Object.DestroyImmediate(replacement);
            }
        }

        private static void AssertLocked(BuildingActionPanel panel, Button build, Button upgrade, Button dismantle,
            List<BuildingActionRequest> requests, string reason)
        {
            Check(!build.interactable && !upgrade.interactable && !dismantle.interactable, "all actions locked: " + reason);
            Check(Field<TMP_Text>(panel, "_status").text.Contains(reason) ||
                Field<TMP_Text>(panel, "_build._reason").text.Contains(reason) ||
                Field<TMP_Text>(panel, "_upgrade._reason").text.Contains(reason), "localized reason: " + reason);
            int count = requests.Count;
            build.onClick.Invoke();
            upgrade.onClick.Invoke();
            dismantle.onClick.Invoke();
            Check(requests.Count == count, "forced UI callbacks cannot bypass phase lock: " + reason);
        }

        private static async UniTask WaitForPhase(GameFlowController flow, GamePhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (flow.CurPhase != phase)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Expected " + phase + "; got " + flow.CurPhase);
                await UniTask.NextFrame();
            }
            await UniTask.NextFrame();
            await UniTask.NextFrame();
        }

        private static T Field<T>(Object owner, string name) where T : Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Building phase: " + description);
        }

        private static void Capture(int width, int height, string state)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics device required for UI visual validation.");
            var camera = Camera.main;
            Check(camera != null, "capture camera available");
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Array.Sort(canvases, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var scales = new float[canvases.Length];
            var scalerEnabled = new bool[canvases.Length];
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            var cameraTarget = camera.targetTexture;
            Texture2D pixels = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                cameras[i] = canvases[i].worldCamera;
                distances[i] = canvases[i].planeDistance;
                scales[i] = canvases[i].scaleFactor;
                var scaler = canvases[i].GetComponent<CanvasScaler>();
                scalerEnabled[i] = scaler != null && scaler.enabled;
            }
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    var canvas = canvases[i];
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler != null) scaler.enabled = false;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1 + i;
                    canvas.scaleFactor = width / 1920f;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var canvas in canvases)
                    foreach (var label in canvas.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Check(label.font != null && label.font.HasCharacters(label.text, out uint[] missing, false, true),
                            state + " " + width + " glyphs: " + label.name);
                        Check(!label.isTextOverflowing, state + " " + width + " text fits: " + label.name);
                    }
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/BuildingPhaseValidation");
                File.WriteAllBytes("Logs/BuildingPhaseValidation/" + state + "-" + width + "x" + height + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                camera.targetTexture = cameraTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                    canvases[i].scaleFactor = scales[i];
                    var scaler = canvases[i].GetComponent<CanvasScaler>();
                    if (scaler != null) scaler.enabled = scalerEnabled[i];
                }
                if (pixels != null) Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
