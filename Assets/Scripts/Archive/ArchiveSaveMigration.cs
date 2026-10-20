using System;
using Newtonsoft.Json.Linq;

namespace Game.Archive
{
    // 이전 별도 파일을 통합 저장 형식으로 읽어오는 코드입니다. 파일을 쓰거나 지우지는 않습니다.
    public static class ArchiveSaveMigration
    {
        public static readonly SaveKey<ArchiveProgressSaveData> LegacyKey = new("archive-progress", version: 1);

        public static bool TryUpgrade(SaveManager saveManager, PersistentSaveData data, out string error)
        {
            error = null;
            if (data.ArchiveVersion != 0) return true;
            ArchiveProgressSaveData archive = ArchiveProgressSaveData.CreateEmpty();
            if (saveManager.HasSaveFile(LegacyKey) &&
                !saveManager.TryLoad(LegacyKey, out archive, out error,
                    json => ValidateArchiveJson(JObject.Parse(json)["Data"]))) return false;
            try { ArchiveController.ValidateSaveData(archive); }
            catch (Exception exception)
            {
                error = "이전 도감 복원 실패: " + exception.Message;
                return false;
            }
            data.Archive = archive;
            data.ArchiveVersion = 1;
            return true;
        }

        public static void ValidatePersistentJson(string json)
        {
            var data = JObject.Parse(json)["Data"];
            if (data is not JObject) throw new ArgumentException("영구 저장 데이터가 없습니다.");
            if ((int?)data["ArchiveVersion"] == 1) ValidateArchiveJson(data["Archive"]);
        }

        private static void ValidateArchiveJson(JToken archive)
        {
            // JsonUtility가 누락된 목록을 빈 목록으로 바꾸기 전에 원본을 확인합니다.
            if (archive is not JObject || archive["DiscoveredKeys"] is not JArray || archive["UnreadKeys"] is not JArray)
                throw new ArgumentException("도감 발견/열람 목록이 누락되었거나 올바른 배열이 아닙니다.");
        }
    }
}
