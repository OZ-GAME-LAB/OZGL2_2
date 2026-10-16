using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>현재 판 또는 결산 기록을 조회 화면에 전달한다. 선택과 효과를 변경하지 않는다.</summary>
    public sealed class RunInfoPresenter : MonoBehaviour
    {
        [SerializeField] private RunAltarView _altarView;
        [SerializeField] private OutGameTraitView _traitView;
        [SerializeField] private OutGameTotemView _totemView;
        [SerializeField] private RunResultView _resultView;
        [SerializeField] private Button _altarButton;
        [SerializeField] private Button _traitButton;
        [SerializeField] private Button _totemButton;
        [SerializeField] private Button _resultAltarButton;
        [SerializeField] private Button _resultTraitButton;

        private InGameUIManager _ui;
        private AltarManager _altars;
        private TraitRunApplier _traits;
        private TotemRunApplier _totems;
        private TraitCatalog _traitCatalog;
        private TotemEffectCatalog _totemCatalog;
        private UIScreen _openedScreen;
        private float _previousTimeScale;
        private bool _subscribed;

        public void Initialize(InGameUIManager ui, AltarManager altars, TraitRunApplier traits,
            TotemRunApplier totems, TraitCatalog traitCatalog, TotemEffectCatalog totemCatalog)
        {
            Unbind();
            CloseOwnedScreen();
            if (ui == null || altars == null || traits == null || totems == null ||
                traitCatalog == null || totemCatalog == null || _altarView == null ||
                _traitView == null || _totemView == null || _resultView == null ||
                _altarButton == null || _traitButton == null || _totemButton == null ||
                _resultAltarButton == null || _resultTraitButton == null)
                throw new InvalidOperationException("제단·특성·토템 조회 참조를 확인해주세요.");
            _ui = ui;
            _altars = altars;
            _traits = traits;
            _totems = totems;
            _traitCatalog = traitCatalog;
            _totemCatalog = totemCatalog;
            if (isActiveAndEnabled) Bind();
        }

        public void OpenAltar() => Open(UIId.RunAltar);
        public void OpenTraits() => Open(UIId.RunTraits);
        public void OpenTotems() => Open(UIId.RunTotems);

        private void Open(UIId id)
        {
            if (_ui == null || _openedScreen != null || !isActiveAndEnabled) return;
            // 필수 보상 등 다른 문맥 위에 HUD 입력이 우회해서 열리지 않게 한다.
            if (_ui.TopPopup != null && _ui.TopPopup.Id != UIId.RunResult) return;
            if (!_ui.TryGetScreen(id, out UIScreen screen)) return;

            RunSummary summary = _resultView.CurrentSummary;
            if (id == UIId.RunAltar)
                _altarView.Show(summary != null ? summary.Altar : _altars.Selected);
            else if (id == UIId.RunTraits)
                _traitView.Initialize(null, null, true, _traitCatalog.Traits,
                    summary != null ? summary.Traits : _traits.CaptureLevels());
            else
                _totemView.Initialize(null, true, _totemCatalog.Totems,
                    summary != null ? summary.Totems : _totems.ActiveTotems);

            if (!_ui.OpenPopup(id)) return;
            _openedScreen = screen;
            screen.Closed += HandleClosed;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
        }

        private void HandleClosed(UIScreen screen, UICloseReason reason)
        {
            if (_openedScreen != screen) return;
            screen.Closed -= HandleClosed;
            _openedScreen = null;
            Time.timeScale = _previousTimeScale;
        }

        private void CloseOwnedScreen()
        {
            if (_openedScreen == null) return;
            UIScreen screen = _openedScreen;
            _ui.ClosePopup(screen.Id, UICloseReason.ContextLost);
            // UI 루트가 먼저 정리된 경우에도 자신이 기억한 배속만 한 번 복원한다.
            if (_openedScreen != null) HandleClosed(screen, UICloseReason.ContextLost);
        }

        private void Bind()
        {
            if (_ui == null || _subscribed) return;
            _altarButton.onClick.AddListener(OpenAltar);
            _traitButton.onClick.AddListener(OpenTraits);
            _totemButton.onClick.AddListener(OpenTotems);
            _resultAltarButton.onClick.AddListener(OpenAltar);
            _resultTraitButton.onClick.AddListener(OpenTraits);
            _subscribed = true;
        }

        private void Unbind()
        {
            if (!_subscribed) return;
            _altarButton.onClick.RemoveListener(OpenAltar);
            _traitButton.onClick.RemoveListener(OpenTraits);
            _totemButton.onClick.RemoveListener(OpenTotems);
            _resultAltarButton.onClick.RemoveListener(OpenAltar);
            _resultTraitButton.onClick.RemoveListener(OpenTraits);
            _subscribed = false;
        }

        private void OnEnable() => Bind();
        private void OnDisable() { Unbind(); CloseOwnedScreen(); }
    }
}
