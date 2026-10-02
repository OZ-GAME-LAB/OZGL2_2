using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
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
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _artifacts;
        [SerializeField] private TMP_Text _bloodstone;
        [SerializeField] private UnityEngine.UI.Button _mainButton;

        private UniTaskCompletionSource _pending;

        public async UniTask ShowAndWaitAsync(RunSummary summary, int bloodstones, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (bloodstones < 0) throw new ArgumentOutOfRangeException(nameof(bloodstones));
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
                Apply(summary, bloodstones);
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

        private void Apply(RunSummary summary, int bloodstones)
        {
            _progress.text = summary.MaxQuarter > 0 && summary.MaxWave > 0
                ? $"{summary.MaxQuarter}분기 · {summary.MaxWave}웨이브" : "클리어 기록 없음";
            summary.Artifacts.TryGetValue(ArtifactRarity.Common, out int common);
            summary.Artifacts.TryGetValue(ArtifactRarity.Rare, out int rare);
            summary.Artifacts.TryGetValue(ArtifactRarity.Legendary, out int legendary);
            summary.Artifacts.TryGetValue(ArtifactRarity.Mythic, out int mythic);
            _artifacts.text = $"일반 {common:N0}개\n희귀 {rare:N0}개\n전설 {legendary:N0}개\n신화 {mythic:N0}개";
            _bloodstone.text = bloodstones.ToString("N0");
        }

        private bool HasView() => _screen != null && _screen.Manager != null && _screen.Root != null &&
            _progress != null && _artifacts != null && _bloodstone != null && _mainButton != null;

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
