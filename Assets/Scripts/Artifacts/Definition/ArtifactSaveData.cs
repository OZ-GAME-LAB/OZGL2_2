using System;
using System.Collections.Generic;

[Serializable]
public class ArtifactSaveData
{
    // 현재 보유한 아티팩트의 ID와 중첩 수 목록입니다.
    public List<ArtifactSaveEntry> Owned = new();
    // 아직 선택하지 않은 클리어 보상 후보의 ID 목록입니다. 표시 순서를 유지합니다.
    public List<string> CandidateIds = new();
    // 해당 보상의 후보 추첨을 이미 처리했는지 나타냅니다. 후보가 없어도 true일 수 있습니다.
    public bool HasRewardCandidates;
    // 해당 보상을 처리했는지 나타냅니다. 복원 후 같은 보상을 다시 받지 않도록 사용합니다.
    public bool RewardApplied;
    // 위 보상 상태에 해당하는 분기 번호입니다. 보상 위치가 없으면 0입니다.
    public int RewardQuarter;
    // 위 보상 상태에 해당하는 분기 내 웨이브 번호입니다. 보상 위치가 없으면 0입니다.
    public int RewardWave;
}

[Serializable]
public class ArtifactSaveEntry
{
    // 카탈로그에서 아티팩트 S.O를 다시 찾기 위한 고유 ID입니다.
    public string ArtifactId;
    // 해당 아티팩트를 보유한 중첩 수입니다.
    public int StackCount;
}
