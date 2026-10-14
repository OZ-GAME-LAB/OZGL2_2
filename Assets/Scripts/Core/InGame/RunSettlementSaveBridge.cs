using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;

namespace Game.Core
{
    // 기존 정산의 계산·지갑 반영을 유지하면서 지급 영수증과 완료 체크포인트를 연결합니다.
    public sealed class RunSettlementSaveBridge : IPersistentSaveWriter, IRunSettlementUI
    {
        private readonly IPersistentSaveWriter _underlying;
        private readonly Func<string> _runId;
        private readonly IRunSettlementUI _view;
        private readonly Func<CancellationToken, UniTask> _beforeShow;

        public RunSettlementSaveBridge(IPersistentSaveWriter underlying, Func<string> runId,
            IRunSettlementUI view, Func<CancellationToken, UniTask> beforeShow)
        {
            _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            _runId = runId ?? throw new ArgumentNullException(nameof(runId));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _beforeShow = beforeShow ?? throw new ArgumentNullException(nameof(beforeShow));
        }

        public bool IsReady => _underlying.IsReady;

        public PersistentSaveData CaptureSaveData() => _underlying.CaptureSaveData();

        public bool TrySave(PersistentSaveData data, out string error)
        {
            if (data == null)
            {
                error = "정산 저장 데이터가 없습니다.";
                return false;
            }
            if (!IsReady)
            {
                error = "아웃게임 저장을 먼저 준비해주세요.";
                return false;
            }

            string currentRunId = _runId();
            if (string.IsNullOrWhiteSpace(currentRunId))
            {
                error = "정산할 런의 ID가 없습니다.";
                return false;
            }

            PersistentSaveData current = _underlying.CaptureSaveData();
            if (current == null)
            {
                error = "현재 아웃게임 저장 데이터를 가져오지 못했습니다.";
                return false;
            }
            if (string.Equals(current.LastSettledRunId, currentRunId, StringComparison.Ordinal))
            {
                error = "이 런의 정산 보상은 이미 저장되었습니다.";
                return false;
            }

            data.LastSettledRunId = currentRunId;
            // 성공한 뒤의 지갑 반영과 지급 기록은 기존 정산 매니저가 담당합니다.
            return _underlying.TrySave(data, out error);
        }

        public async UniTask ShowAndWaitAsync(RunSummary summary, int bloodstones, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string currentRunId = _runId();
            PersistentSaveData current = _underlying.CaptureSaveData();
            if (!IsReady || string.IsNullOrWhiteSpace(currentRunId) || current == null ||
                !string.Equals(current.LastSettledRunId, currentRunId, StringComparison.Ordinal))
                throw new InvalidOperationException("현재 런의 정산 저장이 확인되지 않아 결과 화면을 열 수 없습니다.");

            await _beforeShow(token);
            token.ThrowIfCancellationRequested();
            await _view.ShowAndWaitAsync(summary, bloodstones, token);
        }
    }
}
