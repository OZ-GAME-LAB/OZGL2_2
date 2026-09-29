using UnityEngine;

// 소모성 아이템 한 종류의 기본 정보. 보유한 아이템은 슬롯마다 1개씩 관리
[CreateAssetMenu(fileName = "ConsumableItemData", menuName = "Consumables/Consumable Item Data")]
public class ConsumableItemData : ScriptableObject
{
    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;

    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _icon;
}
