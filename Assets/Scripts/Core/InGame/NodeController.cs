using System;
using System.Collections.Generic;
using Units;
using UnityEditor.SceneManagement;

namespace Game.Core
{
    public enum PostBattleEventType { None, Event, Shop, Curse }

    public sealed class Node
    {
        public int WaveNumber { get; }
        public WaveSO Preset { get; }
        public EnemyUnitFaction Faction { get; }
        public PostBattleEventType PostBattleEvent { get; }
        public WaveBattleType BattleType => Preset.BattleType;

        public Node(int waveNumber, WaveSO preset, EnemyUnitFaction faction,
            PostBattleEventType postBattleEvent)
        {
            WaveNumber = waveNumber;
            Preset = preset ?? throw new ArgumentNullException(nameof(preset));
            Faction = faction;
            PostBattleEvent = postBattleEvent;
        }
    }

    public class NodeSaveData
    {
        public List<Node> nodes;
        public int currentNodeCount;
    }
    
    /// <summary>분기별 노드·편성·부착 이벤트 생성, 현재 위치 보관.
    /// <br/>GameFlow의 요청에 따라 실제 노드 인덱스 변경
    /// <br/>* 실제 전투를 하거나 이벤트 진행같은것은 하지 않는다. </summary>
    public sealed class NodeController : ISaveDataProvider<NodeSaveData>
    {
        public const int WavesPerQuarter = 5;
        public const int MainQuarters = 3;
        
        private readonly WaveSODictionary _catalog;
        private readonly Random _random;
        public IReadOnlyList<Node> Nodes { get; private set; } = Array.Empty<Node>();
        public int CurrentQuarter { get; private set; }
        public int CurrentIndex { get; private set; } = -1;
        public Node CurrentNode => CurrentIndex >= 0 ? Nodes[CurrentIndex] : null;
        public bool IsLastNode => CurrentNode != null && CurrentIndex == Nodes.Count - 1;
        public NodeSaveData CaptureSaveData()
        {
            NodeSaveData saveData = new NodeSaveData();
            foreach (var node in Nodes)
            {
                saveData.nodes.Add(node);
            }
            return saveData;
        }
        
        public void RestoreSaveData(NodeSaveData data)
        {
            var nodes = new Node[WavesPerQuarter];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = data.nodes[i];
            }
        }
        public NodeController(WaveSODictionary catalog, Random random = null)
        {
            _catalog = catalog;
            _random = random ?? new Random();
        }

        public void Reset()
        {
            Nodes = Array.Empty<Node>();
            CurrentQuarter = 0;
            CurrentIndex = -1;
        }

        // 전부 생성된 경우에만 현재 계획을 교체한다.
        public bool TryStartQuarter(int quarter, float twoEliteChance, out string error)
        {
            error = null;
            if (quarter < 1 || _catalog == null)
            {
                error = "분기 번호 또는 웨이브 카탈로그가 유효하지 않습니다.";
                return false;
            }
            //노드별 전투종류를 설정하며, 마지막 5번째에는 보스가 확정적으로 등장한다
            var types = new WaveBattleType[WavesPerQuarter];
            types[4] = WaveBattleType.Boss;
            int eliteCount = _random.NextDouble() < twoEliteChance ? 2 : 1;
            var middle = new List<int> { 1, 2, 3 };
            for (int i = 0; i < eliteCount; i++)
            {
                int pick = _random.Next(middle.Count);
                types[middle[pick]] = WaveBattleType.Elite;
                middle.RemoveAt(pick);
            }
            //이벤트 리스트 붙이기
            //3~4웨이브 중 하나에 상점 / 1~4웨이브중 하나에 이벤트배치 + 나머지 노드에는 10%로 이벤트 부착 
            var events = new PostBattleEventType[WavesPerQuarter];
            int shop = _random.Next(2, 4);
            events[shop] = PostBattleEventType.Shop;
            var remaining = new List<int> { 0, 1, 2, 3 };
            remaining.Remove(shop);
            int guaranteedEvent = _random.Next(remaining.Count);
            events[remaining[guaranteedEvent]] = PostBattleEventType.Event;
            remaining.RemoveAt(guaranteedEvent);
            foreach (int index in remaining)
                if (_random.NextDouble() < 0.1)
                    events[index] = PostBattleEventType.Event;
            if (quarter > MainQuarters)
                events[4] = PostBattleEventType.Curse;
            //결정된 이벤트와 전투 타입을 실제 노드로 만들기
            var nodes = new Node[WavesPerQuarter];
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!_catalog.TryGetRandomWaveSO(Math.Min(quarter, 5), types[i],
                    out var preset, out var faction))
                {
                    error = $"프리셋 후보 누락: 분기 {quarter}, 웨이브 {i + 1}, 타입 {types[i]}";
                    return false;
                }
                nodes[i] = new Node(i + 1, preset, faction, events[i]);
            }
            Nodes = Array.AsReadOnly(nodes);
            CurrentQuarter = quarter;
            CurrentIndex = 0;
            return true;
        }
        
        public bool MoveNext()
        {
            if (CurrentNode == null || IsLastNode) return false;
            CurrentIndex++;
            return true;
        }

        public bool JumpToLastNode()
        {
            if (CurrentNode == null || IsLastNode) return false;
            CurrentIndex = Nodes.Count - 1;
            return true;
        }
    }
}
