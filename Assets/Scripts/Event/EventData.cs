using System;
using System.Collections.Generic;
using UnityEngine;

// 이벤트 ID: 20000~20999. 저장 호환성을 위해 기존 숫자는 변경하거나 재사용하지 않는다.
public enum EventId
{
    None = 0,
    AbandonedAltar = 20000,
    LostTreasure = 20001,
    ArtifactExchange = 20002,
    GoldWager = 20003,
    ArtifactTrader = 20004
}

public enum EventResourceType
{
    None,
    Gold,
    Artifact
}

public enum EventConditionType
{
    HasArtifact,
    MissingArtifact
}

[Serializable]
public sealed class EventConditionData
{
    [SerializeField] private EventConditionType _type;
    [SerializeField] private ArtifactData _artifact;

    public EventConditionType Type => _type;
    public ArtifactData Artifact => _artifact;
}

[Serializable]
public sealed class EventCostData
{
    [SerializeField, Min(0)] private int _gold;
    [Tooltip("목록의 항목마다 1개씩 소모합니다. 같은 유물을 여러 번 넣으면 그 수량만큼 필요합니다.")]
    [SerializeField] private ArtifactData[] _artifacts = Array.Empty<ArtifactData>();

    public int Gold => _gold;
    public IReadOnlyList<ArtifactData> Artifacts => _artifacts;
}

[Serializable]
public sealed class EventResultData
{
    [SerializeField] private EventResourceType _type;
    [SerializeField, Min(0)] private int _gold;
    [SerializeField] private ArtifactData _artifact;

    public EventResourceType Type => _type;
    public int Gold => _gold;
    public ArtifactData Artifact => _artifact;

    public EventResultData() { }

    public EventResultData(EventResourceType type, int gold = 0, ArtifactData artifact = null)
    {
        _type = type;
        _gold = gold;
        _artifact = artifact;
    }
}

[Serializable]
public sealed class EventChoiceData
{
    [SerializeField] private string _title;
    [SerializeField, TextArea] private string _description;
    [Tooltip("모든 조건을 만족해야 선택할 수 있습니다. 조건에 지정한 유물은 소모하지 않습니다.")]
    [SerializeField] private EventConditionData[] _conditions = Array.Empty<EventConditionData>();
    [SerializeField] private EventCostData _cost = new EventCostData();
    [SerializeField] private EventActionSO _action;

    public string Title => _title;
    public string Description => _description;
    public IReadOnlyList<EventConditionData> Conditions => _conditions;
    public EventCostData Cost => _cost;
    public EventActionSO Action => _action;
}

[CreateAssetMenu(fileName = "EventData", menuName = "Scriptable Objects/EventData")]
public sealed class EventData : ScriptableObject
{
    [SerializeField] private EventId _id;
    [SerializeField] private string _title;
    [SerializeField] private Sprite _image;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private EventChoiceData[] _choices = Array.Empty<EventChoiceData>();

    public EventId Id => _id;
    public string Title => _title;
    public string Description => _description;
    public Sprite Image => _image;
    public IReadOnlyList<EventChoiceData> Choices => _choices;
}
