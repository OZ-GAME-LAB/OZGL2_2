using System.Collections.Generic;
using Units;
using UnityEngine;

public enum WaveBattleType
{
    Normal,
    Elite,
    Boss
}

[System.Serializable]
public struct ClassWeights
{
    [Min(0)] public int Tanker;
    [Min(0)] public int Bruiser;
    [Min(0)] public int Assassin;
    [Min(0)] public int RangedPhysical;
    [Min(0)] public int RangedMagic;
    [Min(0)] public int Supporter;
}
[System.Serializable]
public struct IntRange
{
    public int Min;
    public int Max;

    public bool Contains(int value)
    {
        return Min <= value && value <= Max;
    }
}
[CreateAssetMenu(fileName = "WaveSO", menuName = "Scriptable Objects/WaveSO")]
public class WaveSO : ScriptableObject
{
    public int WaveID; //웨이브 식별자
    public string WaveName;
    public WaveBattleType BattleType;
    public ClassWeights Weights;
    public List<EnemyUnitFaction> MonsterIDs = new List<Units.EnemyUnitFaction>(); //몬스터 id 리스트

    //MonsterIDs를 뭘로할까? enum?
}
