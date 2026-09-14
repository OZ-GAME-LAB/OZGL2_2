using System;
using System.Collections.Generic;

// 획득 가능한 아티팩트를 등급별로 추첨 (Inventory 변경 X)
public class ArtifactCandidateSelector
{
    private static readonly ArtifactRarity[] Rarities =
    {
        ArtifactRarity.Common, 
        ArtifactRarity.Rare,
        ArtifactRarity.Legendary, 
        ArtifactRarity.Mythic
    };

    private readonly ArtifactCatalog _catalog;
    private readonly ArtifactInventory _inventory;
    private readonly ArtifactRewardTable _rewardTable;
    private readonly Random _random;

    public ArtifactCandidateSelector(ArtifactCatalog catalog, ArtifactInventory inventory,
        ArtifactRewardTable rewardTable, Random random = null)
    {
        if (catalog == null || inventory == null || rewardTable == null)
        {
            UnityEngine.Debug.LogError("[Artifacts/ArtifactCandidateSelector] Catalog, Inventory, RewardTable 참조를 확인해주세요.");
        }

        _catalog = catalog;
        _inventory = inventory;
        _rewardTable = rewardTable;
        _random = random ?? new Random();
    }

    // 설정 오류는 false, 정상적인 후보 부족은 true와 0~CandidateCount개의 목록을 반환
    public bool TryCreateCandidates(bool isBossWave, out IReadOnlyList<ArtifactData> candidates)
    {
        candidates = new List<ArtifactData>();
        if (_catalog == null || _inventory == null || _rewardTable == null || !_rewardTable.IsValid)
        {
            return false;
        }

        var pools = new List<ArtifactData>[Rarities.Length];
        var weights = new double[Rarities.Length];
        int availableCount = 0;

        for (int i = 0; i < Rarities.Length; i++)
        {
            ArtifactRarity rarity = Rarities[i];
            weights[i] = isBossWave
                ? (rarity == ArtifactRarity.Mythic ? 1 : 0)
                : _rewardTable.GetWeight(rarity);
            if (weights[i] <= 0)
            {
                continue;
            }

            var pool = new List<ArtifactData>();
            foreach (ArtifactData artifact in _catalog.GetByRarity(rarity))
            {
                if (_inventory.CanAdd(artifact))
                {
                    pool.Add(artifact);
                }
            }

            pools[i] = pool;
            availableCount += pool.Count;
        }

        int count = Math.Min(_rewardTable.CandidateCount, availableCount);
        if (count == 0)
        {
            return true;
        }

        var result = new List<ArtifactData>(count);
        for (int selection = 0; selection < count; selection++)
        {
            // 후보가 남은 등급의 가중치만 합산하여 빈 등급을 다시 뽑는 상황을 방지
            double totalWeight = 0;
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] != null && pools[i].Count > 0)
                {
                    totalWeight += weights[i];
                }
            }

            double roll = _random.NextDouble() * totalWeight;
            int selectedRarity = -1;
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] == null || pools[i].Count == 0)
                {
                    continue;
                }

                selectedRarity = i;
                roll -= weights[i];
                if (roll < 0)
                {
                    break;
                }
            }

            List<ArtifactData> selectedPool = pools[selectedRarity];
            int selectedIndex = _random.Next(selectedPool.Count);
            result.Add(selectedPool[selectedIndex]);
            // 이번 후보 목록에서만 제거. 다음 요청 시 최신 보유 상태로 다시 추첨
            selectedPool.RemoveAt(selectedIndex);
        }

        candidates = result;
        return true;
    }
}
