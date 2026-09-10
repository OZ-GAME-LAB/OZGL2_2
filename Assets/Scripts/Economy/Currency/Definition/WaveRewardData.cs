using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveRewardData
{
    public int WaveNumber => _waveNumber;
    public IReadOnlyList<CurrencyAmount> Rewards => _rewards;

    // 분기마다 1부터 시작하며 이벤트,상점,전투를 모두 포함하는 번호
    [SerializeField, Min(1)] private int _waveNumber = 1;

    // 효과 적용 전 기본 보상. 빈 목록은 해당 웨이브의 보상이 없음 의미
    [SerializeField] private List<CurrencyAmount> _rewards = new List<CurrencyAmount>();
}
