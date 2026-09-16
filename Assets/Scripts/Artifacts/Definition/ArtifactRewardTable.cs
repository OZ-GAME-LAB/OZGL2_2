using UnityEngine;

// 일반/정예 전투 보상의 등급 가중치 (보스전은 설정과 관계없이 신화만 추첨)
[CreateAssetMenu(fileName = "ArtifactRewardTable", menuName = "Artifacts/Artifact Reward Table")]
public class ArtifactRewardTable : ScriptableObject
{
    public int CandidateCount => _candidateCount;
    public bool IsValid => _candidateCount > 0 && _commonWeight >= 0
        && _rareWeight >= 0 && _legendaryWeight >= 0;

    [SerializeField, Min(1)] private int _candidateCount = 3;

    [Tooltip("보스전 외 보상의 상대 가중치입니다. 0이면 제외합니다. 기본값 1:1:1은 임시 설정입니다.")]
    [SerializeField, Min(0)] private int _commonWeight = 1;
    [SerializeField, Min(0)] private int _rareWeight = 1;
    [SerializeField, Min(0)] private int _legendaryWeight = 1;

    public int GetWeight(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Common: return _commonWeight;
            case ArtifactRarity.Rare: return _rareWeight;
            case ArtifactRarity.Legendary: return _legendaryWeight;
            default: return 0;
        }
    }

    private void OnValidate()
    {
        if (!IsValid)
        {
            Debug.LogError("[Artifacts/ArtifactRewardTable] 후보 수는 1 이상, 등급 가중치는 0 이상이어야 합니다.", this);
        }
    }
}
