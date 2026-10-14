using System.Collections.Generic;
using UnityEngine;

// Economy 테스트와 같은 매니저를 사용하는 독립 상점 디버그 창
public class ShopTestPanel : MonoBehaviour
{
    [SerializeField] private ShopManager _shop;
    [SerializeField] private RunCurrencyManager _run;
    [SerializeField] private ArtifactManager _artifacts;

    private Vector2 _scroll;
    private string _result = "기존 재화 패널에서 Run 시작 후 상점을 생성하세요.";
    private ShopArtifactExchangeSlot _selectedSlot;
    private ArtifactData _selectedMaterial;

    private bool _isOpen;
    private System.Action _completed;

    public bool TryOpen(System.Action completed)
    {
        if (_isOpen || _shop == null || _run == null || _artifacts == null ||
            !_run.IsInitialized || !_artifacts.IsInitialized)
        {
            return false;
        }
        _shop.Initialize(_artifacts, _run);
        if (!_shop.TryGenerateStock())
        {
            return false;
        }
        ClearSelection();
        _isOpen = true;
        _completed = completed;
        _result = "구매·교환·판매 후 상점 종료를 누르세요.";
        return true;
    }

    public void ResetPanel()
    {
        _isOpen = false;
        _completed = null;
        ClearSelection();
        if (_shop != null)
        {
            _shop.TryEndRun();
        }
    }

    public void DrawPanel()
    {
        GUILayout.Label("상점");
        if (!_isOpen)
        {
            GUILayout.Label("재화 패널에서 이번 웨이브 = 상점을 선택하세요.");
            return;
        }
        if (GUILayout.Button("상점 종료 → 다음 웨이브"))
        {
            var completed = _completed;
            _isOpen = false;
            _completed = null;
            ClearSelection();
            completed?.Invoke();
            GUIUtility.ExitGUI();
        }
        _scroll = GUILayout.BeginScrollView(_scroll);
        DrawContents();
        GUILayout.EndScrollView();
    }
    private void DrawContents()
    {
        if (_shop == null || _run == null || _artifacts == null)
        {
            GUILayout.Label("ShopManager, RunCurrencyManager, ArtifactManager 연결 필요");
            return;
        }

        if (!_run.IsInitialized || !_artifacts.IsInitialized)
        {
            GUILayout.Label("기존 재화 패널의 Run 시작 버튼을 먼저 누르세요.");
            return;
        }

        GUILayout.Label($"공유 골드: {_run.GetBalance(CurrencyType.Gold)} / 보석: {_run.GetBalance(CurrencyType.Gem)}");
        GUILayout.Label(_result);
        if (!_shop.IsInitialized)
        {
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label($"구매 품목 {_shop.PurchaseSlots.Count}종");
        foreach (ShopArtifactSlot slot in _shop.PurchaseSlots)
        {
            GUILayout.Label($"{Describe(slot.Artifact)} / {slot.Currency} {slot.Price}");
            if (slot.IsPurchased)
            {
                GUILayout.Label("구매 완료");
            }
            else if (GUILayout.Button("구매"))
            {
                Report("구매", _shop.TryPurchase(slot));
            }
        }

        GUILayout.Space(8);
        GUILayout.Label("교환 품목 — 클릭 후 재료 선택");
        foreach (ShopArtifactExchangeSlot slot in _shop.ExchangeSlots)
        {
            if (slot.IsExchanged)
            {
                GUILayout.Label($"{Describe(slot.Artifact)} / 교환 완료");
            }
            else if (GUILayout.Button(Describe(slot.Artifact)))
            {
                _selectedSlot = slot;
                _selectedMaterial = null;
                GUIUtility.ExitGUI();
            }
        }

        if (_selectedSlot != null && !_selectedSlot.IsExchanged)
        {
            var candidates = _shop.GetExchangeCandidates(_selectedSlot);
            bool selectedExists = false;
            foreach (ArtifactInstance instance in candidates)
            {
                bool selected = instance.Data == _selectedMaterial;
                selectedExists |= selected;
                if (GUILayout.Button($"{(selected ? "[선택] " : "")}{Describe(instance.Data)} / 보유 {instance.StackCount}"))
                {
                    _selectedMaterial = instance.Data;
                    GUIUtility.ExitGUI();
                }
            }
            if (candidates.Count == 0)
            {
                GUILayout.Label("동일 등급의 교환 재료가 없거나 교환 품목이 최대 중첩입니다.");
            }
            if (selectedExists && GUILayout.Button("선택한 재료 1개로 교환 확정"))
            {
                bool succeeded = _shop.TryExchange(_selectedSlot, _selectedMaterial);
                if (succeeded)
                {
                    ClearSelection();
                }
                Report("교환", succeeded);
            }
        }

        GUILayout.Space(8);
        GUILayout.Label($"공유 보유 아티팩트 {_artifacts.Instances.Count}종 — 판매는 중첩 1개씩");
        // 판매 후 목록이 바뀌므로 화면에 표시할 목록만 복사
        var owned = new List<ArtifactInstance>(_artifacts.Instances);
        foreach (ArtifactInstance instance in owned)
        {
            GUILayout.Label($"{Describe(instance.Data)} / 보유 {instance.StackCount}");
            if (_shop.TryGetSellPrice(instance.Data, out int price))
            {
                if (GUILayout.Button($"1개 판매 / 골드 +{price}"))
                {
                    Report("판매", _shop.TrySell(instance.Data));
                }
            }
            else
            {
                GUILayout.Label("판매가 설정 확인 필요");
            }
        }
    }

    private string Describe(ArtifactData artifact)
    {
        return artifact == null ? "미설정" : $"{artifact.DisplayName} ({artifact.Rarity})";
    }

    private void ClearSelection()
    {
        _selectedSlot = null;
        _selectedMaterial = null;
    }

    private void Report(string action, bool succeeded)
    {
        _result = $"{action}: {(succeeded ? "성공" : "실패 — 잔액·중첩·설정 확인")}";
        Debug.Log($"[Shop/Test] {_result}", this);
        // 버튼 처리로 목록 크기가 바뀌어도 현재 GUI 순회를 계속하지 않음
        GUIUtility.ExitGUI();
    }
}
