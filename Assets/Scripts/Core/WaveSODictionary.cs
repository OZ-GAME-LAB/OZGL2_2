using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveGroup
{
    public IntRange AvailableQuarters;
    public List<WaveSO> Presets = new List<WaveSO>();
}

[Serializable]
public class FactionWaveGroup
{
    public EnemyFaction Faction;
    public List<WaveGroup> Groups = new List<WaveGroup>();
}

[CreateAssetMenu(fileName = "WaveSODictionary", menuName = "Scriptable Objects/WaveSODictionary")]
public class WaveSODictionary : ScriptableObject
{
    public List<FactionWaveGroup> Groups = new List<FactionWaveGroup>();

    private Dictionary<EnemyFaction, List<WaveGroup>> _waveDictionary;

    private void OnEnable()
    {
        RebuildDictionary();
    }

    private void OnValidate()
    {
        RebuildDictionary();
    }

    // 런타임에 원본 리스트 구성을 변경했다면 다시 호출한다.
    public void RebuildDictionary()
    {
        _waveDictionary = new Dictionary<EnemyFaction, List<WaveGroup>>();
        if (Groups == null) return;

        foreach (var entry in Groups)
        {
            if (entry == null || entry.Groups == null) continue;

            if (!_waveDictionary.TryGetValue(entry.Faction, out var factionGroups))
            {
                factionGroups = new List<WaveGroup>();
                _waveDictionary.Add(entry.Faction, factionGroups);
            }

            // 같은 세력의 항목이 여러 개면 덮어쓰지 않고 합친다.
            foreach (var group in entry.Groups)
            {
                if (group != null)
                    factionGroups.Add(group);
            }
        }
    }

    public bool TryGetRandomWaveSO(
        int currentQuarter,
        WaveBattleType battleType,
        out WaveSO result)
    {
        return TryGetRandomWaveSO(currentQuarter, battleType, out result, out _);
    }

    public bool TryGetRandomWaveSO(
        int currentQuarter,
        WaveBattleType battleType,
        out WaveSO result,
        out EnemyFaction faction)
    {
        if (_waveDictionary == null)
            RebuildDictionary();

        var candidates = new List<WaveSO>();
        var seen = new HashSet<WaveSO>();
        var factions = new List<EnemyFaction>();

        foreach (var entry in _waveDictionary)
        {
            foreach (var group in entry.Value)
            {
                if (!group.AvailableQuarters.Contains(currentQuarter) ||
                    group.Presets == null)
                    continue;

                foreach (var preset in group.Presets)
                {
                    if (preset == null || preset.BattleType != battleType)
                        continue;

                    if (seen.Add(preset))
                    {
                        candidates.Add(preset);
                        factions.Add(entry.Key);
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            result = null;
            faction = default;
            return false;
        }

        // 허용된 모든 세력의 중복 없는 프리셋 후보를 균등 추첨한다.
        int index = UnityEngine.Random.Range(0, candidates.Count);
        result = candidates[index];
        faction = factions[index];
        return true;
    }
}