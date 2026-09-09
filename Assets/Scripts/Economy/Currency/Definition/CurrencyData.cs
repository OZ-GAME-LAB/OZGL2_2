using UnityEngine;

[CreateAssetMenu(
    fileName = "CurrencyData",
    menuName = "Economy/Currency Data"
)]
public class CurrencyData : ScriptableObject
{
    public string Id => _id;
    public CurrencyType Type => _type;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public CurrencyLifetime Lifetime => _lifetime;

    [SerializeField] private string _id;
    [SerializeField] private CurrencyType _type;
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _icon;
    [SerializeField] private CurrencyLifetime _lifetime;
}
