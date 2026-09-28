using System;
using System.Collections.Generic;

// UI는 구현 클래스 대신 아래 계약으로 표시 정보와 선택 상태에 접근합니다.
public interface IAltarSelection
{
    event Action Changed;
    IReadOnlyList<AltarData> Data { get; }
    AltarId SelectedAltar { get; }
    bool IsUnlocked(AltarId id);
    string GetUnlockCondition(AltarId id);
    bool TrySelect(AltarId id);
}

public interface ITraitProgression
{
    event Action Changed;
    IReadOnlyList<TraitData> Data { get; }
    int GetLevel(TraitId id);
    bool CanUpgrade(TraitId id, out string reason);
    bool TryUpgrade(TraitId id);
    bool TryUpgrade(TraitId id, out string error);
    List<TraitLevelEntry> CaptureLevels();
}

public interface ITotemSelection
{
    event Action Changed;
    IReadOnlyList<TotemData> Data { get; }
    int GetLevel(TotemId id);
    bool TryChangeLevel(TotemId id, int delta);
    int GetRewardBonusPercent();
    List<TotemLevelEntry> CaptureLevels();
}

[Serializable]
public class TraitLevelEntry
{
    public TraitId Id;
    public int Level;
}

[Serializable]
public class TotemLevelEntry
{
    public TotemId Id;
    public int Level;
}

/// <summary>시작을 눌렀을 때 복사한 설정입니다. SO나 씬 오브젝트의 참조를 담지 않습니다.</summary>
[Serializable]
public class OutGameStartContext
{
    public AltarId SelectedAltar; //선택된 제단
    public List<TraitLevelEntry> Traits = new List<TraitLevelEntry>(); //해금된 특성 리스트
    public List<TotemLevelEntry> Totems = new List<TotemLevelEntry>(); //선택된 토템 리스트
    public int RewardBonusPercent; //보상 배율
}
