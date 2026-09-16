using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ArtifactCatalog", menuName = "Artifacts/Artifact Catalog")]
public class ArtifactCatalog : ScriptableObject
{
    public IReadOnlyList<ArtifactData> Artifacts => _registeredArtifacts;

    [SerializeField] private List<ArtifactData> _artifacts = new List<ArtifactData>();

    private readonly Dictionary<string, ArtifactData> _artifactById =
        new Dictionary<string, ArtifactData>();
    private readonly Dictionary<ArtifactRarity, List<ArtifactData>> _artifactsByRarity =
        new Dictionary<ArtifactRarity, List<ArtifactData>>();
    private List<ArtifactData> _registeredArtifacts = new List<ArtifactData>();

    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public bool TryGetById(string id, out ArtifactData artifact)
    {
        artifact = null;
        return !string.IsNullOrWhiteSpace(id) && _artifactById.TryGetValue(id, out artifact);
    }

    // 등급별 등록 목록 조회 (후보가 없으면 빈 목록 반환)
    public IReadOnlyList<ArtifactData> GetByRarity(ArtifactRarity rarity)
    {
        return _artifactsByRarity.TryGetValue(rarity, out List<ArtifactData> artifacts)
            ? artifacts
            : new List<ArtifactData>();
    }

    public bool Contains(ArtifactData artifact)
    {
        return artifact != null && TryGetById(artifact.Id, out ArtifactData registered)
            && registered == artifact;
    }

    // 등록 목록을 검증하고 ID·등급별 조회 캐시를 구성
    private void BuildLookup()
    {
        _artifactById.Clear();
        _artifactsByRarity.Clear();
        _registeredArtifacts = new List<ArtifactData>();

        if (_artifacts == null)
        {
            Debug.LogError("[Artifacts/ArtifactCatalog] 아티팩트 목록이 null입니다.", this);
            return;
        }

        for (int i = 0; i < _artifacts.Count; i++)
        {
            ArtifactData artifact = _artifacts[i];
            if (artifact == null)
            {
                Debug.LogError($"[Artifacts/ArtifactCatalog] 비어 있는 아티팩트가 있습니다. Index: {i}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(artifact.Id))
            {
                Debug.LogError($"[Artifacts/ArtifactCatalog] 아티팩트 ID가 비어 있습니다. Asset: {artifact.name}", artifact);
                continue;
            }

            // 동일 ID는 먼저 등록된 유효한 항목만 사용하여 추첨 목록 중복을 방지
            if (_artifactById.ContainsKey(artifact.Id))
            {
                Debug.LogError($"[Artifacts/ArtifactCatalog] 중복된 아티팩트 ID입니다. ID: {artifact.Id}", artifact);
                continue;
            }

            _artifactById.Add(artifact.Id, artifact);
            _registeredArtifacts.Add(artifact);

            if (!_artifactsByRarity.TryGetValue(artifact.Rarity, out List<ArtifactData> rarityArtifacts))
            {
                rarityArtifacts = new List<ArtifactData>();
                _artifactsByRarity.Add(artifact.Rarity, rarityArtifacts);
            }

            rarityArtifacts.Add(artifact);
        }

    }
}
