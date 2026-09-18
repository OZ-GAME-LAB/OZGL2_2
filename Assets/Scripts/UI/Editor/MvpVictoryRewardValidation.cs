using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    /// <summary>명시적인 Play Mode 검사에서만 실행. 원본 데이터와 에셋은 수정하지 않는다.</summary>
    public static class MvpVictoryRewardValidation
    {
        private static int _checks;

        public static async UniTask RunChecksAsync()
        {
            _checks = 0;
            var sample = UnityEngine.Object.FindFirstObjectByType<MvpRuntimeHudSample>();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveController>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<RunCurrencyManager>();
            var artifacts = UnityEngine.Object.FindFirstObjectByType<ArtifactManager>();
            var effects = UnityEngine.Object.FindFirstObjectByType<EffectManager>();
            var binding = UnityEngine.Object.FindFirstObjectByType<ArtifactRewardBinding>();
            var panel = UnityEngine.Object.FindFirstObjectByType<ArtifactRewardPanel>();
            var ui = UnityEngine.Object.FindFirstObjectByType<GameUIController>();
            await WaitUntil(() => sample != null && sample.IsReady, "sample initialization");
            var sampleFields = new SerializedObject(sample);
            var reset = Field<Button>(sampleFields, "_resetButton");
            var win = Field<Button>(sampleFields, "_winButton");
            var reopen = Field<Button>(sampleFields, "_rewardButton");
            var lastWave = Field<Button>(sampleFields, "_lastWaveButton");
            var start = Field<Button>(new SerializedObject(ui), "_waveStartButton");
            var panelFields = new SerializedObject(panel);
            var confirm = Field<Button>(panelFields, "_confirmButton");
            var card = (Button)panelFields.FindProperty("_cards").GetArrayElementAtIndex(0)
                .FindPropertyRelative("Button").objectReferenceValue;
            var rewardText = Field<TMP_Text>(panelFields, "_rewardText");
            var catalog = AssetDatabase.LoadAssetAtPath<ArtifactCatalog>(MvpVictoryRewardSetup.CatalogPath);
            float staging = flow.StagingTime;
            bool auto = flow.AutoContinue;
            int completions = 0;
            int stackEvents = 0;
            ArtifactRewardRequest previousRequest = null;
            Action<string> completed = id => completions++;
            Action<ArtifactRewardRequest> requested = request => previousRequest = request;
            Action<ArtifactInstance, int, int> stackChanged = (instance, before, after) =>
            {
                stackEvents++;
                // 동기 효과/인벤토리 이벤트 도중 우리 UI를 재진입해도 중복 지급/리셋하지 않아야 한다.
                reopen.onClick.Invoke();
                reset.onClick.Invoke();
            };
            binding.Completed += completed;
            panel.ChoiceRequested += requested;
            artifacts.StackChanged += stackChanged;
            try
            {
                flow.StagingTime = .02f;
                flow.AutoContinue = false;
                reset.onClick.Invoke();
                Check(wallet.GetBalance(CurrencyType.Gold) == 100 && artifacts.Instances.Count == 0 &&
                    !panel.IsVisible, "fresh run has the authoritative wallet and empty inventory");
                int expectedGold = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gold);
                int expectedGems = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gem);
                await WinAndWait(start, win, flow, panel);
                var first = binding.CurrentReward;
                Check(first != null && first.Candidates.Count > 0 && flow.CurPhase == GamePhase.Reward,
                    "victory automatically opens real candidate UI without releasing the gate");
                Check(first.AwardedGold == expectedGold && first.AwardedGems == expectedGems &&
                    wallet.GetBalance(CurrencyType.Gold) == 100 + expectedGold &&
                    wallet.GetBalance(CurrencyType.Gem) == expectedGems, "display contains actual paid table rewards");
                Check(rewardText.text.Contains("+" + expectedGold) && !start.interactable,
                    "reward text and core input lock");
                reopen.onClick.Invoke();
                reopen.onClick.Invoke();
                Check(ReferenceEquals(first, binding.CurrentReward) &&
                    wallet.GetBalance(CurrencyType.Gold) == 100 + expectedGold, "reopen neither rerolls nor repays");
                Check(first.Candidates.All(offer => catalog.TryGetById(offer.ArtifactId, out var data) &&
                    data.Rarity != ArtifactRarity.Mythic), "normal wave uses original non-mythic candidates");
                binding.enabled = false;
                reopen.onClick.Invoke();
                Check(!panel.IsVisible && ReferenceEquals(first, binding.CurrentReward) &&
                    wallet.GetBalance(CurrencyType.Gold) == 100 + expectedGold, "disabled binding preserves reward without repayment");
                binding.enabled = true;
                Check(panel.IsVisible && ReferenceEquals(first, binding.CurrentReward), "reenable restores the same candidates");
                card.onClick.Invoke();
                string selected = panel.SelectedArtifactId;
                Check(artifacts.Instances.Count == 0 && stackEvents == 0, "highlighting a card never grants effects");
                confirm.onClick.Invoke();
                confirm.onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                Check(completions == 1 && stackEvents == 1 && artifacts.TryGetById(selected, out var owned) &&
                    owned.StackCount == 1 && waves.CurWave == 2, "one confirmed request creates one real stack");
                artifacts.TryGetById(selected, out var instance);
                int registered = effects.AllyModifiers.Count(m => ReferenceEquals(m.Source, instance)) +
                    effects.EnemyModifiers.Count(m => ReferenceEquals(m.Source, instance)) +
                    effects.CurrencyModifiers.Count(m => ReferenceEquals(m.Source, instance));
                Check(registered == instance.Data.UnitStatEffects.Count + instance.Data.CurrencyEffects.Count,
                    "actual artifact Source owns the converted modifiers");
                Check(!panel.IsVisible && !binding.IsChoosing && !panel.IsRequestPending,
                    "successful selection closes and resolves the panel");

                await WinAndWait(start, win, flow, panel);
                Check(binding.CurrentReward.RewardId != first.RewardId, "different wave has a different reward ID");
                confirm.onClick.Invoke(); // 기본 미선택 = 모두 포기.
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                Check(completions == 2 && artifacts.Instances.Count == 1 && stackEvents == 1,
                    "forfeit changes no inventory or artifact effects");

                // 재화 효과는 다음 거래부터 적용된다. 원본 003은 웨이브 골드 +20인 테스트 데이터다.
                reset.onClick.Invoke();
                Check(catalog.TryGetById("artifact_003", out var goldArtifact), "team Gold effect fixture");
                artifacts.StackChanged -= stackChanged;
                Check(artifacts.TryAdd(goldArtifact), "register original Gold modifier");
                int baseGold = MvpEconomyUiValidation.GetExpectedReward(waves, CurrencyType.Gold);
                await WinAndWait(start, win, flow, panel);
                Check(binding.CurrentReward.AwardedGold == baseGold + 20 &&
                    wallet.GetBalance(CurrencyType.Gold) == 100 + baseGold + 20, "real next payout consumes registered currency effects");
                string abandonedId = binding.CurrentReward.RewardId;
                reset.onClick.Invoke();
                Check(!panel.IsVisible && !binding.IsChoosing && artifacts.Instances.Count == 0 &&
                    effects.CurrencyModifiers.Count == 0 && wallet.GetBalance(CurrencyType.Gold) == 100,
                    "reset removes artifact-owned effects, request and old wallet");
                Check(previousRequest != null && !panel.TryResolveRequest(previousRequest.RequestId, true),
                    "late old request cannot resolve a new run");

                // 보상창이 열린 이후 다른 처리가 최대 중첩에 도달한 경우: 후보 유지, 실패 후 포기 가능.
                await WinAndWait(start, win, flow, panel);
                Check(binding.CurrentReward.RewardId != abandonedId, "reset changes run identity even at the same wave");
                card.onClick.Invoke();
                catalog.TryGetById(panel.SelectedArtifactId, out var capped);
                for (int i = 0; i < capped.MaxStacks; i++) Check(artifacts.TryAdd(capped), "fill runtime stack");
                var sameCandidates = binding.CurrentReward;
                int beforeRetryGold = wallet.GetBalance(CurrencyType.Gold);
                confirm.onClick.Invoke();
                Check(panel.IsVisible && !panel.IsRequestPending && binding.IsChoosing &&
                    flow.CurPhase == GamePhase.Reward, "failed grant leaves the choice open");
                reopen.onClick.Invoke();
                Check(ReferenceEquals(sameCandidates, binding.CurrentReward) &&
                    wallet.GetBalance(CurrencyType.Gold) == beforeRetryGold, "failed grant retry does not reroll or repay");
                Field<Button>(panelFields, "_clearButton").onClick.Invoke();
                confirm.onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);

                // 다른 Source는 리셋이 지우면 안 된다.
                var foreignSource = new object();
                effects.RegisterEffects(foreignSource, new ConvertedEffects(null, null,
                    new System.Collections.Generic.List<CurrencyModifier>
                    { new CurrencyModifier(foreignSource, CurrencyType.Gold, CurrencyRewardType.Production, CurrencyModifierType.Flat, 3) }));
                reset.onClick.Invoke();
                Check(effects.CurrencyModifiers.Count == 1 && ReferenceEquals(effects.CurrencyModifiers[0].Source, foreignSource),
                    "reset preserves effects from other systems");
                effects.RemoveEffects(foreignSource);

                // 보스 후보가 모두 최대 중첩이면 0개 성공이다. 빈 UI를 열거나 영원히 기다리지 않는다.
                foreach (var mythic in catalog.GetByRarity(ArtifactRarity.Mythic))
                    for (int i = 0; i < mythic.MaxStacks; i++) Check(artifacts.TryAdd(mythic), "fill boss candidate stack");
                lastWave.onClick.Invoke();
                start.onClick.Invoke();
                await WaitUntil(() => flow.CurPhase == GamePhase.Battle, "boss battle");
                win.onClick.Invoke();
                await MvpRuntimeHudValidation.WaitForPhaseAfterContentAsync(flow, GamePhase.Preparation);
                Check(waves.CurQuarter == 2, "empty candidates release boss gate");
                Check(!panel.IsVisible && !binding.IsChoosing, "empty eligible pool completes without an empty panel");

                reset.onClick.Invoke();
                start.onClick.Invoke();
                await WaitUntil(() => flow.CurPhase == GamePhase.Battle, "defeat battle");
                Field<Button>(sampleFields, "_loseButton").onClick.Invoke();
                await WaitUntil(() => flow.CurPhase == GamePhase.Finished, "defeat finishes");
                Check(!panel.IsVisible && wallet.GetBalance(CurrencyType.Gold) == 100,
                    "defeat never opens victory rewards");
                Debug.Log("[UI/MvpVictoryRewardValidation] PASS: " + _checks + " real Play Mode checks.");
            }
            finally
            {
                binding.Completed -= completed;
                panel.ChoiceRequested -= requested;
                artifacts.StackChanged -= stackChanged;
                flow.StagingTime = staging;
                flow.AutoContinue = auto;
            }
        }

        private static async UniTask WinAndWait(Button start, Button win, GameFlowController flow, ArtifactRewardPanel panel)
        {
            start.onClick.Invoke();
            await WaitUntil(() => flow.CurPhase == GamePhase.Battle, "battle");
            win.onClick.Invoke();
            await WaitUntil(() => panel.IsVisible, "automatic victory UI");
        }

        private static async UniTask WaitUntil(Func<bool> condition, string description)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(description);
                await UniTask.NextFrame();
            }
            await UniTask.NextFrame();
        }

        private static T Field<T>(SerializedObject fields, string name) where T : UnityEngine.Object =>
            (T)fields.FindProperty(name).objectReferenceValue;

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("Victory rewards: " + description);
        }
    }
}
