using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

// 소모성 아이템 한 종류의 기본 정보. 보유한 아이템은 슬롯마다 1개씩 관리
[CreateAssetMenu(fileName = "ConsumableItemData", menuName = "Consumables/Consumable Item Data")]
public class ConsumableItemData : ScriptableObject
{
    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public ConsumableTargetTeam TargetTeam => _targetTeam;
    public ConsumableTargetMode TargetMode => _targetMode;
    // UI에서 사용 위치와 범위 표시가 필요한 아이템인지 확인합니다.
    public bool IsAreaItem => _targetMode == ConsumableTargetMode.Area;
    public float Radius => _radius;
    public IReadOnlyList<SkillEffectData> Effects => _effects;

    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _icon;

    [Header("사용 대상")]
    [Tooltip("플레이어 기준 적용 대상 팀")]
    [SerializeField] private ConsumableTargetTeam _targetTeam = ConsumableTargetTeam.Ally;
    [SerializeField] private ConsumableTargetMode _targetMode = ConsumableTargetMode.Area;
    [Tooltip("Area 방식에서 선택한 위치를 중심으로 적용할 반경")]
    [SerializeField, Min(0f)] private float _radius = 1f;

    [Header("사용 효과")]
    [SerializeReference] private List<SkillEffectData> _effects = new List<SkillEffectData>();

    private void OnValidate()
    {
        _radius = Mathf.Max(0f, _radius);
    }
}
