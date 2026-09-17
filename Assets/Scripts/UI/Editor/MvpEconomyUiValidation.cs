using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>기존 런타임 검사에서 호출한다. 원본 재화/보상 에셋을 수정하지 않는다.</summary>
    public static class MvpEconomyUiValidation
    {
        private static int _checks;

        internal static int GetExpectedReward(WaveController waves, CurrencyType type)
        {
            var table = MvpEconomyUiSetup.LoadRewardTable();
            int quarter = Mathf.Min(waves.CurQuarter, WaveController.MAIN_QUARTERS);
            if (!table.TryGetRewards(quarter, waves.CurWave, out var rewards))
                throw new InvalidOperationException("No authoritative reward fixture for the current wave.");
            // 현재 팀원 매니저의 베이스캠프 레벨은 1이다. 강화 효과 연결 시 검증 계약도 갱신한다.
            foreach (var reward in rewards)
                if (reward.Currency.Type == type) return reward.Amount;
            return 0;
        }

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
            var ui = UnityEngine.Object.FindFirstObjectByType<GameUIController>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveController>();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            var sampleFields = new SerializedObject(sample);
            var reset = Field<Button>(sampleFields, "_resetButton");
            var reward = Field<Button>(sampleFields, "_rewardButton");
            var win = Field<Button>(sampleFields, "_winButton");
            var lastWave = Field<Button>(sampleFields, "_lastWaveButton");
            var lastQuarter = Field<Button>(sampleFields, "_lastQuarterButton");
            var uiFields = new SerializedObject(ui);
            var goldText = Field<TMP_Text>(uiFields, "_goldText");
            var start = Field<Button>(uiFields, "_waveStartButton");
            float previousStaging = flow.StagingTime;
            bool previousAuto = flow.AutoContinue;
            int events = 0;
            int reentries = 0;
            bool reenter = false;
            bool atomicSnapshot = true;
            int expectedGold = 0;
            int expectedGem = 0;
            Action<CurrencyData, int, int> changed = (currency, before, after) =>
            {
                events++;
                if (!reenter) return;
                reentries++;
                atomicSnapshot &= wallet.GetBalance(CurrencyType.Gold) == expectedGold &&
                    wallet.GetBalance(CurrencyType.Gem) == expectedGem;
                // 실제 잔액 이벤트 안에서 UI 재요청/리셋을 시도한다.
                reward.onClick.Invoke();
                reset.onClick.Invoke();
            };
            wallet.BalanceChanged += changed;
            try
            {
                flow.StagingTime = .02f;
                flow.AutoContinue = false;
                reset.onClick.Invoke();
                Check(sample.IsReady && wallet.GetBalance(CurrencyType.Gold) == 100 &&
                    goldText.text == "100" && events == 0, "Initialize(WaveController) and explicit HUD refresh");
                var balances = wallet.Balances;
                wallet.Initialize(waves, null, null);
                Check(ReferenceEquals(balances, wallet.Balances) && events == 0,
                    "duplicate initialization preserves the wallet and does not publish changes");
                Check(wallet.CanSpend(CurrencyType.Gold, 30) && !wallet.CanSpend(CurrencyType.Gold, 101) &&
                    wallet.GetBalance(CurrencyType.Gold) == 100 && events == 0, "CanSpend is read-only");
                Check(!wallet.TrySpend(CurrencyType.Gold, 101) && events == 0 &&
                    goldText.text == "100", "insufficient spend does not change the HUD");
                Check(wallet.TryApplyProductionReward(CurrencyType.Gold, 20) && goldText.text == "120",
                    "production reward uses the authoritative wallet event");
                // 적 드랍 API는 팀에서 제거했다. 일반 잔액 갱신만 검증하며 드랍 연동 완료로 보고하지 않는다.
                Check(wallet.TryAdd(CurrencyType.Gold, 5) && goldText.text == "125",
                    "explicit test Gold addition updates the HUD (not an enemy drop)");
                Check(wallet.TryAdd(CurrencyType.Gem, 2) &&
                    wallet.GetBalance(CurrencyType.Gem) == 2 && goldText.text == "125",
                    "explicit test Gem addition never replaces the Gold HUD");
                Check(events == 3, "exactly one event per nonzero transaction");
                reward.onClick.Invoke();
                Check(events == 3 && wallet.GetBalance(CurrencyType.Gold) == 125 &&
                    flow.CurPhase == GamePhase.Preparation, "reward input outside Reward has no effect");

                reset.onClick.Invoke();
                lastWave.onClick.Invoke();
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                win.onClick.Invoke();
                await WaitFor(flow, GamePhase.Reward);
                int goldReward = GetExpectedReward(waves, CurrencyType.Gold);
                int gemReward = GetExpectedReward(waves, CurrencyType.Gem);
                Check(goldReward > 0 && gemReward > 0, "quarter-end fixture includes both currencies");

                // 보석만 오버플로 시켜 앞 순서의 골드도 부분 지급되지 않는지 확인한다.
                Check(wallet.TryAdd(CurrencyType.Gem, int.MaxValue), "prepare Gem overflow boundary");
                int eventsBeforeFailure = events;
                reward.onClick.Invoke();
                reward.onClick.Invoke();
                await UniTask.NextFrame();
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 &&
                    wallet.GetBalance(CurrencyType.Gem) == int.MaxValue && events == eventsBeforeFailure,
                    "failed multi-currency reward leaves every balance and event unchanged");
                Check(flow.CurPhase == GamePhase.Reward && !start.interactable,
                    "failed reward does not release the core reward gate");
                Check(wallet.TrySpend(CurrencyType.Gem, int.MaxValue), "remove only the test overflow balance");
                expectedGold = 100 + goldReward;
                expectedGem = gemReward;
                int eventsBeforeSuccess = events;
                reenter = true;
                reward.onClick.Invoke();
                reenter = false;
                reward.onClick.Invoke();
                Check(atomicSnapshot && reentries == 2 && events == eventsBeforeSuccess + 2,
                    "synchronous reentrant reward/reset requests do not repeat or interrupt the transaction");
                Check(wallet.GetBalance(CurrencyType.Gold) == expectedGold &&
                    wallet.GetBalance(CurrencyType.Gem) == expectedGem &&
                    goldText.text == expectedGold.ToString("N0"), "retry applies exactly the team Gold and Gem rewards");
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                Check(waves.CurQuarter == 2 && waves.CurWave == 1, "successful reward releases the gate once");

                reset.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 && wallet.GetBalance(CurrencyType.Gem) == 0,
                    "restart removes prior wave rewards");
                lastQuarter.onClick.Invoke();
                lastWave.onClick.Invoke();
                flow.AutoContinue = true;
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                win.onClick.Invoke();
                await WaitFor(flow, GamePhase.Reward);
                reward.onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                Check(waves.CurQuarter == WaveController.MAIN_QUARTERS + 1, "reach endless through public core actions");
                int endlessBefore = wallet.GetBalance(CurrencyType.Gold);
                int endlessReward = GetExpectedReward(waves, CurrencyType.Gold);
                start.onClick.Invoke();
                await WaitFor(flow, GamePhase.Battle);
                win.onClick.Invoke();
                await WaitFor(flow, GamePhase.Reward);
                reward.onClick.Invoke();
                reward.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == endlessBefore + endlessReward,
                    "endless reward follows the team's clamped quarter table, once only");
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                Debug.Log($"[UI/MvpEconomyUiValidation] PASS: {_checks} Play Mode checks against the team Economy API and table.");
            }
            finally
            {
                reenter = false;
                wallet.BalanceChanged -= changed;
                flow.StagingTime = previousStaging;
                flow.AutoContinue = previousAuto;
                reset.onClick.Invoke();
            }
        }

        private static async UniTask WaitFor(GameFlowController flow, GamePhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (flow.CurPhase != phase)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"Economy UI expected {phase}, got {flow.CurPhase}");
                await UniTask.NextFrame();
            }
            await UniTask.NextFrame();
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Economy UI: " + description);
        }
    }
}
