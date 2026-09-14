using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ArtifactData", menuName = "Artifacts/Artifact Data")]
public class ArtifactData : ScriptableObject
{
    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public ArtifactRarity Rarity => _rarity;
    public int MaxStacks => _maxStacks;
    public IReadOnlyList<UnitStatEffectData> UnitStatEffects => _unitStatEffects;
    public IReadOnlyList<CurrencyEffectData> CurrencyEffects => _currencyEffects;

    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _icon;
    [SerializeField] private ArtifactRarity _rarity;
    [SerializeField, Min(1)] private int _maxStacks = 1;

    [SerializeField] private List<UnitStatEffectData> _unitStatEffects =
        new List<UnitStatEffectData>();
    [SerializeField] private List<CurrencyEffectData> _currencyEffects =
        new List<CurrencyEffectData>();
}
