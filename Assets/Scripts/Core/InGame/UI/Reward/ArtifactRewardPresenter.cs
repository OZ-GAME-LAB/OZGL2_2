using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.UI.InGame
{
    /// <summary>전달받은 후보를 표시하고 선택만 반환한다. 지급과 재시도는 요청한 시스템이 결정한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactRewardPresenter : MonoBehaviour, IArtifactSelectionUI
    {
        public bool IsChoosing => _selectionCompletion != null;
        public IReadOnlyList<ArtifactData> CurrentCandidates => _currentCandidates;

        [SerializeField] private ArtifactRewardView _panel;

        private UniTaskCompletionSource<ArtifactData> _selectionCompletion;
        private IReadOnlyList<ArtifactData> _currentCandidates = Array.Empty<ArtifactData>();

        private void OnDisable() => ResetReward();

        public async UniTask<ArtifactData> SelectAsync(
            IReadOnlyList<ArtifactData> candidates,
            CancellationToken token,
            bool allowForfeit = false,
            string message = null)
        {
            token.ThrowIfCancellationRequested();
            if (!isActiveAndEnabled || _panel == null || _panel.Screen == null)
                throw new InvalidOperationException("아티팩트 선택 UI 가 존재하지 않습니다.");
            if (IsChoosing)
                throw new InvalidOperationException("아티팩트 선택이 이미 진행중입니다.");
            if (candidates == null || candidates.Count == 0)
                throw new ArgumentException("최소한 아티팩트 후보 1개는 제공되어야 합니다.", nameof(candidates));

            // 목록만 복사해 요청 중 후보 순서를 고정한다. 유물 데이터 자체는 원본을 사용한다.
            var snapshot = new ArtifactData[candidates.Count];
            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                ArtifactData data = candidates[i];
                if (data == null || string.IsNullOrWhiteSpace(data.Id) || !candidateIds.Add(data.Id))
                    throw new ArgumentException("Candidates must be non-null with distinct IDs.", nameof(candidates));
                snapshot[i] = data;
            }

            IReadOnlyList<ArtifactData> requestCandidates = Array.AsReadOnly(snapshot);
            var completion = new UniTaskCompletionSource<ArtifactData>();
            bool submitting = false;
            void OnChoice(ArtifactData selected)
            {
                if (!ReferenceEquals(_selectionCompletion, completion) || submitting ||
                    completion.Task.Status != UniTaskStatus.Pending) return;
                if (selected == null && !allowForfeit)
                {
                    _panel.TryResolveSelection(false, "유물을 하나 선택해주세요.");
                    return;
                }
                if (selected != null && !ContainsCandidate(requestCandidates, selected))
                {
                    _panel.TryResolveSelection(false, "선택한 아티팩트가 현재 후보에 없습니다.");
                    return;
                }

                // 선택에 따른 정상 닫힘이 취소 응답을 앞서 보내지 않게 한다.
                submitting = true;
                if (_panel.TryResolveSelection(true)) completion.TrySetResult(selected);
                else submitting = false;
            }
            
            void OnClosed(UIScreen screen, UICloseReason reason)
            {
                if (!submitting) completion.TrySetCanceled();
            }

            _selectionCompletion = completion;
            _currentCandidates = requestCandidates;
            UIScreen requestScreen = _panel.Screen;
            _panel.ChoiceRequested += OnChoice;
            requestScreen.Closed += OnClosed;
            try
            {
                using (token.Register(() => completion.TrySetCanceled(token)))
                {
                    token.ThrowIfCancellationRequested();
                    _panel.ShowSelection(requestCandidates, allowForfeit, message);
                    if (!_panel.IsVisible)
                        throw new InvalidOperationException("Artifact selection popup could not open.");
                    return await completion.Task;
                }
            }
            finally
            {
                // 취소 토큰은 작업 스레드에서도 취소될 수 있으므로 화면 정리는 Unity 스레드에서 한다.
                await UniTask.SwitchToMainThread();
                requestScreen.Closed -= OnClosed;
                _panel.ChoiceRequested -= OnChoice;
                if (ReferenceEquals(_selectionCompletion, completion))
                {
                    _selectionCompletion = null;
                    _currentCandidates = Array.Empty<ArtifactData>();
                    _panel.EndSelection();
                }
            }
        }

        /// <summary>화면 수명이 끝날 때 진행 중인 선택을 취소하고 표시 상태를 비운다.</summary>
        public void ResetReward()
        {
            _selectionCompletion?.TrySetCanceled();
            if (_panel != null) _panel.ResetReward();
        }

        private static bool ContainsCandidate(IReadOnlyList<ArtifactData> candidates, ArtifactData selected)
        {
            for (int i = 0; i < candidates.Count; i++)
                if (ReferenceEquals(candidates[i], selected)) return true;
            return false;
        }
    }
}
