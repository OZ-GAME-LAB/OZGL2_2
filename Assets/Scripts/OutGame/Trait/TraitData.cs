using UnityEngine;

/// <summary>
/// 영구 특성의 고정 표시 정보입니다. 해금 여부와 현재 레벨은 영구 저장 상태에서 관리합니다.
/// 업그레이드 비용은 레벨에 관계없이 고정된 UI 테스트 값입니다.
/// </summary>
[CreateAssetMenu(fileName = "TraitData", menuName = "OutGame/Trait Data")]
public class TraitData : ScriptableObject
{
    public TraitId Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public string EffectLabel => _effectLabel;
    public float ValuePerLevel => _valuePerLevel;
    public string ValueUnit => _valueUnit;
    public int MaxLevel => _maxLevel;
    public int UpgradeCost => _upgradeCost;
    public TraitData Prerequisite => _prerequisite;

    [SerializeField] private TraitId _id;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;

    [SerializeField] private string _effectLabel;
    [Tooltip("UI 표시 단위입니다. 5와 %를 조합하면 5%이며, 누적 표시는 이 값에 현재 레벨을 곱합니다.")]
    [SerializeField, Min(0f)] private float _valuePerLevel;
    [Tooltip("수치 뒤에 표시할 단위입니다. 예: %, 골드. 단위가 없으면 비워둡니다.")]
    [SerializeField] private string _valueUnit;
    [SerializeField, Min(1)] private int _maxLevel = 5;

    [Tooltip("한 레벨을 올리는 데 필요한 고정 혈석 비용입니다. 재화 차감과 저장은 특성 서비스가 담당합니다.")]
    [SerializeField, Min(0)] private int _upgradeCost = 200;

    [Tooltip("선행 특성입니다. 비워두면 바로 구매할 수 있고, 연결하면 해당 특성 1레벨이 필요합니다.")]
    [SerializeField] private TraitData _prerequisite;

    private void OnValidate()
    {
        _maxLevel = Mathf.Max(1, _maxLevel);
        _valuePerLevel = Mathf.Max(0f, _valuePerLevel);
        _upgradeCost = Mathf.Max(0, _upgradeCost);
    }
}
