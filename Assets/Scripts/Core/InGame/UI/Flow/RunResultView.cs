using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>호출자가 준비한 정산 결과를 표시하고 확인만 기다린다.</summary>
    public sealed class RunResultView : MonoBehaviour, IRunSettlementUI
    {
        public bool IsVisible => _screen != null && _screen.IsVisible;
        public bool IsPending => _pending != null;
        public RunSummary CurrentSummary { get; private set; }

        [SerializeField] private UIScreen _screen;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _progressScore;
        [SerializeField] private TMP_Text _artifactScore;
        [SerializeField] private TMP_Text _totemMultiplier;
        [SerializeField] private TMP_Text _totalScore;
        [SerializeField] private TotemRowView _totemRowPrefab;
        [SerializeField] private RectTransform _totemContent;
        [SerializeField] private GameObject _emptyTotems;
        [SerializeField] private ScrollRect _totemScrollRect;
        [SerializeField] private TMP_Text _artifacts;
        [SerializeField] private TMP_Text _bloodstone;
        [SerializeField] private Button _mainButton;

        private UniTaskCompletionSource _pending;
        private readonly List<TotemRowView> _totemRows = new();

        public async UniTask ShowAndWaitAsync(RunSummary summary, RunResult result, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_pending != null) throw new InvalidOperationException("보상 정산이 이미 실행중입니다");
            if (!isActiveAndEnabled || !HasView())
                throw new InvalidOperationException("게임 정산 UI 사용이 불가능합니다");

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
                CurrentSummary = summary;
                Apply(summary, result);
                _mainButton.interactable = true;
                _mainButton.onClick.AddListener(confirm);
                _screen.Closed += closed;
                if (!_screen.Manager.ReplacePopup(_screen.Id) || !_screen.IsVisible)
                    throw new InvalidOperationException("게임 정산 UI를 열 수 없습니다.");
                ResetTotemScroll();
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
                    CurrentSummary = null;
                }
            }
        }

        private void Apply(RunSummary summary, RunResult result)
        {
            _progress.text = summary.MaxQuarter > 0 && summary.MaxWave > 0
                ? $"{summary.MaxQuarter}분기 · {summary.MaxWave}웨이브" : "클리어 기록 없음";
            ApplyTotems(summary.Totems);
            _progressScore.text = result.ProgressScore.ToString("N0");
            _artifactScore.text = result.ArtifactScore.ToString("N0");
            _totemMultiplier.text = (result.TotemMultiplier * 100).ToString("0.##") + "%";
            _totalScore.text = result.TotemAdjustedScore.ToString("#,##0.##");
            _artifacts.text = ArtifactLine("일반", ArtifactRarity.Common, summary, result) + "\n" +
                ArtifactLine("희귀", ArtifactRarity.Rare, summary, result) + "\n" +
                ArtifactLine("전설", ArtifactRarity.Legendary, summary, result) + "\n" +
                ArtifactLine("신화", ArtifactRarity.Mythic, summary, result);
            _bloodstone.text = result.FinalBloodstones.ToString("N0");
        }

        private static string ArtifactLine(string label, ArtifactRarity rarity, RunSummary summary, RunResult result)
        {
            summary.Artifacts.TryGetValue(rarity, out int count);
            result.ArtifactScores.TryGetValue(rarity, out long score);
            return $"{label} {count:N0}개 · {score:N0}점";
        }

        private void ApplyTotems(IReadOnlyList<ActiveTotem> totems)
        {
            for (int i = 0; i < totems.Count; i++)
            {
                if (i == _totemRows.Count)
                    _totemRows.Add(Instantiate(_totemRowPrefab, _totemContent));

                _totemRows[i].Bind(totems[i]);
                _totemRows[i].gameObject.SetActive(true);
            }

            for (int i = totems.Count; i < _totemRows.Count; i++)
                _totemRows[i].gameObject.SetActive(false);

            _totemContent.gameObject.SetActive(totems.Count > 0);
            _emptyTotems.SetActive(totems.Count == 0);
        }

        private void ResetTotemScroll()
        {
            if (_totemScrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            _totemScrollRect.StopMovement();
            _totemScrollRect.verticalNormalizedPosition = 1f;
        }

        private bool HasView() => _screen != null && _screen.Manager != null && _screen.Root != null &&
            _progress != null && _artifacts != null && _bloodstone != null && _mainButton != null &&
            _progressScore != null && _artifactScore != null && _totemMultiplier != null && _totalScore != null &&
            _totemRowPrefab != null && _totemRowPrefab.HasReferences && _totemContent != null && _emptyTotems != null;

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
