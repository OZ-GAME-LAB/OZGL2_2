using UnityEngine;

// 소모성 아이템 한 종류의 기본 정보. 현재 보유 수량은 런타임에서 별도 관리
[CreateAssetMenu(fileName = "ConsumableItemData", menuName = "Consumables/Consumable Item Data")]
public class ConsumableItemData : ScriptableObject
{
    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public int MaxCount => _maxCount;

    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _icon;

    // 같은 종류의 아이템을 보유할 수 있는 최대 수량
    [SerializeField, Min(1)] private int _maxCount = 1;
}
