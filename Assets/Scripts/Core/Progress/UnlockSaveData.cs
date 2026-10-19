using System;
using System.Collections.Generic;

namespace Game.Core
{
    [Serializable]
    public sealed class UnlockSaveData
    {
        public int Version;
        public static UnlockSaveData CreateEmpty() => new UnlockSaveData { Version = 1 };
        public List<string> Ids = new List<string>();
        public UnlockSaveData Copy() => new UnlockSaveData { Version = Version, Ids = new List<string>(Ids) };

        public void Validate()
        {
            if (Version != 1) throw new ArgumentException("필수 해금 기록이 없거나 버전이 올바르지 않습니다.");
            if (Ids == null) throw new ArgumentException("해금 ID 목록이 없습니다.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in Ids)
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    throw new ArgumentException("해금 ID가 비어 있거나 중복됩니다.");
        }
    }
}
