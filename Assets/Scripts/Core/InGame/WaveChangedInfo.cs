using System;
using Game.Core;using Unity.VisualScripting;
using UnityEngine;

public readonly struct WaveChangedInfo
{
    public int QuarterNumber { get; }
    public int WaveNumber { get; }
    public WaveBattleType BattleType { get; } //전투 타입(일반, 정예, 보스)
    public PostBattleEventType PostBattleEvent { get; } //해당 전투 종료 후 이벤트 타입

    public WaveChangedInfo(int quarterNumber, int waveNumber, WaveBattleType battleType,
        PostBattleEventType postBattleEvent)
    {
        QuarterNumber = quarterNumber;
        WaveNumber = waveNumber;
        BattleType = battleType;
        PostBattleEvent = postBattleEvent;
    }
}

