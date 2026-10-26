using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>현재 판 또는 결산 기록을 조회 화면에 전달한다. 선택과 효과를 변경하지 않는다.</summary>
    public sealed class RunInfoPresenter : MonoBehaviour
    {
        [SerializeField] private RunInfoView _view;
        [SerializeField] private Button _infoButton;
        [Header("Settlement - Optional")]
        [SerializeField] private RunResultView _resultView;
        [SerializeField] private Button _resultAltarButton;
        [SerializeField] private Button _resultTraitButton;
        private InGameUIManager _ui;
        private AltarManager _altars;
        private TraitRunApplier _traits;
        private TotemRunApplier _totems;
        private TraitCatalog _traitCatalog;
        private readonly List<RunInfoRowData> _traitRows = new List<RunInfoRowData>();
        private readonly List<RunInfoRowData> _totemRows = new List<RunInfoRowData>();
        private UIScreen _openedScreen;
        private float _previousTimeScale;
        private bool _subscribed;

        public void Initialize(InGameUIManager ui, AltarManager altars, TraitRunApplier traits,
            TotemRunApplier totems, TraitCatalog traitCatalog, TotemEffectCatalog totemCatalog)
        {
            Unbind();
            CloseOwnedScreen();
            if (ui == null || altars == null || traits == null || totems == null ||
                traitCatalog == null || _view == null || !_view.ValidateReferences() ||
                _infoButton == null || _view.Screen.Id != UIId.RunInfo)
                throw new InvalidOperationException("계승 정보 Presenter / View 참조와 UIScreen의 RunInfo ID를 확인해주세요.");
            _ui = ui;
            _altars = altars;
            _traits = traits;
            _totems = totems;
            _traitCatalog = traitCatalog;

            if (isActiveAndEnabled) Bind();
        }

        public void ToggleInfo()
        {
            if (_ui == null || !isActiveAndEnabled) return;
            if (_openedScreen != null)
            {
                // 다른 팝업이 위에 있다면 뒤쪽 화면을 닫아 배속을 복원하지 않습니다.
                if (_ui.TopPopup == _openedScreen)
                    _ui.ClosePopup(_openedScreen.Id, UICloseReason.UserCancel);
                return;
            }
            OpenInfo();
        }

        public void OpenInfo()
        {
            if (_ui == null || _openedScreen != null || !isActiveAndEnabled) return;
            if (_ui.TopPopup != null && _ui.TopPopup.Id != UIId.RunResult) return;
            if (!_ui.TryGetScreen(UIId.RunInfo, out UIScreen screen) || screen != _view.Screen)
            {
                Debug.LogError("[RunInfoPresenter] 통합 View의 UIScreen을 RunInfo로 등록해주세요.", this);
                return;
            }

            // 결산 창에서 열 때만 기록을 사용합니다.
            RunSummary summary = _ui.TopPopup != null && _ui.TopPopup.Id == UIId.RunResult &&
                _resultView != null ? _resultView.CurrentSummary : null;
            AltarData altar = summary != null ? summary.Altar : _altars.Selected;
            BuildTraits(summary != null ? summary.Traits : _traits.CaptureLevels());
            BuildTotems(summary != null ? summary.Totems : _totems.ActiveTotems);
            _view.Show(new RunInfoRowData(altar != null ? altar.Icon : null,
                altar != null ? altar.DisplayName : "선택한 제단 없음", string.Empty,
                altar != null ? GetFirstLine(altar.Description) : "이번 판에 적용된 제단이 없습니다."),
                _traitRows, _totemRows);

            if (!_ui.OpenPopup(UIId.RunInfo)) return;
            _openedScreen = screen;
            screen.Closed += HandleClosed;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            _view.ResetScrollPositions();
        }

        // 기존 결산 및 UnityEvent 호출도 통합 창으로 연결합니다.
        public void OpenAltar() => OpenInfo();
        public void OpenTraits() => OpenInfo();
        public void OpenTotems() => OpenInfo();

        private static string GetFirstLine(string description)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;
            int end = description.IndexOfAny(new[] { '\r', '\n' });
            return (end >= 0 ? description.Substring(0, end) : description).Trim();
        }
        private void BuildTraits(IReadOnlyList<TraitLevelEntry> levels)
        {
            _traitRows.Clear();
            if (levels == null) return;
            foreach (TraitData data in _traitCatalog.Traits)
            {
                if (data == null) continue;
                int level = 0;
                foreach (TraitLevelEntry entry in levels)
                    if (entry != null && entry.Id == data.Id) { level = entry.Level; break; }
                level = Mathf.Clamp(level, 0, data.MaxLevel);
                if (level == 0) continue;
                string effect = data.EffectLabel + " " +
                    (data.ValuePerLevel * level).ToString("0.##") + data.ValueUnit;
                _traitRows.Add(new RunInfoRowData(data.Icon, data.DisplayName, $"Lv.{level}", effect));
            }
        }

        private void BuildTotems(IReadOnlyList<ActiveTotem> totems)
        {
            _totemRows.Clear();
            if (totems == null) return;
            foreach (ActiveTotem active in totems)
            {
                TotemData data = active.Data;
                if (data == null || active.Level <= 0) continue;
                int level = Mathf.Clamp(active.Level, 0, data.MaxLevel);
                string label = data.SelectionMode == TotemSelectionMode.Toggle ? "활성" : $"Lv.{level}";
                _totemRows.Add(new RunInfoRowData(data.Icon, data.DisplayName, label,
                    data.GetActiveDescription(level)));
            }
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
            if (_ui != null) _ui.ClosePopup(screen.Id, UICloseReason.ContextLost);
            // UI 루트가 먼저 정리된 경우에도 자신이 기억한 배속만 한 번 복원한다.
            if (_openedScreen != null) HandleClosed(screen, UICloseReason.ContextLost);
        }

        private void Bind()
        {
            if (_ui == null || _subscribed) return;
            _infoButton.onClick.AddListener(ToggleInfo);
            if (_resultAltarButton != null) _resultAltarButton.onClick.AddListener(OpenInfo);
            if (_resultTraitButton != null) _resultTraitButton.onClick.AddListener(OpenInfo);
            _subscribed = true;
        }

        private void Unbind()
        {
            if (!_subscribed) return;
            if (_infoButton != null) _infoButton.onClick.RemoveListener(ToggleInfo);
            if (_resultAltarButton != null) _resultAltarButton.onClick.RemoveListener(OpenInfo);
            if (_resultTraitButton != null) _resultTraitButton.onClick.RemoveListener(OpenInfo);
            _subscribed = false;
        }

        private void OnEnable() => Bind();
        private void OnDisable() { Unbind(); CloseOwnedScreen(); }
    }
}
