using System;
using System.Collections.Generic;

// UI와 상점에서 보유 목록 및 교환 후보 조회에 사용
public interface IArtifactReader
{
    bool IsInitialized { get; }
    IReadOnlyList<ArtifactInstance> Instances { get; }
    ArtifactCatalog ArtifactCatalog { get; }

    event Action<ArtifactInstance, int, int> StackChanged;
    event Action<IReadOnlyList<ArtifactInstance>> Cleared;

    bool Contains(ArtifactData artifact);
    bool TryGetById(string id, out ArtifactInstance instance);
    List<ArtifactInstance> GetExchangeCandidates(ArtifactData rewardArtifact);
}
