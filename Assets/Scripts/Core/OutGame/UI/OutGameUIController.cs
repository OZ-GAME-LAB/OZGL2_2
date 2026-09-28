using UnityEngine;

// UI 초기화, 이벤트 연결과 화면 이동을 담당합니다. 선택·구매 판정은 Selector에 맡깁니다.
public class OutGameUIController : MonoBehaviour
{
    [SerializeField] private OutGameAltarView _altarView;
    [SerializeField] private OutGameTraitView _traitView;
    [SerializeField] private OutGameTotemView _totemView;

    private IAltarSelection _altars;
    private ITraitProgression _traits;
    private ITotemSelection _totems;
    private OutGameTestWallet _wallet;
    private OutGameStartController _startController;

    public bool ValidateReferences()
    {
        if (_altarView == null || _traitView == null || _totemView == null)
        {
            Debug.LogError("[OutGameUIController] View 3개를 Inspector에서 연결해주세요.", this);
            return false;
        }
        return _altarView.ValidateReferences() && _traitView.ValidateReferences() && _totemView.ValidateReferences();
    }

    public void Initialize(IAltarSelection altars, ITraitProgression traits, ITotemSelection totems,
        OutGameTestWallet wallet, OutGameStartController startController)
    {
        Shutdown();
        _altars = altars;
        _traits = traits;
        _totems = totems;
        _wallet = wallet;
        _startController = startController;

        _altarView.Initialize(altars);
        _traitView.Initialize(traits, wallet);
        _totemView.Initialize(totems);

        _altarView.Selected += OnAltarSelected;
        _altarView.TraitRequested += OnTraitsRequested;
        _altarView.NextRequested += OnTotemsRequested;
        _traitView.UpgradeRequested += OnTraitUpgradeRequested;
        _traitView.BackRequested += OnAltarRequested;
        _totemView.LevelChangeRequested += OnTotemLevelChangeRequested;
        _totemView.BackRequested += OnAltarRequested;
        _totemView.StartRequested += OnStartRequested;

        _altars.Changed += RefreshAltar;
        _traits.Changed += RefreshTraits;
        _totems.Changed += RefreshTotems;
        _wallet.Changed += RefreshTraits;

        // 각 View의 초기 표시와 이벤트 연결을 마친 후 첫 화면을 엽니다.
        ShowAltar();
    }

    public void ShowAltar()
    {
        ShowPanel(_altarView.gameObject);
    }

    public void ShowTraits()
    {
        ShowPanel(_traitView.gameObject);
    }

    public void ShowTotems()
    {
        ShowPanel(_totemView.gameObject);
    }

    private void ShowPanel(GameObject panel)
    {
        // View가 패널 루트에 있으므로 별도의 패널 참조를 중복해서 저장하지 않습니다.
        _altarView.gameObject.SetActive(panel == _altarView.gameObject);
        _traitView.gameObject.SetActive(panel == _traitView.gameObject);
        _totemView.gameObject.SetActive(panel == _totemView.gameObject);
    }

    public void Shutdown()
    {
        if (_altars != null) _altars.Changed -= RefreshAltar;
        if (_traits != null) _traits.Changed -= RefreshTraits;
        if (_totems != null) _totems.Changed -= RefreshTotems;
        if (_wallet != null) _wallet.Changed -= RefreshTraits;

        if (_altarView != null)
        {
            _altarView.Selected -= OnAltarSelected;
            _altarView.TraitRequested -= OnTraitsRequested;
            _altarView.NextRequested -= OnTotemsRequested;
            _altarView.Shutdown();
        }
        if (_traitView != null)
        {
            _traitView.UpgradeRequested -= OnTraitUpgradeRequested;
            _traitView.BackRequested -= OnAltarRequested;
            _traitView.Shutdown();
        }
        if (_totemView != null)
        {
            _totemView.LevelChangeRequested -= OnTotemLevelChangeRequested;
            _totemView.BackRequested -= OnAltarRequested;
            _totemView.StartRequested -= OnStartRequested;
            _totemView.Shutdown();
        }
        _altars = null;
        _traits = null;
        _totems = null;
        _wallet = null;
        _startController = null;
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnAltarSelected(AltarId id)
    {
        _altars.TrySelect(id);
        // 잠긴 제단도 상세 설명을 확인할 수 있습니다.
        _altarView.Inspect(id);
    }

    private void OnTraitUpgradeRequested(TraitId id)
    {
        if (!_traits.TryUpgrade(id, out string error))
            Debug.LogWarning("[OutGameUIController] 특성 구매 실패: " + error, this);
        _traitView.Refresh();
    }

    private void OnTotemLevelChangeRequested(TotemId id, int delta)
    {
        _totems.TryChangeLevel(id, delta);
    }

    private void OnAltarRequested()
    {
        ShowAltar();
    }

    private void OnTraitsRequested()
    {
        ShowTraits();
    }

    private void OnTotemsRequested()
    {
        ShowTotems();
    }

    private void OnStartRequested()
    {
        _startController.StartRun();
    }

    private void RefreshAltar() { _altarView.Refresh(); }
    private void RefreshTraits() { _traitView.Refresh(); }
    private void RefreshTotems() { _totemView.Refresh(); }
}
