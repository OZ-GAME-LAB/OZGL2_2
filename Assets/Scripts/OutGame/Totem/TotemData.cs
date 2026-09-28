using UnityEngine;

/// <summary>
/// 토템의 고정 표시 정보입니다. 현재 선택 레벨은 런 준비 상태에서 별도로 관리합니다.
/// 이 데이터는 전투 효과를 실행하지 않습니다.
/// </summary>
[CreateAssetMenu(fileName = "TotemData", menuName = "OutGame/Totem Data")]
public class TotemData : ScriptableObject
{
    public TotemId Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public TotemSelectionMode SelectionMode => _selectionMode;
    public string EffectLabel => _effectLabel;
    public float ValuePerLevel => _valuePerLevel;
    public string ValueUnit => _valueUnit;
    public int MaxLevel => _maxLevel;
    public float RewardBonusPerLevel => _rewardBonusPerLevel;

    [SerializeField] private TotemId _id;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;

    [SerializeField] private TotemSelectionMode _selectionMode;
    [SerializeField] private string _effectLabel;

    [Tooltip("UI 표시 단위입니다. 15와 %를 조합하면 15%입니다. 감소 효과도 양수로 입력합니다. ON/OFF형에서는 활성화 시 표시할 수치입니다.")]
    [SerializeField, Min(0f)] private float _valuePerLevel;
    [Tooltip("수치 뒤에 표시할 단위입니다. 예: %, 명, 회, 발. 단위가 없으면 비워둡니다.")]
    [SerializeField] private string _valueUnit;
    [Tooltip("레벨형의 선택 상한입니다. ON/OFF형은 항상 1입니다.")]
    [SerializeField, Min(1)] private int _maxLevel = 3;

    [Tooltip("선택 레벨당 표시할 보상 보너스(%)입니다. 10은 10%입니다. ON/OFF형에서는 활성화 시 보너스입니다.")]
    [SerializeField, Min(0f)] private float _rewardBonusPerLevel;

    private void OnValidate()
    {
        _maxLevel = _selectionMode == TotemSelectionMode.Toggle ? 1 : Mathf.Max(1, _maxLevel);
        _valuePerLevel = Mathf.Max(0f, _valuePerLevel);
        _rewardBonusPerLevel = Mathf.Max(0f, _rewardBonusPerLevel);
    }
}
