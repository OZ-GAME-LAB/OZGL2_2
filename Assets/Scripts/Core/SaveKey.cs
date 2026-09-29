using System;

// 키와 저장 DTO의 타입을 묶습니다. 콘텐츠별 키는 호출하는 쪽에서 정의합니다.
public sealed class SaveKey<T> where T : class
{
    public string Id { get; }
    public int Version { get; }

    public SaveKey(string id, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("저장 키가 비어 있습니다.", nameof(id));
        foreach (char character in id)
        {
            if (!(character >= 'a' && character <= 'z') &&
                !(character >= 'A' && character <= 'Z') &&
                !(character >= '0' && character <= '9') && character != '-' && character != '_')
                throw new ArgumentException("저장 키는 영문, 숫자, 하이픈, 밑줄만 사용할 수 있습니다.", nameof(id));
        }
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        Version = version;
    }
}
