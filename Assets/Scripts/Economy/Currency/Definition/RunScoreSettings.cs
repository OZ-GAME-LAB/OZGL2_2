using System;
using UnityEngine;

/// <summary>정산 점수표. 여러 씬에서 같은 에셋을 참조합니다.</summary>
[CreateAssetMenu(menuName = "Economy/Run Score Settings", fileName = "RunScoreSettings")]
public sealed class RunScoreSettings : ScriptableObject
{
    [Header("진행도 점수")]
    [SerializeField, Min(0)] private int _waveScore = 10;
    [Tooltip("보스 웨이브도 웨이브 점수를 받습니다. 이 값은 추가 점수입니다.")]
    [SerializeField, Min(0)] private int _bossBonusScore = 50;
    [Header("유물 한 개당 점수")]
    [SerializeField, Min(0)] private int _commonScore = 5;
    [SerializeField, Min(0)] private int _rareScore = 15;
    [SerializeField, Min(0)] private int _legendaryScore = 50;
    [SerializeField, Min(0)] private int _mythicScore = 150;
    [Header("혈석 환산")]
    [SerializeField, Min(1)] private int _pointsPerBloodstone = 10;

    public int WaveScore => _waveScore;
    public int BossBonusScore => _bossBonusScore;
    public int PointsPerBloodstone => _pointsPerBloodstone;

    public int GetArtifactScore(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Common: return _commonScore;
            case ArtifactRarity.Rare: return _rareScore;
            case ArtifactRarity.Legendary: return _legendaryScore;
            case ArtifactRarity.Mythic: return _mythicScore;
            default: throw new ArgumentOutOfRangeException(nameof(rarity));
        }
    }

    public void Validate()
    {
        if (_waveScore < 0 || _bossBonusScore < 0 || _commonScore < 0 || _rareScore < 0 ||
            _legendaryScore < 0 || _mythicScore < 0 || _pointsPerBloodstone < 1)
            throw new InvalidOperationException("정산 점수는 0 이상, 혈석당 필요 점수는 1 이상이어야 합니다.");
    }
}
