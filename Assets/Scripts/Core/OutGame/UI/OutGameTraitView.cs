using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TraitButtonSlot
{
    public TraitId Id;
    public OutGameChoiceButton Button;
}

/// <summary>특성 노드는 미리보기만 변경하고, 구매 버튼이 업그레이드를 요청합니다.</summary>
public class OutGameTraitView : MonoBehaviour
{
    [SerializeField] private TraitButtonSlot[] _slots;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _level;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private TMP_Text _walletLabel;
    [SerializeField] private TMP_Text _upgradeLabel;
    [SerializeField] private Image _icon;
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private Button _backButton;

    private ITraitProgression _progression;
    private PersistentCurrencyManager _wallet;
    private TraitId _inspectedTrait;

    public event Action<TraitId> UpgradeRequested;
    public event Action BackRequested;

    public bool ValidateReferences()
    {
        bool valid = _slots != null && _slots.Length > 0 && _title != null && _level != null &&
            _description != null && _walletLabel != null && _upgradeLabel != null &&
            _icon != null && _upgradeButton != null && _backButton != null;
        if (valid)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i].Button == null ||
                    !_slots[i].Button.ValidateReferences()) valid = false;
            }
        }
        if (!valid) Debug.LogError("[OutGameTraitView] Inspector의 슬롯과 UI 참조를 확인해주세요.", this);
        return valid;
    }

    public void Initialize(ITraitProgression progression, PersistentCurrencyManager wallet)
    {
        Shutdown();
        if (!ValidateReferences() || progression == null || wallet == null) return;
        _progression = progression;
        _wallet = wallet;
        _inspectedTrait = TraitId.StartingGold;
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Button.Initialize();
            _slots[i].Button.Clicked += OnTraitClicked;
        }
        _upgradeLabel.richText = true;
        _upgradeButton.onClick.AddListener(OnUpgradeClicked);
        _backButton.onClick.AddListener(OnBackClicked);
        Refresh();
    }

    public void Refresh()
    {
        if (_progression == null || _wallet == null) return;
        _walletLabel.text = "혈석 " + _wallet.Balance;
        for (int i = 0; i < _slots.Length; i++)
        {
            TraitData data = FindData(_slots[i].Id);
            if (data == null) continue;
            int level = _progression.GetLevel(data.Id);
            Color color = level > 0 ? new Color(0.65f, 0.85f, 1f) : new Color(0.72f, 0.72f, 0.72f);
            _slots[i].Button.SetDisplay(data.DisplayName, color);
        }

        TraitData inspected = FindData(_inspectedTrait);
        if (inspected == null) return;
        int currentLevel = _progression.GetLevel(inspected.Id);
        _title.text = inspected.DisplayName;
        _level.text = "(" + currentLevel + " / " + inspected.MaxLevel + ")";
        _icon.sprite = inspected.Icon;
        _icon.enabled = inspected.Icon != null;
        _description.text = inspected.Description + "\n\n" + inspected.EffectLabel + "\n현재: " +
            (inspected.ValuePerLevel * currentLevel).ToString("0.##") + inspected.ValueUnit +
            "\n레벨당: " + inspected.ValuePerLevel.ToString("0.##") + inspected.ValueUnit;

        string reason;
        bool canUpgrade = _progression.CanUpgrade(inspected.Id, out reason);
        _upgradeButton.interactable = canUpgrade;
        if (currentLevel >= inspected.MaxLevel)
        {
            _upgradeLabel.text = "최대 레벨";
            return;
        }

        // Current date KDH 2026-09-29: 현재 레벨에서 다음 레벨로 올리는 비용을 표시합니다.
        string cost = "(혈석 " + inspected.GetUpgradeCost(currentLevel) + ")";
        if (!canUpgrade) cost = "<color=#D32F2F>" + cost + "</color>";
        _upgradeLabel.text = "레벨 상승\n" + cost;
        if (!canUpgrade) _description.text += "\n\n" + reason;
    }

    public void Shutdown()
    {
        if (_slots != null)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i].Button == null) continue;
                _slots[i].Button.Clicked -= OnTraitClicked;
                _slots[i].Button.Shutdown();
            }
        }
        if (_upgradeButton != null) _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
        _progression = null;
        _wallet = null;
    }

    private TraitData FindData(TraitId id)
    {
        for (int i = 0; i < _progression.Data.Count; i++)
        {
            if (_progression.Data[i].Id == id) return _progression.Data[i];
        }
        return null;
    }

    private void OnTraitClicked(OutGameChoiceButton button)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Button != button) continue;
            _inspectedTrait = _slots[i].Id;
            Refresh();
            return;
        }
    }

    private void OnUpgradeClicked() { UpgradeRequested?.Invoke(_inspectedTrait); }
    private void OnBackClicked() { BackRequested?.Invoke(); }
    private void OnDestroy() { Shutdown(); }
}
