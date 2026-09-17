using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>팀원 코어 원본과 UI의 분기·종료 선택·취소 경계를 Play Mode에서 검사한다.</summary>
    public static class MvpCoreQuarterValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
            var ui = UnityEngine.Object.FindFirstObjectByType<GameUIController>();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveController>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
            var binding = ui.GetComponentInChildren<CoreRunDecisionBinding>(true);
            var hud = ui.GetComponent<CoreHudBinding>();
            Check(binding != null, "run decision UI is wired in our scene");
            var fields = new SerializedObject(sample);
            var reset = Field<Button>(fields, "_resetButton");
            var win = Field<Button>(fields, "_winButton");
            var lose = Field<Button>(fields, "_loseButton");
            var reward = Field<Button>(fields, "_rewardButton");
            var lastWave = Field<Button>(fields, "_lastWaveButton");
            var lastQuarter = Field<Button>(fields, "_lastQuarterButton");
            var uiFields = new SerializedObject(ui);
            var start = Field<Button>(uiFields, "_waveStartButton");
            var phaseText = Field<TMP_Text>(uiFields, "_phaseText");
            var quarterText = Field<TMP_Text>(new SerializedObject(hud), "_quarterText");
            var decisionFields = new SerializedObject(binding);
            var finish = Field<Button>(decisionFields, "_finishButton");
            var next = Field<Button>(decisionFields, "_continueButton");
            float previousStaging = flow.StagingTime;
            bool previousAuto = flow.AutoContinue;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 1;
                flow.StagingTime = .02f;
                flow.AutoContinue = false;
                reset.onClick.Invoke();
                Check(waves.CurrentPreset != null && start.interactable && quarterText.text == "분기 1 · 웨이브", "catalog and quarter available at startup");
                Check(!binding.IsVisible && !finish.interactable && !next.interactable, "no unsolicited decision");
                finish.onClick.Invoke();
                next.onClick.Invoke();
                Check(flow.CurPhase == GamePhase.Preparation, "hidden choice inputs do not change core");
                Check(finish.navigation.selectOnRight == next && next.navigation.selectOnLeft == finish, "explicit choice navigation");

                // A missing preset is a real core startup condition, not a UI-created fallback.
                var missingObject = new GameObject("UI Validation Missing Preset");
                try
                {
                    var missingWaves = missingObject.AddComponent<WaveController>();
                    hud.Initialize(ui, flow, missingWaves);
                    Check(!start.interactable, "no preset disables start");
                    start.onClick.Invoke();
                    Check(flow.CurPhase == GamePhase.Preparation, "disabled start cannot begin battle");
                }
                finally
                {
                    hud.Initialize(ui, flow, waves);
                    UnityEngine.Object.DestroyImmediate(missingObject);
                }
                Check(start.interactable, "valid binding restores start");
                try { binding.Initialize(null, waves); Check(false, "null flow must reject"); }
                catch (ArgumentNullException) { Check(true, "invalid initialization rejects before altering binding"); }

                int totalWaveGold = 100;
                for (int quarter = 1; quarter <= WaveController.MAIN_QUARTERS; quarter++)
                {
                    for (int wave = 1; wave <= WaveController.MAX_WAVE; wave++)
                    {
                        Check(waves.CurQuarter == quarter && waves.CurWave == wave && start.interactable,
                            $"quarter {quarter} wave {wave} ready");
                        Check(quarterText.text == $"분기 {quarter} · 웨이브", "quarter label follows core");
                        start.onClick.Invoke();
                        await WaitFor(flow, GamePhase.Battle);
                        win.onClick.Invoke();
                        Check(flow.CurPhase == GamePhase.BattleResolving && phaseText.text == "전투 정산" && !start.interactable,
                            "victory staging is visible and locked");
                        bool mainEnd = quarter == WaveController.MAIN_QUARTERS && wave == WaveController.MAX_WAVE;
                        await WaitFor(flow, GamePhase.Reward);
                        Check(!binding.IsVisible && !start.interactable, "reward is separate from choice");
                        if (wave == WaveController.MAX_WAVE)
                            Check(flow.IsWaitingForArtifactSelection, "quarter reward keeps team artifact wait contract");
                        int before = wallet.GetBalance(CurrencyType.Gold);
                        int expectedReward = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gold);
                        totalWaveGold += expectedReward;
                        reward.onClick.Invoke();
                        reward.onClick.Invoke();
                        Check(wallet.GetBalance(CurrencyType.Gold) == before + expectedReward, "one table reward per quarter/wave");
                        if (mainEnd)
                        {
                            await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.QuarterComplete, phaseText);
                            Check(flow.HasClearedMainGame && binding.IsVisible && flow.CanChooseRunDecision,
                                "main clear waits for user decision after reward");
                            Check(phaseText.text == "분기 완료", "quarter complete label");
                            int balance = wallet.GetBalance(CurrencyType.Gold);
                            reward.onClick.Invoke();
                            Check(wallet.GetBalance(CurrencyType.Gold) == balance && flow.CanChooseRunDecision,
                                "reward input cannot bypass quarter decision");
                            Check(EventSystem.current.currentSelectedGameObject == finish.gameObject, "safe default focus is finish");
                            CheckModalBlocksUi(finish);
                            // Capture the enabled button colors, not the disabled-to-enabled color tween's first frame.
                            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(finish.colors.fadeDuration, next.colors.fadeDuration) + .05f), DelayType.Realtime);
                            Check(finish.targetGraphic.color.a > .95f && next.targetGraphic.color.a > .95f,
                                "enabled choice buttons finish their visual transition");
                            MvpRuntimeHudValidation.Capture(ui, sample, 1280, 720, "-quarter-choice");
                            MvpRuntimeHudValidation.Capture(ui, sample, 1920, 1080, "-quarter-choice");
                            ui.gameObject.SetActive(false);
                            Check(!binding.IsVisible && flow.CanChooseRunDecision, "hidden view does not finish core request");
                            ui.gameObject.SetActive(true);
                            Check(binding.IsVisible && finish.interactable && next.interactable, "reenable restores pending choice");
                            binding.Initialize(flow, waves);
                            binding.Initialize(flow, waves);
                            Check(binding.IsVisible && flow.CanChooseRunDecision, "reinitialize preserves pending core decision");
                            next.onClick.Invoke();
                            finish.onClick.Invoke();
                            next.onClick.Invoke();
                            Check(!flow.CanChooseRunDecision && !binding.IsVisible && !finish.interactable && !next.interactable,
                                "first choice consumes core request and locks both buttons");
                        }
                        await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation, phaseText);
                    }
                }
                Check(waves.CurQuarter == WaveController.MAIN_QUARTERS + 1 && waves.CurWave == 1 && flow.HasClearedMainGame,
                    "continue enters next quarter and retains clear record");
                Check(wallet.GetBalance(CurrencyType.Gold) == totalWaveGold,
                    "all quarter rewards reflected exactly once");
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                lose.onClick.Invoke();
                await WaitFor(flow, GamePhase.Finished);
                Check(flow.HasClearedMainGame && !binding.IsVisible, "endless defeat retains main clear without assuming final victory");
                Check(!Field<GameObject>(uiFields, "_runResultPanel").activeSelf, "no fabricated final result payload");

                // Finish, auto-continue, and resets are checked via the same public core entry points.
                reset.onClick.Invoke();
                Check(!flow.HasClearedMainGame && quarterText.text == "분기 1 · 웨이브", "restart clears run and display");
                lastQuarter.onClick.Invoke();
                lastWave.onClick.Invoke();
                Check(waves.CurQuarter == WaveController.MAIN_QUARTERS && waves.CurWave == WaveController.MAX_WAVE &&
                    wallet.GetBalance(CurrencyType.Gold) == 100, "test shortcuts never grant rewards");
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                win.onClick.Invoke();
                await WaitFor(flow, GamePhase.Reward);
                int finishReward = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gold);
                reward.onClick.Invoke();
                ui.gameObject.SetActive(false);
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.QuarterComplete);
                Check(!binding.IsVisible && flow.CanChooseRunDecision, "choice request survives fully hidden HUD");
                ui.gameObject.SetActive(true);
                Check(binding.IsVisible && finish.interactable, "hidden request is recovered from core state");
                binding.enabled = false;
                finish.onClick.Invoke();
                Check(!binding.IsVisible && flow.CanChooseRunDecision, "disabled binding releases input and does not choose");
                binding.enabled = true;
                Check(binding.IsVisible, "component reenable restores choice");
                finish.onClick.Invoke();
                next.onClick.Invoke();
                await WaitFor(flow, GamePhase.Finished);
                Check(waves.CurQuarter == WaveController.MAIN_QUARTERS && flow.HasClearedMainGame && !binding.IsVisible,
                    "finish stops at cleared quarter; late continue is ignored");
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 + finishReward,
                    "finish path keeps only the authoritative quarter reward");

                reset.onClick.Invoke();
                lastQuarter.onClick.Invoke();
                lastWave.onClick.Invoke();
                flow.AutoContinue = true;
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                win.onClick.Invoke();
                await WaitFor(flow, GamePhase.Reward);
                Check(!binding.IsVisible && !flow.CanChooseRunDecision && flow.IsWaitingForArtifactSelection,
                    "core auto-continue does not open a decision UI");
                reward.onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation, phaseText);
                Check(waves.CurQuarter == WaveController.MAIN_QUARTERS + 1, "auto-continue advances through reward gate");
                flow.AutoContinue = false;

                reset.onClick.Invoke();
                lastQuarter.onClick.Invoke();
                lastWave.onClick.Invoke();
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                win.onClick.Invoke();
                await WaitFor(flow, GamePhase.Reward);
                reward.onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.QuarterComplete, phaseText);
                reset.onClick.Invoke();
                next.onClick.Invoke();
                finish.onClick.Invoke();
                await UniTask.NextFrame();
                Check(flow.CurPhase == GamePhase.Preparation && waves.CurQuarter == 1 && !binding.IsVisible && start.interactable,
                    "reset cancels pending choice; stale buttons cannot affect new run");
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                flow.StagingTime = .2f;
                win.onClick.Invoke();
                reset.onClick.Invoke();
                await UniTask.Delay(TimeSpan.FromSeconds(.3), DelayType.Realtime);
                Check(flow.CurPhase == GamePhase.Preparation && waves.CurWave == 1 && start.interactable && !hud.IsStartPending,
                    "reset during staging prevents stale completion");
                Check(!Field<GameObject>(uiFields, "_messagePanel").activeSelf, "no stale cancellation message");
                Debug.Log($"[UI/MvpCoreQuarterValidation] PASS: {_checks} Play Mode checks against unchanged team core; test spawner/team reward table, no final result API inferred.");
            }
            finally
            {
                flow.StagingTime = previousStaging;
                flow.AutoContinue = previousAuto;
                Time.timeScale = previousScale;
                ui.gameObject.SetActive(true);
                binding.enabled = true;
                reset.onClick.Invoke();
            }
        }

        private static async UniTask WaitFor(GameFlowController flow, GamePhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (flow.CurPhase != phase)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException($"Quarter UI expected {phase}, got {flow.CurPhase}");
                await UniTask.NextFrame();
            }
            await UniTask.NextFrame();
        }

        private static void CheckModalBlocksUi(Button finish)
        {
            Canvas.ForceUpdateCanvases();
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, finish.transform.position)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == finish.gameObject,
                "choice button is the topmost UI hit");
            data.position = new Vector2(2, 2);
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            Check(hits.Count > 0 && hits[0].gameObject.name == "DecisionOverlay", "modal backdrop blocks clicks outside card");
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static void Check(bool condition, string message)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Core quarter UI: " + message);
        }
    }
}
