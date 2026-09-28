using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 매번 별도의 .utmp 폴더를 사용합니다. 실제 플레이어 저장 파일과 기존 씬은 변경하지 않습니다.
public static class PersistentSaveValidation
{
    private static GameObject _root;
    private static OutGameTraitController _traits;
    private static OutGameTestWallet _wallet;
    private static SaveManager _saveManager;
    private static PersistentSaveCoordinator _coordinator;
    private static string _directory;
    private static int _assertions;
    private static readonly List<string> Report = new List<string>();

    [MenuItem("Tools/OutGame/Validate Persistent Save")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 후 실행해주세요.");
        _assertions = 0;
        Report.Clear();
        _directory = Path.GetFullPath(".utmp/PersistentSaveValidation/Saves-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Report.Add("Isolated directory: " + _directory);
        try
        {
            CreateFixture();
            ValidateTraitProvider();
            ValidateStartupAndPurchase();
            ValidateSaveWithoutApply();
            ValidateWriteFailure();
            ValidateFileLockRetry();
            ValidateInvalidFilesAndRecovery();
            Report.Add("PASS: " + _assertions + " assertions");
            Debug.Log("PERSISTENT_SAVE_VALIDATED\n" + string.Join("\n", Report));
        }
        catch (Exception exception)
        {
            Report.Add("FAIL: " + exception);
            throw;
        }
        finally
        {
            File.WriteAllLines(".utmp/PersistentSaveValidation/report.txt", Report);
            if (_root != null) Object.DestroyImmediate(_root);
        }
    }

    private static void CreateFixture()
    {
        _root = new GameObject("PersistentSaveValidation") { hideFlags = HideFlags.HideAndDontSave };
        _wallet = _root.AddComponent<OutGameTestWallet>();
        _traits = _root.AddComponent<OutGameTraitController>();
        _saveManager = _root.AddComponent<SaveManager>();
        _coordinator = _root.AddComponent<PersistentSaveCoordinator>();
        _saveManager.ConfigureDirectory(_directory);

        string[] guids = AssetDatabase.FindAssets("t:TraitData", new[] { "Assets/Data/OutGame/Trait" });
        Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
        SerializedObject serialized = new SerializedObject(_traits);
        SerializedProperty list = serialized.FindProperty("_traitDatas");
        list.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TraitData>(AssetDatabase.GUIDToAssetPath(guids[i]));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Initialize();
        Require(_traits.Data.Count == 9, "Nine existing TraitData assets loaded");
    }

    private static void Initialize()
    {
        _wallet.Initialize();
        _traits.Initialize(_wallet, _coordinator);
        _coordinator.Initialize(_saveManager, _traits, _wallet);
    }

    private static void ValidateTraitProvider()
    {
        string error;
        TraitSaveData captured = _traits.CaptureSaveData();
        Require(captured.Version == 1 && captured._levels.Count == 9, "Capture includes section version and all nine traits");
        captured._levels[0].Level = 4;
        Require(_traits.GetLevel(captured._levels[0].Id) == 0, "Capture entries are detached");
        captured._levels.Clear();
        Require(_traits.CaptureSaveData()._levels.Count == 9, "Capture list is detached");

        List<TraitLevelEntry> source = new List<TraitLevelEntry>
        {
            Entry(TraitId.Production, 1), Entry(TraitId.StartingGold, 2)
        };
        TraitSaveData reordered = new TraitSaveData(source);
        source[0].Level = 3;
        Require(reordered._levels[0].Level == 1, "DTO constructor deep-copies entries");
        Require(_traits.TryValidateSaveData(reordered, out error), "Reordered sparse save is valid: " + error);
        int events = 0;
        Action changed = () => events++;
        _traits.Changed += changed;
        try
        {
            _traits.RestoreSaveData(reordered, false);
            Require(events == 0, "Silent restore emits no change event");
            Require(_traits.GetLevel(TraitId.StartingGold) == 2 && _traits.GetLevel(TraitId.Production) == 1,
                "Restore uses ID instead of list index");
            Require(_traits.GetLevel(TraitId.AttackPower) == 0 && _traits.CaptureSaveData()._levels.Count == 9,
                "Missing trait IDs initialized to zero");
            reordered._levels[0].Level = 4;
            Require(_traits.GetLevel(TraitId.Production) == 1, "Restore copies entry values");
            _traits.RestoreSaveData(Data(Entry(TraitId.StartingGold, 0)));
            Require(events == 1 && _traits.GetLevel(TraitId.StartingGold) == 0,
                "Repeated restore replaces old state and emits once");

            TraitSaveData[] invalid =
            {
                null,
                new TraitSaveData { Version = 1, _levels = null },
                new TraitSaveData { Version = 1, _levels = new List<TraitLevelEntry> { null } },
                Data(Entry(TraitId.None, 0)),
                Data(Entry((TraitId)19999, 0)),
                Data(Entry(TraitId.StartingGold, 0), Entry(TraitId.StartingGold, 1)),
                Data(Entry(TraitId.StartingGold, -1)),
                Data(Entry(TraitId.StartingGold, 6)),
                Data(Entry(TraitId.Production, 1)),
                Data(Entry(TraitId.StartingGold, 1), Entry(TraitId.KillGold, 1)),
                new TraitSaveData { Version = 0, _levels = new List<TraitLevelEntry> { Entry(TraitId.StartingGold, 0) } },
                new TraitSaveData { Version = 99, _levels = new List<TraitLevelEntry> { Entry(TraitId.StartingGold, 0) } },
                new TraitSaveData { Version = 1, _levels = new List<TraitLevelEntry>() }
            };
            for (int i = 0; i < invalid.Length; i++)
            {
                Require(!_traits.TryValidateSaveData(invalid[i], out error) && !string.IsNullOrEmpty(error),
                    "Reject invalid trait save " + i);
            }
            bool rejected = false;
            try { _traits.RestoreSaveData(invalid[7]); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected && events == 1 && _traits.GetLevel(TraitId.StartingGold) == 0,
                "Invalid restore leaves existing state and events unchanged");
        }
        finally { _traits.Changed -= changed; }
        PersistentWalletSaveData[] invalidWallets =
        {
            null,
            new PersistentWalletSaveData(-1),
            new PersistentWalletSaveData { Version = 0, Bloodstone = 500 },
            new PersistentWalletSaveData { Version = 99, Bloodstone = 500 }
        };
        for (int i = 0; i < invalidWallets.Length; i++)
            Require(!_wallet.TryValidateSaveData(invalidWallets[i], out error) && !string.IsNullOrEmpty(error),
                "Reject invalid wallet save " + i);
        Require(_wallet.TryValidateSaveData(new PersistentWalletSaveData(0), out error), "Zero bloodstone remains valid: " + error);
        Report.Add("Provider: deep copies, ID restoration, missing IDs, invalid data and section versions, replace semantics and change notifications passed.");
    }

    private static void ValidateStartupAndPurchase()
    {
        Initialize();
        string error;
        Require(_coordinator.TryLoadOrCreate(out error), "Create initial profile: " + error);
        Require(_coordinator.IsReady && File.Exists(_coordinator.SavePath), "Initial profile written and coordinator ready");
        Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.StartingGold) == 0, "Initial defaults retained");
        string original = File.ReadAllText(_coordinator.SavePath);
        Require(original.Contains("\"Traits\"") && original.Contains("\"Wallet\"") && original.Contains("\"_levels\"") && original.Contains("\"Bloodstone\""),
            "JSON contains nested trait and wallet fields");
        ITraitProgression progression = _traits;
        Require(!progression.TryUpgrade(TraitId.Production, out error), "Missing prerequisite rejected through trait interface");
        Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.Production) == 0 && original == File.ReadAllText(_coordinator.SavePath),
            "Denied purchase changes neither memory nor file");

        int traitEvents = 0;
        int walletEvents = 0;
        bool completeStateAtNotification = true;
        Action traitChanged = () =>
        {
            traitEvents++;
            completeStateAtNotification &= _wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1;
        };
        Action walletChanged = () =>
        {
            walletEvents++;
            completeStateAtNotification &= _wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1;
        };
        _traits.Changed += traitChanged;
        _wallet.Changed += walletChanged;
        try
        {
            Require(progression.TryUpgrade(TraitId.StartingGold, out error), "Trait interface owns persistent purchase: " + error);
            Require(traitEvents == 1 && walletEvents == 1 && completeStateAtNotification,
                "Each provider notifies once after both states committed");
        }
        finally
        {
            _traits.Changed -= traitChanged;
            _wallet.Changed -= walletChanged;
        }
        Require(_saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData saved, out error),
            "Saved profile deserializes: " + error);
        Require(saved.Traits.Version == 1 && saved.Wallet.Version == 1 && saved.Traits._levels.Count == 9 &&
            Level(saved.Traits, TraitId.StartingGold) == 1 && saved.Wallet.Bloodstone == 400,
            "Purchase file contains one upgraded level and one currency deduction");
        Initialize();
        Require(_coordinator.TryLoadOrCreate(out error), "Load existing profile: " + error);
        Require(_traits.GetLevel(TraitId.StartingGold) == 1 && _wallet.Balance == 400, "Both providers restored on restart");

        DateTime marker = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(_coordinator.SavePath, marker);
        string beforeLoad = File.ReadAllText(_coordinator.SavePath);
        Require(_coordinator.TryLoad(out error) && _coordinator.TryLoad(out error), "Repeated load succeeds: " + error);
        Require(beforeLoad == File.ReadAllText(_coordinator.SavePath) && File.GetLastWriteTimeUtc(_coordinator.SavePath) == marker,
            "Load does not autosave or charge currency");

        for (int i = 0; i < 4; i++)
            Require(_traits.TryUpgrade(TraitId.StartingGold, out error),
                "Purchase to maximum " + i + ": " + error + "; Balance=" + _wallet.Balance +
                "; Level=" + _traits.GetLevel(TraitId.StartingGold) + "; Path=" + _coordinator.SavePath);
        Require(_wallet.Balance == 0 && _traits.GetLevel(TraitId.StartingGold) == 5, "Exact remaining currency accepted once per level");
        string maximum = File.ReadAllText(_coordinator.SavePath);
        Require(!_traits.TryUpgrade(TraitId.StartingGold, out error), "Maximum trait rejected");
        Require(!_traits.TryUpgrade(TraitId.WaveReward, out error), "Insufficient currency rejected");
        Require(maximum == File.ReadAllText(_coordinator.SavePath) && _wallet.Balance == 0, "Rejected purchases preserve saved file");
        Report.Add("Trait purchase: interface request, saved single purchase, event ordering, restart, read-only repeat load, max level and insufficient balance passed.");
    }

    private static void ValidateSaveWithoutApply()
    {
        string error;
        _saveManager.ConfigureDirectory(Path.Combine(_directory, "snapshot-profile"));
        Initialize();
        Require(_coordinator.TryLoadOrCreate(out error), "Prepare snapshot-only save profile: " + error);
        IPersistentSaveWriter writer = _coordinator;
        PersistentSaveData next = writer.CaptureSaveData();
        for (int i = 0; i < next.Traits._levels.Count; i++)
        {
            if (next.Traits._levels[i].Id == TraitId.StartingGold)
                next.Traits._levels[i].Level = 1;
        }
        next.Wallet.Bloodstone = 400;
        Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.StartingGold) == 0,
            "Coordinator capture provides detached data for content changes");

        int traitEvents = 0;
        int walletEvents = 0;
        bool completeStateAtNotification = true;
        Action traitChanged = () =>
        {
            traitEvents++;
            completeStateAtNotification &= _wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1;
        };
        Action walletChanged = () =>
        {
            walletEvents++;
            completeStateAtNotification &= _wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1;
        };
        _traits.Changed += traitChanged;
        _wallet.Changed += walletChanged;
        try
        {
            Require(writer.TrySave(next, out error), "Save writer accepts prepared snapshot: " + error);
            Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.StartingGold) == 0 && traitEvents == 0 && walletEvents == 0,
                "Coordinator snapshot save never applies gameplay changes or sends provider notifications");
            Require(_saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData saved, out error) &&
                saved.Wallet.Bloodstone == 400 && Level(saved.Traits, TraitId.StartingGold) == 1,
                "Snapshot save persists supplied data instead of unchanged live state: " + error);
            Require(_coordinator.TryLoad(out error), "Explicit load applies saved snapshot: " + error);
            Require(_wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1 &&
                traitEvents == 1 && walletEvents == 1 && completeStateAtNotification,
                "Explicit load restores both providers then notifies each once");
        }
        finally
        {
            _traits.Changed -= traitChanged;
            _wallet.Changed -= walletChanged;
        }
        Report.Add("Save boundary: snapshot capture is detached; saving only writes data; explicit loading restores both providers and notifies once.");
    }

    private static void ValidateWriteFailure()
    {
        string error;
        _saveManager.ConfigureDirectory(Path.Combine(_directory, "write-failure-profile"));
        Initialize();
        Require(_coordinator.TryLoadOrCreate(out error), "Prepare write-failure profile: " + error);
        string originalPath = _coordinator.SavePath;
        string original = File.ReadAllText(originalPath);
        string blocker = Path.Combine(_directory, "directory-blocker");
        File.WriteAllText(blocker, "This file intentionally blocks directory creation.");
        _saveManager.ConfigureDirectory(blocker);
        int traitEvents = 0;
        int walletEvents = 0;
        Action traitChanged = () => traitEvents++;
        Action walletChanged = () => walletEvents++;
        _traits.Changed += traitChanged;
        _wallet.Changed += walletChanged;
        try
        {
            Require(!_traits.TryUpgrade(TraitId.StartingGold, out error) && !string.IsNullOrEmpty(error),
                "Filesystem write failure returned to caller");
            Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.StartingGold) == 0 && traitEvents == 0 && walletEvents == 0,
                "Failed write does not commit or notify either provider");
            Require(original == File.ReadAllText(originalPath), "Failed write preserves previous valid profile");
        }
        finally
        {
            _traits.Changed -= traitChanged;
            _wallet.Changed -= walletChanged;
            _saveManager.ConfigureDirectory(Path.GetDirectoryName(originalPath));
        }
        Require(_traits.TryUpgrade(TraitId.StartingGold, out error), "Purchase can retry after storage recovery: " + error);
        Require(_wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1, "Retry commits only once");
        Report.Add("Write failure: no currency loss, level change or notifications; existing profile retained; retry passed.");
    }

    private static void ValidateFileLockRetry()
    {
        string error;
        _saveManager.ConfigureDirectory(Path.Combine(_directory, "file-lock-profile"));
        Initialize();
        Require(_coordinator.TryLoadOrCreate(out error), "Prepare file-lock profile: " + error);
        string path = _coordinator.SavePath;
        int traitEvents = 0;
        int walletEvents = 0;
        Action traitChanged = () => traitEvents++;
        Action walletChanged = () => walletEvents++;
        _traits.Changed += traitChanged;
        _wallet.Changed += walletChanged;
        try
        {
            // 읽기는 허용하되 Delete 공유를 주지 않아 Windows 파일 교체를 잠시 막습니다.
            FileStream temporaryLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Thread releaseThread = new Thread(() =>
            {
                Thread.Sleep(60);
                temporaryLock.Dispose();
            });
            releaseThread.IsBackground = true;
            try
            {
                releaseThread.Start();
                Require(_traits.TryUpgrade(TraitId.StartingGold, out error), "Transient file lock recovers during save retry: " + error);
            }
            finally
            {
                releaseThread.Join();
                temporaryLock.Dispose();
            }
            Require(_wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1 && traitEvents == 1 && walletEvents == 1,
                "Transient lock success commits and notifies each provider once");
            Require(_saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData firstPurchase, out error) &&
                firstPurchase.Wallet.Bloodstone == 400 && Level(firstPurchase.Traits, TraitId.StartingGold) == 1,
                "Transient lock success writes correct paired state: " + error);

            string original = File.ReadAllText(path);
            using (FileStream persistentLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Require(!_traits.TryUpgrade(TraitId.StartingGold, out error) && !string.IsNullOrEmpty(error),
                    "Persistent file lock exhausts bounded retries");
                Require(_wallet.Balance == 400 && _traits.GetLevel(TraitId.StartingGold) == 1 && traitEvents == 1 && walletEvents == 1,
                    "Exhausted retries do not spend, upgrade or notify");
                Require(original == File.ReadAllText(path), "Exhausted retries preserve previous JSON");
            }

            Require(_traits.TryUpgrade(TraitId.StartingGold, out error), "Manual retry after unlocking succeeds: " + error);
            Require(_wallet.Balance == 300 && _traits.GetLevel(TraitId.StartingGold) == 2 && traitEvents == 2 && walletEvents == 2,
                "Unlocked retry applies exactly one additional purchase");
            Require(_saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData secondPurchase, out error) &&
                secondPurchase.Wallet.Bloodstone == 300 && Level(secondPurchase.Traits, TraitId.StartingGold) == 2,
                "Unlocked retry stores second paired purchase: " + error);
        }
        finally
        {
            _traits.Changed -= traitChanged;
            _wallet.Changed -= walletChanged;
        }
        Report.Add("File locks: short lock recovers within bounded retry; sustained lock preserves file and memory; explicit retry commits once.");
    }

    private static void ValidateInvalidFilesAndRecovery()
    {
        string error;
        _saveManager.ConfigureDirectory(Path.Combine(_directory, "invalid-profile"));
        Directory.CreateDirectory(_saveManager.DirectoryPath);
        Initialize();
        string path = _coordinator.SavePath;
        File.WriteAllText(path, "{ invalid JSON");
        AssertStartupRejected("Corrupt JSON");
        PersistentSaveData valid = Profile(Data(Entry(TraitId.StartingGold, 2), Entry(TraitId.Production, 1)), 150);
        SaveKey<PersistentSaveData> futureVersion = new SaveKey<PersistentSaveData>(PersistentSaveCoordinator.SaveKey.Id, 99);
        Require(_saveManager.TrySave(futureVersion, valid, out error), "Prepare incompatible version: " + error);
        AssertStartupRejected("Incompatible version");

        // JsonUtility.ToJson은 null 직렬화 클래스도 기본 필드가 있는 객체로 만들 수 있습니다.
        // 손상된 파일 입력은 저장 API를 거치지 않고 실제 null/누락 JSON으로 작성합니다.
        string validTraitSection = "\"Traits\":{\"Version\":1,\"_levels\":[{\"Id\":12000,\"Level\":0}]}";
        string validWalletSection = "\"Wallet\":{\"Version\":1,\"Bloodstone\":150}";
        string[] missingSections =
        {
            "{\"Traits\":null," + validWalletSection + "}",
            "{" + validWalletSection + "}",
            "{" + validTraitSection + ",\"Wallet\":null}",
            "{" + validTraitSection + "}",
            "{\"Traits\":{\"Version\":1,\"_levels\":null}," + validWalletSection + "}",
            "{\"Traits\":{\"Version\":1}," + validWalletSection + "}",
            "{\"Traits\":{\"Version\":1,\"_levels\":[]}," + validWalletSection + "}"
        };
        string[] sectionLabels =
        {
            "Null traits section", "Missing traits section", "Null wallet section", "Missing wallet section",
            "Null trait levels", "Missing trait levels", "Empty trait levels"
        };
        for (int i = 0; i < missingSections.Length; i++)
        {
            string envelope = "{\"Key\":\"" + PersistentSaveCoordinator.SaveKey.Id +
                "\",\"Version\":" + PersistentSaveCoordinator.SaveKey.Version +
                ",\"DataType\":\"" + typeof(PersistentSaveData).FullName + "\",\"Data\":" + missingSections[i] + "}";
            File.WriteAllText(path, envelope);
            if (_saveManager.TryLoad(PersistentSaveCoordinator.SaveKey, out PersistentSaveData parsed, out error))
                Report.Add(sectionLabels[i] + " parsed: Traits null=" + (parsed.Traits == null) + ", Wallet null=" + (parsed.Wallet == null));
            AssertStartupRejected(sectionLabels[i]);
        }

        PersistentSaveData[] invalid =
        {
            Profile(Data(Entry(TraitId.StartingGold, 0)), -1),
            Profile(Data(Entry(TraitId.StartingGold, 6)), 150),
            Profile(Data(Entry(TraitId.Production, 1)), 150)
        };
        for (int i = 0; i < invalid.Length; i++)
        {
            Require(_saveManager.TrySave(PersistentSaveCoordinator.SaveKey, invalid[i], out error), "Prepare semantically invalid profile " + i + ": " + error);
            AssertStartupRejected("Invalid profile " + i);
        }

        Require(_saveManager.TrySave(PersistentSaveCoordinator.SaveKey, valid, out error), "Repair profile: " + error);
        Require(_coordinator.TryLoad(out error) && _coordinator.IsReady, "Explicit load recovers readiness after repaired file: " + error);
        Require(_wallet.Balance == 150 && _traits.GetLevel(TraitId.StartingGold) == 2 && _traits.GetLevel(TraitId.Production) == 1,
            "Recovery restores all valid provider states");
        Require(_traits.GetLevel(TraitId.MaxHealth) == 0, "Recovery initializes missing trait IDs to zero");
        Require(_coordinator.TrySave(out error), "Explicit save succeeds after recovery: " + error);
        Report.Add("Invalid files: malformed JSON, version, raw null/missing sections, negative balance and trait rules rejected without overwrite; repaired file recovers.");
    }

    private static void AssertStartupRejected(string label)
    {
        Initialize();
        string original = File.ReadAllText(_coordinator.SavePath);
        Require(!_coordinator.TryLoadOrCreate(out string error) && !string.IsNullOrEmpty(error), label + " fails startup");
        Require(!_coordinator.IsReady, label + " blocks purchases until recovered");
        Require(_wallet.Balance == 500 && _traits.GetLevel(TraitId.StartingGold) == 0 && _traits.GetLevel(TraitId.Production) == 0,
            label + " causes no partial restore");
        Require(!_traits.TryUpgrade(TraitId.StartingGold, out error), label + " cannot purchase over unreadable save");
        Require(!_coordinator.TrySave(out error), label + " cannot overwrite unreadable save with defaults");
        Require(original == File.ReadAllText(_coordinator.SavePath), label + " file preserved");
    }

    private static TraitLevelEntry Entry(TraitId id, int level) { return new TraitLevelEntry { Id = id, Level = level }; }
    private static TraitSaveData Data(params TraitLevelEntry[] entries) { return new TraitSaveData(new List<TraitLevelEntry>(entries)); }
    private static PersistentSaveData Profile(TraitSaveData traits, int bloodstone)
    {
        return new PersistentSaveData { Traits = traits, Wallet = new PersistentWalletSaveData(bloodstone) };
    }

    private static int Level(TraitSaveData data, TraitId id)
    {
        for (int i = 0; i < data._levels.Count; i++) if (data._levels[i].Id == id) return data._levels[i].Level;
        return 0;
    }

    private static void Require(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException("Persistent save validation failed: " + message);
    }
}
