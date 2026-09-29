using UnityEngine;

// 재화 테스트 패널과 같은 Run 및 효과 매니저를 사용하는 소모성 아이템 테스트
public class ConsumableItemTestPanel : MonoBehaviour
{
    public ConsumableItemManager Items => _items;
    [SerializeField] private ConsumableItemManager _items;
    private EffectManager _effects;
    private ArtifactManager _artifacts;
    private Vector2 _scroll;

    private string _result = "재화 패널에서 Run을 시작하세요.";

    public void Initialize(EffectManager effects, ArtifactManager artifacts, bool newRun)
    {
        _effects = effects;
        _artifacts = artifacts;
        if (_items == null)
        {
            _result = "ConsumableItemManager를 연결해주세요.";
            return;
        }
        if (!_items.IsInitialized)
        {
            _items.Initialize(effects);
        }
        else if (newRun)
        {
            ResetPanel();
        }
        _result = _items.IsInitialized ? "아이템 추가 후 슬롯 감소를 테스트하세요." : "Catalog 연결을 확인해주세요.";
    }

    public void ResetPanel()
    {

        if (_items != null && _items.IsInitialized)
        {
            _items.ResetRun();
        }
        _result = "테스트 상태 초기화";
    }

    public void DrawPanel(bool runActive)
    {
        GUILayout.Label("소모성 아이템 / 슬롯");
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label(_result);
        if (!runActive || _items == null || !_items.IsInitialized)
        {
            GUILayout.Label("Run 시작 및 Manager·Catalog 연결을 확인하세요.");
            GUILayout.EndScrollView();
            return;
        }

        GUILayout.Label($"허용 {_items.Capacity}칸 / 실제 {_items.SlotCount}칸 / 보유 {_items.ItemCount}개");
        GUILayout.Label($"초과 {_items.OverflowCount}개 / 추가 가능: {_items.HasEmptySlot}");
        GUILayout.Label($"효과 보정: {(_effects != null ? _effects.ConsumableSlotAdjustment : 0)}");
        for (int i = 0; i < _items.Slots.Count; i++)
        {
            IConsumableItemSlotReader slot = _items.Slots[i];
            string name = slot.IsEmpty ? "빈칸" : slot.Item.DisplayName;
            GUILayout.Label($"슬롯 {i + 1}: {name}{(i >= _items.Capacity ? " (초과 슬롯)" : "")}");
            if (!slot.IsEmpty && GUILayout.Button($"슬롯 {i + 1} 버리기"))
            {
                _result = _items.TryRemove(i) ? "아이템 제거 완료" : "제거 실패";
                GUIUtility.ExitGUI();
            }
        }

        GUILayout.Label("Catalog 아이템 추가 (재화 소비 없음)");
        foreach (ConsumableItemData item in _items.Catalog.Items)
        {
            if (GUILayout.Button($"{item.DisplayName} 추가"))
            {
                _result = _items.TryAdd(item) ? "추가 성공" : "추가 실패: 빈 슬롯 또는 초과 상태 확인";
                GUIUtility.ExitGUI();
            }
        }

        GUILayout.Label("슬롯 수 직접 변경 (효과 변경 시 다시 계산됨)");
        if (GUILayout.Button("허용 슬롯 -1"))
        {
            _items.TrySetCapacity(Mathf.Max(0, _items.Capacity - 1));
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("허용 슬롯 +1"))
        {
            _items.TrySetCapacity(_items.Capacity + 1);
            GUIUtility.ExitGUI();
        }
        GUILayout.Label("슬롯 효과 아티팩트 획득 / 중첩 1개 제거");
        if (_artifacts != null && _artifacts.IsInitialized)
        {
            foreach (ArtifactData artifact in _artifacts.ArtifactCatalog.Artifacts)
            {
                if (artifact.ConsumableSlotEffects == null || artifact.ConsumableSlotEffects.Count == 0)
                {
                    continue;
                }
                if (GUILayout.Button($"{artifact.DisplayName} 획득"))
                {
                    _result = _artifacts.TryAdd(artifact) ? "아티팩트 획득 완료" : "획득 실패";
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button($"{artifact.DisplayName} 1개 제거"))
                {
                    _result = _artifacts.TryRemove(artifact) ? "아티팩트 제거 완료" : "제거 실패";
                    GUIUtility.ExitGUI();
                }
            }
        }
        GUILayout.EndScrollView();
    }

}
