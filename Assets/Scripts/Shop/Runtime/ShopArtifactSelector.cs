using System;
using System.Collections.Generic;

// 상점 구매·교환 품목 추첨. 보유 아티팩트와 재화 변경 X
public class ShopArtifactSelector
{
    private readonly ArtifactCatalog _catalog;
    private readonly ArtifactManager _artifactManager;
    private readonly ShopTable _table;
    private readonly Random _random;

    // 이번 추첨에서만 사용하는 등급별 후보와 설정
    private class RarityPool
    {
        public List<ArtifactData> Artifacts;
        public int Weight;
        public int MinPrice;
        public int MaxPrice;
    }

    public ShopArtifactSelector(ArtifactCatalog catalog, ArtifactManager artifactManager,
        ShopTable table, Random random = null)
    {
        _catalog = catalog;
        _artifactManager = artifactManager;
        _table = table;
        _random = random ?? new Random();
    }

    // 설정 오류는 false, 후보 부족은 true와 가능한 수량만 반환
    public bool TryCreatePurchaseSlots(out List<ShopArtifactSlot> slots)
    {
        slots = new List<ShopArtifactSlot>();
        if (!IsReady() || _table.ArtifactSlotCount < 1 || _table.ShopArtifactTable == null ||
            _table.ShopArtifactTable.RaritySettings == null ||
            (_table.PurchaseCurrency != CurrencyType.Gold &&
             _table.PurchaseCurrency != CurrencyType.Gem &&
             _table.PurchaseCurrency != CurrencyType.Bloodstone))
        {
            return false;
        }

        var pools = new List<RarityPool>();
        var registered = new HashSet<ArtifactRarity>();
        foreach (ShopArtifactRarityData setting in _table.ShopArtifactTable.RaritySettings)
        {
            if (setting == null || setting.Weight < 0 || setting.MinPrice < 0 ||
                setting.MaxPrice < setting.MinPrice || !registered.Add(setting.Rarity))
            {
                return false;
            }

            if (setting.Weight == 0)
            {
                continue;
            }

            pools.Add(new RarityPool
            {
                Artifacts = GetCandidates(setting.Rarity, false),
                Weight = setting.Weight,
                MinPrice = setting.MinPrice,
                MaxPrice = setting.MaxPrice
            });
        }

        for (int i = 0; i < _table.ArtifactSlotCount; i++)
        {
            RarityPool pool = SelectPool(pools);
            if (pool == null)
            {
                break;
            }

            ArtifactData artifact = TakeArtifact(pool);
            // 최대 가격이 int.MaxValue여도 넘치지 않도록 범위 계산은 long 사용
            long priceCount = (long)pool.MaxPrice - pool.MinPrice + 1;
            int price = (int)(pool.MinPrice + (long)(_random.NextDouble() * priceCount));
            slots.Add(new ShopArtifactSlot(artifact, _table.PurchaseCurrency, price));
        }

        return true;
    }

    // 구매 목록과 별도로 추첨. 구매 품목과 교환 품목 사이의 중복은 허용
    public bool TryCreateExchangeSlots(out List<ShopArtifactExchangeSlot> slots)
    {
        slots = new List<ShopArtifactExchangeSlot>();
        if (!IsReady() || _table.ShopArtifactExchangeTable == null)
        {
            return false;
        }

        ShopArtifactExchangeTable exchangeTable = _table.ShopArtifactExchangeTable;
        if (exchangeTable.SlotCount < 1 || exchangeTable.RaritySettings == null)
        {
            return false;
        }

        var pools = new List<RarityPool>();
        var registered = new HashSet<ArtifactRarity>();
        foreach (ShopArtifactExchangeRarityData setting in exchangeTable.RaritySettings)
        {
            if (setting == null || setting.Weight < 0 || !registered.Add(setting.Rarity))
            {
                return false;
            }

            if (setting.Weight == 0)
            {
                continue;
            }

            pools.Add(new RarityPool
            {
                Artifacts = GetCandidates(setting.Rarity, exchangeTable.OnlyExchangeableRarities),
                Weight = setting.Weight
            });
        }

        for (int i = 0; i < exchangeTable.SlotCount; i++)
        {
            RarityPool pool = SelectPool(pools);
            if (pool == null)
            {
                break;
            }

            slots.Add(new ShopArtifactExchangeSlot(TakeArtifact(pool)));
        }

        return true;
    }

    private bool IsReady()
    {
        return _catalog != null && _artifactManager != null && _artifactManager.IsInitialized && _table != null;
    }

    private List<ArtifactData> GetCandidates(ArtifactRarity rarity, bool onlyExchangeable)
    {
        var candidates = new List<ArtifactData>();
        foreach (ArtifactData artifact in _catalog.GetByRarity(rarity))
        {
            if (artifact == null || artifact.MaxStacks < 1)
            {
                continue;
            }

            if (_artifactManager.TryGetById(artifact.Id, out ArtifactInstance instance) &&
                (instance.Data != artifact || instance.StackCount >= artifact.MaxStacks))
            {
                continue;
            }

            if (onlyExchangeable && _artifactManager.GetExchangeCandidates(artifact).Count == 0)
            {
                continue;
            }

            candidates.Add(artifact);
        }

        return candidates;
    }

    // 후보가 남은 등급에만 가중치 적용. 등급 내 아티팩트 수는 등급 확률에 영향 X
    private RarityPool SelectPool(List<RarityPool> pools)
    {
        double totalWeight = 0;
        foreach (RarityPool pool in pools)
        {
            if (pool.Artifacts.Count > 0)
            {
                totalWeight += pool.Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        double roll = _random.NextDouble() * totalWeight;
        RarityPool selected = null;
        foreach (RarityPool pool in pools)
        {
            if (pool.Artifacts.Count == 0)
            {
                continue;
            }

            selected = pool;
            roll -= pool.Weight;
            if (roll < 0)
            {
                break;
            }
        }

        return selected;
    }

    private ArtifactData TakeArtifact(RarityPool pool)
    {
        int index = _random.Next(pool.Artifacts.Count);
        ArtifactData artifact = pool.Artifacts[index];
        // 이번 목록에서만 제거하여 중복 방지
        pool.Artifacts.RemoveAt(index);
        return artifact;
    }
}
