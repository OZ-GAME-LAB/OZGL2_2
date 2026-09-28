using System;
using System.Collections.Generic;

namespace Game.Core.SaveTests
{
    // 팀의 실제 저장 DTO가 준비되기 전 사용하는 테스트 전용 계약입니다.
    [Serializable]
    public sealed class OutGameSaveTestData
    {
        public TestPermanentState Permanent = new TestPermanentState();
        public TestOutGameSelection LastSelection = new TestOutGameSelection();

        public bool TryValidate(out string error)
        {
            error = null;
            if (Permanent == null || LastSelection == null || Permanent.Bloodstone < 0 ||
                Permanent.UnlockedAltarIds == null || Permanent.Traits == null ||
                LastSelection.AltarId == null || LastSelection.Totems == null)
            {
                error = "필수 영역이 없거나 영구 재화가 음수입니다.";
                return false;
            }

            var altarIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in Permanent.UnlockedAltarIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !altarIds.Add(id))
                {
                    error = "해금 제단 ID가 비어 있거나 중복됩니다.";
                    return false;
                }
            }
            if (LastSelection.AltarId.Length > 0 && !altarIds.Contains(LastSelection.AltarId))
            {
                error = "선택한 제단이 해금 목록에 없습니다.";
                return false;
            }
            return ValidateLevels(Permanent.Traits, "특성", out error) &&
                   ValidateLevels(LastSelection.Totems, "토템", out error);
        }

        private static bool ValidateLevels(List<TestLevelEntry> entries, string label, out string error)
        {
            error = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TestLevelEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id) || entry.Level < 0 || !ids.Add(entry.Id))
                {
                    error = label + " ID가 비어 있거나 중복되거나 레벨이 음수입니다.";
                    return false;
                }
            }
            return true;
        }

        public OutGameSaveTestData Copy()
        {
            if (!TryValidate(out string error)) throw new ArgumentException(error);
            return new OutGameSaveTestData
            {
                Permanent = new TestPermanentState
                {
                    Bloodstone = Permanent.Bloodstone,
                    UnlockedAltarIds = new List<string>(Permanent.UnlockedAltarIds),
                    Traits = new List<TestLevelEntry>(Permanent.Traits)
                },
                LastSelection = new TestOutGameSelection
                {
                    AltarId = LastSelection.AltarId,
                    Totems = new List<TestLevelEntry>(LastSelection.Totems)
                }
            };
        }
    }

    [Serializable]
    public sealed class TestPermanentState
    {
        public int Bloodstone;
        public List<string> UnlockedAltarIds = new List<string>();
        public List<TestLevelEntry> Traits = new List<TestLevelEntry>();
    }

    [Serializable]
    public sealed class TestOutGameSelection
    {
        // 출전 효과/성장이 아닌 마지막 선택 복원 테스트용 값입니다.
        public string AltarId = "";
        public List<TestLevelEntry> Totems = new List<TestLevelEntry>();
    }

    [Serializable]
    public struct TestLevelEntry
    {
        public string Id;
        public int Level;
    }
}
