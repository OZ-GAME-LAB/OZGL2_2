using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 실제 플레이어 저장소 대신 .utmp 아래 별도 폴더에서 정산과 아웃게임 재로드를 검사합니다.
public static class RunSettlementValidation
{
    public static string LastResult { get; private set; }
    private static readonly List<string> Checks = new List<string>();
    private static bool _running;

    [MenuItem("Tools/Economy/Validate Run Settlement")]
    public static void Run() => ValidateAsync().Forget();

    public static async UniTask ValidateAsync()
    {
        if (_running) throw new InvalidOperationException("정산 검증이 이미 진행 중입니다.");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 후 실행해주세요.");
        _running = true;
        Checks.Clear();
        string directory = Path.GetFullPath(".utmp/RunSettlementValidation/Session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await ValidateAmounts();
            await ValidateFailuresAndRetry();
            await ValidatePendingAndCancellation();
            await ValidatePersistentProfile(directory);
            LastResult = "PASS (" + Checks.Count + ")\n" + string.Join("\n", Checks);
            Debug.Log("[RunSettlementValidation] " + LastResult);
        }
        catch (Exception exception)
        {
            LastResult = "FAIL after " + Checks.Count + " checks: " + exception;
            throw;
        }
        finally
        {
            File.WriteAllText(Path.Combine(directory, "report.txt"), LastResult ?? "No result");
            _running = false;
        }
    }

    private static async UniTask ValidateAmounts()
    {
        using (var fixture = new Fixture())
        {
            RunSummary summary = Summary(5, 1);
            fixture.UI.OnShow = () => Check(fixture.Wallet.Balance == 110 &&
                fixture.Writer.Saved.Wallet.Amount == 110, "file and wallet commit before showing a result");
            Check(await fixture.Manager.TryApplyReward(summary), "base reward confirms successfully");
            Check(ReferenceEquals(fixture.UI.Summary, summary) && fixture.UI.Bloodstones == 10,
                "UI receives the original RunSummary and the exact base payout");
            Check(fixture.Writer.SaveCalls == 1 && fixture.Wallet.Balance == 110,
                "no totem still pays the complete base reward");
        }

        using (var fixture = new Fixture())
        {
            fixture.SetTotemPercent(25);
            Check(await fixture.Manager.TryApplyReward(Summary(7, 1)), "totem reward confirms successfully");
            Check(fixture.UI.Bloodstones == 17 && fixture.Wallet.Balance == 117,
                "14 base plus floor(14 * 25%) pays 17, including the base reward");
        }

        using (var fixture = new Fixture())
        {
            Check(await fixture.Manager.TryApplyReward(Summary(0, 0)) && fixture.UI.Bloodstones == 0 &&
                fixture.Wallet.Balance == 100, "a first-wave defeat can display and confirm zero reward");
        }

        using (var fixture = new Fixture())
        {
            Check(!await fixture.Manager.TryApplyReward(Summary(1, 2)), "invalid clear counts are rejected");
            Check(!await fixture.Manager.TryApplyReward(Summary(int.MaxValue, 1)), "base reward overflow is rejected");
            fixture.SetTotemPercent(100);
            Check(!await fixture.Manager.TryApplyReward(Summary(int.MaxValue, 0)), "base plus totem overflow is rejected");
            fixture.Wallet.RestoreSaveData(new PersistentWalletSaveData(int.MaxValue));
            Check(!await fixture.Manager.TryApplyReward(Summary(1, 0)), "wallet overflow is rejected");
            Check(fixture.Writer.SaveCalls == 0 && fixture.UI.ShowCalls == 0,
                "invalid or overflowing rewards never write or display success");
        }
    }

    private static async UniTask ValidateFailuresAndRetry()
    {
        using (var fixture = new Fixture())
        {
            RunSummary summary = Summary(10, 1);
            fixture.Writer.FailSave = true;
            Check(!await fixture.Manager.TryApplyReward(summary), "a failed save returns false");
            Check(fixture.Wallet.Balance == 100 && fixture.Writer.Saved.Wallet.Amount == 100 && fixture.UI.ShowCalls == 0,
                "failed saving changes neither wallet nor stored profile and does not show a paid result");
            fixture.Writer.FailSave = false;
            Check(await fixture.Manager.TryApplyReward(summary) && fixture.Wallet.Balance == 120,
                "the same summary can retry after a save failure");
            Check(await fixture.Manager.TryApplyReward(summary) && fixture.Writer.SaveCalls == 2 && fixture.Wallet.Balance == 120,
                "a successfully saved summary is not paid again");
        }

        using (var fixture = new Fixture())
        {
            RunSummary summary = Summary(5, 0);
            fixture.UI.ThrowOnShow = true;
            Check(!await fixture.Manager.TryApplyReward(summary) && fixture.Wallet.Balance == 105,
                "a UI error returns false after preserving the committed payout");
            fixture.UI.ThrowOnShow = false;
            Check(await fixture.Manager.TryApplyReward(summary) && fixture.Writer.SaveCalls == 1,
                "retrying a failed UI does not repeat the saved payout");
        }

        using (var fixture = new Fixture())
        {
            fixture.Manager.Initialize(null, null, fixture.Wallet, null, fixture.UI, null);
            Check(!await fixture.Manager.TryApplyReward(Summary(1, 0)), "a missing writer prevents payout");
            fixture.Manager.Initialize(null, null, fixture.Wallet, null, null, fixture.Writer);
            Check(!await fixture.Manager.TryApplyReward(Summary(1, 0)), "a missing UI prevents payout");
            Check(fixture.Wallet.Balance == 100 && fixture.Writer.SaveCalls == 0,
                "missing production dependencies do not silently grant a reward");
        }
    }

    private static async UniTask ValidatePendingAndCancellation()
    {
        using (var fixture = new Fixture())
        {
            fixture.UI.AutoConfirm = false;
            RunSummary summary = Summary(5, 1);
            UniTask<bool> pending = fixture.Manager.TryApplyReward(summary);
            Check(pending.Status == UniTaskStatus.Pending && fixture.Wallet.Balance == 110,
                "settlement awaits the user's confirmation after committing once");
            Check(!await fixture.Manager.TryApplyReward(summary) && !await fixture.Manager.TryApplyReward(Summary(6, 1)),
                "both same and different requests are rejected while confirmation is pending");
            ExpectInvalidOperation(() => fixture.Manager.Initialize(null, null, fixture.Wallet, null, fixture.UI, fixture.Writer),
                "Initialize cannot replace a pending request");
            fixture.UI.Confirm();
            fixture.UI.Confirm();
            Check(await pending && fixture.Writer.SaveCalls == 1 && fixture.UI.ShowCalls == 1,
                "repeated confirmation does not duplicate the reward or request");

            fixture.UI.AutoConfirm = true;
            fixture.Manager.Initialize(null, null, fixture.Wallet, null, fixture.UI, fixture.Writer);
            Check(await fixture.Manager.TryApplyReward(summary) && fixture.Writer.SaveCalls == 1,
                "reinitializing the same dependencies retains committed summaries");
            ExpectInvalidOperation(() => fixture.Manager.Initialize(null, null, fixture.Wallet, null, fixture.UI,
                new FakeWriter(fixture.Wallet)), "a different save destination cannot inherit committed summaries");
        }

        using (var fixture = new Fixture())
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await ExpectCanceled(fixture.Manager.TryApplyReward(Summary(5, 1), cancellation.Token));
                Check(fixture.Writer.SaveCalls == 0 && fixture.UI.ShowCalls == 0,
                    "a token canceled before settlement makes no changes");
            }

            fixture.UI.AutoConfirm = false;
            RunSummary summary = Summary(5, 1);
            using (var cancellation = new CancellationTokenSource())
            {
                UniTask<bool> pending = fixture.Manager.TryApplyReward(summary, cancellation.Token);
                cancellation.Cancel();
                await ExpectCanceled(pending);
            }
            Check(fixture.Wallet.Balance == 110 && fixture.Writer.SaveCalls == 1,
                "canceling the result screen does not roll back an already saved payout");
            fixture.SetTotemPercent(100);
            fixture.UI.AutoConfirm = true;
            Check(await fixture.Manager.TryApplyReward(summary) && fixture.UI.Bloodstones == 10 && fixture.Writer.SaveCalls == 1,
                "a canceled result reopens with its saved amount even if current modifiers change");
            Check(await fixture.Manager.TryApplyReward(Summary(1, 0)) && fixture.Wallet.Balance == 112,
                "a different run summary receives its own reward");
            Check(await fixture.Manager.TryApplyReward(summary) && fixture.Writer.SaveCalls == 2 && fixture.Wallet.Balance == 112,
                "returning to an earlier committed summary still does not repay it");
        }
    }

    private static async UniTask ValidatePersistentProfile(string directory)
    {
        using (var fixture = new Fixture())
        {
            var saveManager = fixture.Root.AddComponent<SaveManager>();
            var coordinator = fixture.Root.AddComponent<PersistentSaveCoordinator>();
            var traits = fixture.Root.AddComponent<OutGameTraitController>();
            Set(saveManager, "_directory", string.Empty);
            Check(saveManager.DirectoryPath == Path.Combine(Application.persistentDataPath, "Saves"),
                "an empty directory after domain reload falls back to the default path without accessing it");
            saveManager.ConfigureDirectory(directory);
            Check(saveManager.DirectoryPath == Path.GetFullPath(directory),
                "an explicitly configured isolated directory takes precedence over the default path");
            var traitAssets = new List<TraitData>();
            foreach (string guid in AssetDatabase.FindAssets("t:TraitData", new[] { "Assets/Data/OutGame/Trait" }))
                traitAssets.Add(AssetDatabase.LoadAssetAtPath<TraitData>(AssetDatabase.GUIDToAssetPath(guid)));
            Check(traitAssets.Count > 0, "existing trait assets are available for the complete-profile check");
            Set(traits, "_traitDatas", traitAssets);
            traits.Initialize(fixture.Wallet, coordinator);
            coordinator.Initialize(saveManager, traits, fixture.Wallet);
            Check(coordinator.TryLoadOrCreate(out string error), "first startup creates the complete profile: " + error);

            PersistentSaveData seed = coordinator.CaptureSaveData();
            foreach (TraitLevelEntry entry in seed.Traits._levels)
                if (entry.Id == TraitId.StartingGold) entry.Level = 2;
            Check(coordinator.TrySave(seed, out error) && coordinator.TryLoad(out error),
                "a preexisting nonzero trait profile is loaded: " + error);
            string originalTraits = JsonUtility.ToJson(traits.CaptureSaveData());
            fixture.Manager.Initialize(null, null, fixture.Wallet, null, fixture.UI, coordinator);
            fixture.UI.OnShow = () =>
            {
                Check(saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData persisted, out string readError) &&
                    persisted.Wallet.Amount == fixture.Wallet.Balance, "result display follows an actual file commit: " + readError);
            };
            Check(await fixture.Manager.TryApplyReward(Summary(6, 1)), "a settlement writes through the real coordinator");
            Check(saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData saved, out error) &&
                saved.Wallet.Amount == 112 && JsonUtility.ToJson(saved.Traits) == originalTraits,
                "the saved profile changes only bloodstones and preserves every trait entry: " + error);

            // 메인 씬의 OutGameBootstrap과 같은 초기화/로드 순서로 복귀 후 잔액을 검사합니다.
            fixture.Wallet.Initialize();
            traits.Initialize(fixture.Wallet, coordinator);
            coordinator.Initialize(saveManager, traits, fixture.Wallet);
            Check(coordinator.TryLoadOrCreate(out error) && fixture.Wallet.Balance == 112 &&
                JsonUtility.ToJson(traits.CaptureSaveData()) == originalTraits,
                "main-menu initialization reloads the paid bloodstones and unchanged traits: " + error);

            string profilePath = coordinator.SavePath;
            string originalFile = File.ReadAllText(profilePath);
            string blockedDirectory = Path.Combine(directory, "blocked-directory");
            File.WriteAllText(blockedDirectory, "This is a file, not a directory.");
            saveManager.ConfigureDirectory(blockedDirectory);
            int shown = fixture.UI.ShowCalls;
            RunSummary retry = Summary(1, 0);
            Check(!await fixture.Manager.TryApplyReward(retry) && fixture.Wallet.Balance == 112 && fixture.UI.ShowCalls == shown,
                "an actual file-write failure keeps the wallet and UI unchanged");
            Check(File.ReadAllText(profilePath) == originalFile, "a failed write preserves the original complete profile");
            saveManager.ConfigureDirectory(directory);
            Check(await fixture.Manager.TryApplyReward(retry) && fixture.Wallet.Balance == 113,
                "retry after the write problem is fixed grants the reward exactly once");

            // 기존 파일이 손상되면 최초 실행용 기본 프로필로 바꾸지 않아야 합니다.
            byte[] corruptFile = { 0x7b, 0x22, 0x54, 0x72 };
            File.WriteAllBytes(profilePath, corruptFile);
            shown = fixture.UI.ShowCalls;
            Check(!coordinator.TryLoadOrCreate(out error) && !coordinator.IsReady,
                "a corrupt existing profile is rejected rather than recreated");
            Check(Convert.ToBase64String(File.ReadAllBytes(profilePath)) == Convert.ToBase64String(corruptFile),
                "startup rejection leaves every byte of the corrupt profile untouched");
            Check(!await fixture.Manager.TryApplyReward(Summary(2, 0)) && fixture.Wallet.Balance == 113 &&
                fixture.UI.ShowCalls == shown && JsonUtility.ToJson(traits.CaptureSaveData()) == originalTraits,
                "settlement cannot pay or display success after profile loading fails");
            Check(Convert.ToBase64String(File.ReadAllBytes(profilePath)) == Convert.ToBase64String(corruptFile),
                "rejected settlement also preserves the corrupt profile for recovery");
        }
    }

    private static RunSummary Summary(int waves, int bosses) => new RunSummary(new RunStats
    {
        ClearWaveCount = waves,
        ClearBossCount = bosses,
        MaxQuarter = waves > 0 ? 1 : 0,
        MaxWave = waves,
        EnemyKillCount = 12,
        Artifacts = new Dictionary<ArtifactRarity, int>()
    });

    private static async UniTask ExpectCanceled(UniTask<bool> task)
    {
        try { await task; }
        catch (OperationCanceledException) { Check(true, "cancellation remains cancellation rather than false or success"); return; }
        throw new InvalidOperationException("A canceled settlement did not propagate cancellation.");
    }

    private static void ExpectInvalidOperation(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { Check(true, message); return; }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Checks.Add(message);
    }

    private static void Set(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly PersistentCurrencyManager Wallet;
        public readonly RunSettlementManager Manager;
        public readonly FakeUI UI = new FakeUI();
        public readonly FakeWriter Writer;
        private readonly CurrencyData _bloodstone;
        private readonly CurrencyCatalog _catalog;
        private TotemRunApplier _totems;

        public Fixture()
        {
            Root = new GameObject("RunSettlementValidation") { hideFlags = HideFlags.HideAndDontSave };
            _bloodstone = ScriptableObject.CreateInstance<CurrencyData>();
            Set(_bloodstone, "_id", "validation-bloodstone");
            Set(_bloodstone, "_type", CurrencyType.Bloodstone);
            Set(_bloodstone, "_lifetime", CurrencyLifetime.Persistent);
            _catalog = ScriptableObject.CreateInstance<CurrencyCatalog>();
            Set(_catalog, "_currencies", new List<CurrencyData> { _bloodstone });
            typeof(CurrencyCatalog).GetMethod("RebuildDicionary", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_catalog, null);
            Wallet = Root.AddComponent<PersistentCurrencyManager>();
            Set(Wallet, "_initialBloodstone", 100);
            Set(Wallet, "_bloodstone", _bloodstone);
            Wallet.Initialize();
            Writer = new FakeWriter(Wallet);
            Manager = Root.AddComponent<RunSettlementManager>();
            Set(Manager, "_currencyCatalog", _catalog);
            Manager.Initialize(null, null, Wallet, null, UI, Writer);
        }

        public void SetTotemPercent(int percent)
        {
            if (_totems == null) _totems = Root.AddComponent<TotemRunApplier>();
            Set(_totems, "_rewardBonusPercent", percent);
            Manager.Initialize(null, null, Wallet, _totems, UI, Writer);
        }

        public void Dispose()
        {
            UI.Cancel();
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_bloodstone);
        }
    }

    private sealed class FakeUI : IRunSettlementUI
    {
        public bool AutoConfirm = true;
        public bool ThrowOnShow;
        public int ShowCalls;
        public RunSummary Summary;
        public int Bloodstones;
        public Action OnShow;
        private UniTaskCompletionSource _pending;

        public async UniTask ShowAndWaitAsync(RunSummary summary, int bloodstones, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ShowCalls++;
            Summary = summary;
            Bloodstones = bloodstones;
            OnShow?.Invoke();
            if (ThrowOnShow) throw new InvalidOperationException("Expected validation UI failure.");
            if (AutoConfirm) return;
            var completion = new UniTaskCompletionSource();
            _pending = completion;
            try
            {
                using (token.Register(() => completion.TrySetCanceled(token)))
                    await completion.Task;
            }
            finally
            {
                if (ReferenceEquals(_pending, completion)) _pending = null;
            }
        }

        public void Confirm() => _pending?.TrySetResult();
        public void Cancel() => _pending?.TrySetCanceled();
    }

    private sealed class FakeWriter : IPersistentSaveWriter
    {
        private readonly PersistentCurrencyManager _wallet;
        public bool IsReady => true;
        public bool FailSave;
        public int SaveCalls;
        public PersistentSaveData Saved;

        public FakeWriter(PersistentCurrencyManager wallet)
        {
            _wallet = wallet;
            Saved = new PersistentSaveData
            {
                Traits = new TraitSaveData(new List<TraitLevelEntry>
                {
                    new TraitLevelEntry { Id = TraitId.StartingGold, Level = 2 }
                }),
                Wallet = wallet.CaptureSaveData()
            };
        }

        public PersistentSaveData CaptureSaveData() => new PersistentSaveData
        {
            Traits = new TraitSaveData(Saved.Traits._levels),
            Wallet = _wallet.CaptureSaveData()
        };

        public bool TrySave(PersistentSaveData data, out string error)
        {
            SaveCalls++;
            error = FailSave ? "Expected validation write failure." : null;
            if (FailSave) return false;
            Saved = data;
            return true;
        }
    }
}
