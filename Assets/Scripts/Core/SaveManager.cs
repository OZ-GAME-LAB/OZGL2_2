using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public interface ISaveDataProvider<T> where T : class
{
    T CaptureSaveData();
    void RestoreSaveData(T data);
}

public interface IRunCheckpointWriter
{
    bool TrySave(out string error);
    bool TryDelete(out string error);
}

public class SaveManager : MonoBehaviour
{
    private string _directory;

    // Editor의 도메인 재로드에서 null 문자열이 빈 값으로 복원될 수 있다.
    public string DirectoryPath => string.IsNullOrWhiteSpace(_directory)
        ? Path.Combine(Application.persistentDataPath, "Saves") : _directory;

    public bool HasSaveFile<T>(SaveKey<T> key) where T : class
    {
        return File.Exists(GetFilePath(key));
    }
    // 테스트에서는 실제 플레이어 저장소와 분리된 디렉터리를 주입합니다.
    public void ConfigureDirectory(string absoluteDirectory)
    {
        if (string.IsNullOrWhiteSpace(absoluteDirectory) || !Path.IsPathRooted(absoluteDirectory))
            throw new ArgumentException("저장 디렉터리는 절대 경로여야 합니다.", nameof(absoluteDirectory));

        _directory = Path.GetFullPath(absoluteDirectory);
    }

    public string GetFilePath<T>(SaveKey<T> key) where T : class
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return Path.Combine(DirectoryPath, key.Id + ".json");
    }

    // 복원을 생략한 영역이 있는 파일은 첫 덮어쓰기 전에 원본 그대로 보관합니다.
    public bool TryBackup<T>(SaveKey<T> key, out string backupPath, out string error) where T : class
    {
        backupPath = null;
        error = null;
        try
        {
            string path = GetFilePath(key);
            if (!File.Exists(path)) return true;

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");
            string destination = path + "." + timestamp + "." + Guid.NewGuid().ToString("N") + ".bak";
            File.Copy(path, destination, false);
            backupPath = destination;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    // 이미 삭제된 런도 성공으로 처리합니다.
    public bool TryDelete<T>(SaveKey<T> key, out string error) where T : class
    {
        error = null;
        try
        {
            File.Delete(GetFilePath(key));
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    // true는 임시 메모리 갱신이 아니라 JSON 파일 교체까지 성공했다는 뜻입니다.
    public bool TrySave<T>(SaveKey<T> key, T data, out string error) where T : class
    {
        error = null;
        string temporaryPath = null;
        try
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));
            ValidateDataType<T>();
            if (data.GetType() != typeof(T))
                throw new ArgumentException("저장 키와 데이터의 실제 타입이 같아야 합니다.", nameof(data));

            var file = new SaveFile<T>
            {
                Key = key.Id,
                Version = key.Version,
                DataType = typeof(T).FullName,
                Data = data
            };
            string json = JsonUtility.ToJson(file, true);
            string path = GetFilePath(key);
            Directory.CreateDirectory(DirectoryPath);
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

            // 기존 파일은 새 JSON 쓰기가 완료된 뒤 한 번에 교체합니다.
            if (File.Exists(path)) ReplaceFile(temporaryPath, path);
            else File.Move(temporaryPath, path);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (temporaryPath != null)
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    // 파일 없음/손상/버전 불일치는 실패로 반환합니다. 기본값 저장으로 덮어쓰지 않습니다.
    public bool TryLoad<T>(SaveKey<T> key, out T data, out string error) where T : class
    {
        data = null;
        error = null;
        try
        {
            ValidateDataType<T>();
            string path = GetFilePath(key);
            if (!File.Exists(path))
            {
                error = "저장 파일이 없습니다: " + path;
                return false;
            }

            var file = JsonUtility.FromJson<SaveFile<T>>(File.ReadAllText(path, Encoding.UTF8));
            if (file == null || file.Key != key.Id || file.Version != key.Version ||
                file.DataType != typeof(T).FullName || file.Data == null)
            {
                error = "저장 키, 버전, 데이터 타입 또는 내용이 올바르지 않습니다.";
                return false;
            }

            data = file.Data;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    // Windows에서 잠시 열린 파일 때문에 교체가 거부될 때만 같은 임시 파일로 재시도합니다.
    // 원본을 먼저 지우거나 구매 로직을 다시 실행하지 않습니다.
    private static void ReplaceFile(string temporaryPath, string path)
    {
        int delayMilliseconds = 25;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                File.Replace(temporaryPath, path, null);
                return;
            }
            catch (IOException exception)
            {
                int errorCode = exception.HResult & 0xFFFF;
                // 공유 위반(32), 잠금 위반(33), 기존 파일 제거 불가(1175).
                bool temporaryLock = errorCode == 32 || errorCode == 33 || errorCode == 1175;
                if (!temporaryLock || attempt == 3 || !File.Exists(temporaryPath) || !File.Exists(path)) throw;

                Thread.Sleep(delayMilliseconds);
                delayMilliseconds *= 2;
            }
        }
    }

    private static void ValidateDataType<T>() where T : class
    {
        Type type = typeof(T);
        if (!type.IsClass || type.IsAbstract || type == typeof(object) || type == typeof(string) ||
            !Attribute.IsDefined(type, typeof(SerializableAttribute), false) ||
            typeof(UnityEngine.Object).IsAssignableFrom(type) ||
            typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            throw new ArgumentException("저장 루트는 [Serializable]이 붙은 구체적인 DTO 클래스여야 합니다. 컬렉션은 DTO의 필드로 감싸주세요.");
    }

    [Serializable]
    private sealed class SaveFile<T> where T : class
    {
        public string Key;
        public int Version;
        public string DataType;
        public T Data;
    }
}
