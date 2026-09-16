using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuarterRewardData
{
    public int QuarterNumber => _quarterNumber;
    public IReadOnlyList<WaveRewardData> Waves => _waves;

    [SerializeField, Min(1)] private int _quarterNumber = 1;
    [SerializeField] private List<WaveRewardData> _waves = new List<WaveRewardData>();
}
