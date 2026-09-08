using System.Collections.Generic;
using UnityEngine;

public enum EnemyFaction
{
    Irregulars,     // 비정규군
    RegularArmy,   // 정규군
    EliteArmy,     // 정예군
    Crusaders      // 성전군
}
public enum WaveBattleType
{
    Normal,
    Elite,
    Boss
}

public enum EnemyClass
{
    Tanker,
    Bruiser,
    Assassin,
    RangedPhysical,
    RangedMagic,
    Supporter
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
    public int WaveID;
    public string WaveName;
    public WaveBattleType BattleType;
    public ClassWeights Weights;
    public List<int> MonsterIDs = new List<int>();
}
