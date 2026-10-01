using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.InGame
{
    /// <summary>호출자가 준비한 정산 결과를 표시하고 확인만 기다린다.</summary>
    public sealed class SettlementView : MonoBehaviour, IRunSettlementUI
    {
        public bool IsVisible => _screen != null && _screen.IsVisible;
        public bool IsPending => _pending != null;

        [SerializeField] private UIScreen _screen;
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _totems;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _artifacts;
        [SerializeField] private TMP_Text _score;
        [SerializeField] private TMP_Text _bloodstone;
        [SerializeField] private TMP_Text _gaugeText;
        [SerializeField] private RectTransform _gaugeFill;
        [SerializeField] private TMP_Text _notice;
        [SerializeField] private UnityEngine.UI.Button _mainButton;

        private UniTaskCompletionSource _pending;

        public async UniTask ShowAndWaitAsync(RunSettlementViewData data, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_pending != null) throw new InvalidOperationException("A settlement request is already in progress.");
            if (!isActiveAndEnabled || !HasView())
                throw new InvalidOperationException("Settlement UI is not available.");

            var completion = new UniTaskCompletionSource();
            _pending = completion;
            bool completed = false;
            UnityEngine.Events.UnityAction confirm = () => HandleMain(completion);
            Action<UIScreen, UICloseReason> closed = (_, __) =>
            {
                if (ReferenceEquals(_pending, completion)) completion.TrySetCanceled();
            };
            try
            {
                Apply(data);
                _mainButton.interactable = true;
                _mainButton.onClick.AddListener(confirm);
                _screen.Closed += closed;
                if (!_screen.Manager.ReplacePopup(_screen.Id) || !_screen.IsVisible)
                    throw new InvalidOperationException("Settlement UI could not be opened.");
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_mainButton.gameObject);
                using (token.Register(() => completion.TrySetCanceled(token)))
                    await completion.Task;
                completed = true;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                if (ReferenceEquals(_pending, completion))
                {
                    if (_mainButton != null)
                    {
                        _mainButton.onClick.RemoveListener(confirm);
                        _mainButton.interactable = false;
                    }
                    if (_screen != null)
                    {
                        _screen.Closed -= closed;
                        _screen.Manager?.ClosePopup(_screen.Id,
                            completed ? UICloseReason.Completed : UICloseReason.ContextLost);
                    }
                    _pending = null;
                }
            }
        }

        private void Apply(RunSettlementViewData data)
        {
            _title.text = data.IsVictory ? "원정 완료" : "원정 종료";
            _totems.text = data.TotemSummary ?? "선택 토템 정보 대기";
            _progress.text = data.ProgressSummary ?? "진행 기록 연결 대기";
            _artifacts.text = data.ArtifactSummary ?? "획득 유물 기록 대기";
            _score.text = data.TotalScore.HasValue ? data.TotalScore.Value.ToString("N0") : "—";
            _bloodstone.text = data.Bloodstones.HasValue ? $"획득 혈석  {data.Bloodstones.Value:N0}" : "획득 혈석  —";
            bool hasGauge = data.GaugeCurrent.HasValue && data.GaugeTarget.HasValue;
            _gaugeText.text = hasGauge ? $"{data.GaugeCurrent:N0} / {data.GaugeTarget:N0}" : "혈석 게이지 연결 대기";
            _gaugeFill.anchorMax = new Vector2(hasGauge ? (float)data.GaugeCurrent.Value / data.GaugeTarget.Value : 0, 1);
            _notice.text = data.TotalScore.HasValue && data.Bloodstones.HasValue
                ? "정산 결과" : "정산 시스템 연결 대기 · 표시만으로 재화가 지급되지 않습니다";
        }

        private bool HasView() => _screen != null && _screen.Manager != null && _root != null &&
            _title != null && _totems != null && _progress != null && _artifacts != null &&
            _score != null && _bloodstone != null && _gaugeText != null && _gaugeFill != null &&
            _notice != null && _mainButton != null;

        private void HandleMain(UniTaskCompletionSource completion)
        {
            if (!ReferenceEquals(_pending, completion) || !isActiveAndEnabled || !IsVisible || !_mainButton.IsInteractable()) return;
            _mainButton.interactable = false;
            completion.TrySetResult();
        }

        private void OnDisable() => _pending?.TrySetCanceled();
        private void OnDestroy() => _pending?.TrySetCanceled();
    }
}
