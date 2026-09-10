using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveRewardTable", menuName = "Economy/Wave Reward Table")]
public class WaveRewardTable : ScriptableObject
{
    public IReadOnlyList<QuarterRewardData> Quarters => _quarters;

    [SerializeField] private List<QuarterRewardData> _quarters = new List<QuarterRewardData>();

    // 목록의 인덱스가 아닌 명시적으로 설정한 분기,웨이브 번호로 조회
    public bool TryGetRewards(int quarterNumber, int waveNumber, out IReadOnlyList<CurrencyAmount> rewards)
    {
        rewards = null;
        if (quarterNumber < 1 || waveNumber < 1 || _quarters == null)
            return false;

        QuarterRewardData matchedQuarter = null;
        foreach (QuarterRewardData quarter in _quarters)
        {
            if (quarter == null || quarter.QuarterNumber != quarterNumber)
                continue;

            if (matchedQuarter != null)
                return false;

            matchedQuarter = quarter;
        }

        if (matchedQuarter == null || matchedQuarter.Waves == null)
            return false;

        WaveRewardData matchedWave = null;
        foreach (WaveRewardData wave in matchedQuarter.Waves)
        {
            if (wave == null || wave.WaveNumber != waveNumber)
                continue;

            if (matchedWave != null)
                return false;

            matchedWave = wave;
        }

        if (matchedWave == null || !AreRewardsValid(matchedWave.Rewards))
            return false;

        rewards = matchedWave.Rewards;
        return true;
    }

    private static bool AreRewardsValid(IReadOnlyList<CurrencyAmount> rewards)
    {
        if (rewards == null)
            return false;

        HashSet<CurrencyType> types = new HashSet<CurrencyType>();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (CurrencyAmount reward in rewards)
        {
            CurrencyData currency = reward.Currency;
            if (currency == null || reward.Amount < 0 || currency.Lifetime != CurrencyLifetime.Run ||
                string.IsNullOrWhiteSpace(currency.Id) || currency.Type == CurrencyType.None ||
                !Enum.IsDefined(typeof(CurrencyType), currency.Type))
                return false;

            if (!types.Add(currency.Type) || !ids.Add(currency.Id))
                return false;
        }

        return true;
    }

    private void OnValidate()
    {
        if (_quarters == null)
        {
            Debug.LogError("[Economy/WaveRewardTable] 분기 목록이 null입니다.", this);
            return;
        }

        HashSet<int> quarterNumbers = new HashSet<int>();
        foreach (QuarterRewardData quarter in _quarters)
        {
            if (quarter == null)
            {
                Debug.LogError("[Economy/WaveRewardTable] 비어 있는 분기 항목이 있습니다.", this);
                continue;
            }

            if (quarter.QuarterNumber < 1 || !quarterNumbers.Add(quarter.QuarterNumber))
                Debug.LogError($"[Economy/WaveRewardTable] 분기 번호는 1 이상이며 중복될 수 없습니다. Quarter: {quarter.QuarterNumber}", this);

            if (quarter.Waves == null)
            {
                Debug.LogError($"[Economy/WaveRewardTable] 웨이브 목록이 null입니다. Quarter: {quarter.QuarterNumber}", this);
                continue;
            }

            HashSet<int> waveNumbers = new HashSet<int>();
            foreach (WaveRewardData wave in quarter.Waves)
            {
                if (wave == null)
                {
                    Debug.LogError($"[Economy/WaveRewardTable] 비어 있는 웨이브 항목이 있습니다. Quarter: {quarter.QuarterNumber}", this);
                    continue;
                }

                if (wave.WaveNumber < 1 || !waveNumbers.Add(wave.WaveNumber))
                    Debug.LogError($"[Economy/WaveRewardTable] 웨이브 번호는 분기 내에서 1 이상이며 중복될 수 없습니다. Quarter: {quarter.QuarterNumber}, Wave: {wave.WaveNumber}", this);

                if (!AreRewardsValid(wave.Rewards))
                    Debug.LogError($"[Economy/WaveRewardTable] 보상의 재화·Type·ID·수량을 확인하세요. Run 재화만 가능하며 동일 재화를 중복 설정할 수 없습니다. Quarter: {quarter.QuarterNumber}, Wave: {wave.WaveNumber}", this);
            }
        }
    }
}
