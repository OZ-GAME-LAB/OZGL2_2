using System;
using System.Collections.Generic;

// Run의 아티팩트 보유 상태 관리 (효과 등록 및 획득 알림은 Manager에서 처리)
public class ArtifactInventory
{
    public IReadOnlyList<ArtifactInstance> Instances => _instances;

    // 전체 제거 완료 후 제거된 Instance 목록 전달 (Source별 효과 해제에 사용)
    public event Action<IReadOnlyList<ArtifactInstance>> Cleared;

    private readonly ArtifactCatalog _catalog;
    private readonly List<ArtifactInstance> _instances = new List<ArtifactInstance>();
    private readonly Dictionary<string, ArtifactInstance> _instanceById =
        new Dictionary<string, ArtifactInstance>();

    public ArtifactInventory(ArtifactCatalog catalog)
    {
        if (catalog == null)
        {
            UnityEngine.Debug.LogError("[Artifacts/ArtifactInventory] Catalog가 없습니다. 아티팩트를 획득할 수 없습니다.");
        }

        _catalog = catalog;
    }

    public bool TryGetById(string id, out ArtifactInstance instance)
    {
        instance = null;
        return !string.IsNullOrWhiteSpace(id) && _instanceById.TryGetValue(id, out instance);
    }

    public bool Contains(ArtifactData artifact)
    {
        ArtifactInstance instance;
        return artifact != null && TryGetById(artifact.Id, out instance)
            && instance.Data == artifact;
    }

    // 획득 가능 여부 확인, 실제 획득 X (최대 중첩 아티팩트의 후보 제외에 사용)
    public bool CanAdd(ArtifactData artifact)
    {
        if (_catalog == null || !_catalog.Contains(artifact) || artifact.MaxStacks < 1)
        {
            return false;
        }

        ArtifactInstance instance;
        return !_instanceById.TryGetValue(artifact.Id, out instance)
            || (instance.Data == artifact && instance.StackCount < artifact.MaxStacks);
    }

    // 아티팩트 1개 획득. 실패 시 보유 상태 변경 X
    public bool TryAdd(ArtifactInstance instance)
    {
        if (instance == null)
        {
            return false;
        }

        ArtifactData artifact = instance.Data;
        if (!CanAdd(artifact))
        {
            return false;
        }

        if (_instanceById.TryGetValue(artifact.Id, out ArtifactInstance registered))
        {
            if (registered != instance)
            {
                return false;
            }
            if (!instance.TryIncreaseStack())
            {
                return false;
            }
        }
        else
        {
            if (instance.StackCount != 1)
            {
                return false;
            }
            _instanceById.Add(artifact.Id, instance);
            _instances.Add(instance);
        }

        return true;
    }

    // 보유한 Instance의 중첩 1개 차감. 마지막 중첩이면 목록에서도 제거
    public bool TryRemove(ArtifactInstance instance)
    {
        if (instance == null || instance.Data == null ||
            !TryGetById(instance.Data.Id, out ArtifactInstance registered) || registered != instance)
        {
            return false;
        }

        if (!instance.TryDecreaseStack())
        {
            return false;
        }

        if (instance.StackCount == 0)
        {
            _instanceById.Remove(instance.Data.Id);
            _instances.Remove(instance);
        }

        return true;
    }

    // 교환 조건을 모두 확인한 뒤 지급·차감. 처리 중 이벤트 발생 X
    public bool TryExchange(ArtifactInstance owned, ArtifactInstance reward)
    {
        if (owned == null || reward == null || owned.Data == null || reward.Data == null ||
            owned.Data == reward.Data || owned.Data.Rarity != reward.Data.Rarity || owned.StackCount < 1 ||
            !TryGetById(owned.Data.Id, out ArtifactInstance registered) || registered != owned)
        {
            return false;
        }

        // 지급 실패 시 보유 아티팩트 차감 X. 성공 후에는 위에서 확인한 Instance만 차감
        if (!TryAdd(reward))
        {
            return false;
        }

        TryRemove(owned);
        return true;
    }

    // Run 종료 시 호출 : 보유 목록 제거 (이미 비어 있으면 이벤트 발생 X)
    public void Clear()
    {
        if (_instances.Count == 0)
        {
            return;
        }

        IReadOnlyList<ArtifactInstance> removed = _instances.ToArray();
        _instances.Clear();
        _instanceById.Clear();
        Cleared?.Invoke(removed);
    }
}
