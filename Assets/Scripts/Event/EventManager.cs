using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.UI;
using UnityEngine;

public enum EventProgress { Choosing, Result, Completed }

[Serializable]
public sealed class EventSaveData
{
    public string OccurrenceId;
    public EventId EventId;
    public EventProgress Progress;
    public int SelectedChoiceIndex = -1;
    public int GoldCost;
    public int GoldReward;
    public string[] SpentArtifactIds = Array.Empty<string>();
    public string RewardArtifactId;
}

// 사건 선정, 선택 결과 적용, 저장과 화면 대기를 한곳에서 관리한다.
public sealed class EventManager : MonoBehaviour, IEventFlow, ISaveDataProvider<EventSaveData>
{
    public bool IsInitialized { get; private set; }
    public EventData CurrentEvent { get; private set; }
    public string CurrentOccurrenceId => _state.OccurrenceId;
    public EventProgress Progress => _state.Progress;
    public bool IsProcessing { get; private set; }
    public bool IsResolved => CurrentEvent != null && Progress != EventProgress.Choosing;
    public bool IsCompleted => CurrentEvent != null && Progress == EventProgress.Completed;
    public bool IsOpen { get; private set; }
    public string LastFailureReason { get; private set; }

    [SerializeField] private EventData[] _events = Array.Empty<EventData>();

    private readonly Dictionary<EventId, EventData> _eventsById = new Dictionary<EventId, EventData>();
    private readonly System.Random _random = new System.Random();
    private RunCurrencyManager _currencyManager;
    private ArtifactManager _artifactManager;
    private GameFlowController _flow;
    private IEventUI _ui;
    private IArtifactSelectionUI _selectionUI;
    private IRunCheckpointWriter _checkpointWriter;
    private EventSaveData _state = new EventSaveData();
    private CancellationTokenSource _runCancellation;
    private UniTaskCompletionSource _completion;
    private bool _isApplying;
    private bool _faulted;

    public bool Initialize(RunCurrencyManager currencyManager, ArtifactManager artifactManager,
        GameFlowController flow, IEventUI ui, IArtifactSelectionUI selectionUI)
    {
        IsInitialized = false;
        ResetRun();
        _eventsById.Clear();
        _currencyManager = currencyManager;
        _artifactManager = artifactManager;
        _flow = flow;
        _ui = ui;
        _selectionUI = selectionUI;
        _checkpointWriter = null;
        if (_currencyManager == null || !_currencyManager.IsInitialized ||
            _artifactManager == null || !_artifactManager.IsInitialized ||
            _artifactManager.ArtifactCatalog == null || _flow == null || _ui == null || _selectionUI == null)
        {
            Debug.LogError("[EventManager] 초기화된 재화·유물 매니저, 진행 정보와 UI를 연결해주세요.", this);
            return false;
        }
        if (!TryValidateEvents(out string error))
        {
            Debug.LogError("[EventManager] " + error, this);
            return false;
        }
        IsInitialized = true;
        return true;
    }

    public void InitializeSave(IRunCheckpointWriter checkpointWriter)
    {
        _checkpointWriter = checkpointWriter ?? throw new ArgumentNullException(nameof(checkpointWriter));
    }

    public bool TryPrepare(string occurrenceId, out string reason)
    {
        reason = null;
        if (!IsInitialized || IsProcessing || _faulted) reason = "이벤트를 준비할 수 없는 상태입니다.";
        else if (string.IsNullOrWhiteSpace(occurrenceId)) reason = "이벤트 발생 식별자가 필요합니다.";
        else if (CurrentEvent != null && CurrentOccurrenceId == occurrenceId) return true;
        else if (IsOpen || (CurrentEvent != null && !IsCompleted)) reason = "현재 이벤트를 먼저 완료해주세요.";
        else if (_eventsById.Count == 0) reason = "등록된 이벤트가 없습니다.";
        if (reason != null) return false;

        ResetRun();
        CurrentEvent = _events[_random.Next(_events.Length)];
        _state.EventId = CurrentEvent.Id;
        _state.OccurrenceId = occurrenceId;
        _completion = new UniTaskCompletionSource();
        return true;
    }

    public async UniTask OpenEventAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (IsOpen) throw new InvalidOperationException("이벤트가 이미 열려 있습니다.");
        if (!IsInitialized || _flow.CurrentQuarter < 1 || _flow.CurrentWave < 1 ||
            string.IsNullOrWhiteSpace(_flow.RunId))
            throw new InvalidOperationException("런과 이벤트 초기화를 마친 뒤 이벤트를 열어주세요.");
        if (!TryPrepare(GetOccurrenceId(), out string reason)) throw new InvalidOperationException(reason);

        CancellationTokenSource lifetime = _runCancellation;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        CancellationToken waitToken = request.Token;
        IsOpen = true;
        try
        {
            // 사건을 먼저 저장해야 선택 중 종료해도 같은 사건의 첫 화면으로 돌아온다.
            await SaveCheckpointAsync(waitToken);
            while (Progress == EventProgress.Choosing)
            {
                var available = new bool[CurrentEvent.Choices.Count];
                var reasons = new string[available.Length];
                for (int i = 0; i < available.Length; i++)
                    available[i] = CanSelect(i, out reasons[i]);

                int choiceIndex = await _ui.ChooseAsync(CurrentEvent, available, reasons, LastFailureReason, waitToken);
                waitToken.ThrowIfCancellationRequested();
                await TrySelectAsync(CurrentOccurrenceId, choiceIndex, waitToken);
            }
            if (Progress == EventProgress.Result)
            {
                await _ui.ShowMessageAsync(CurrentEvent, BuildResultText(), "계속", waitToken);
                waitToken.ThrowIfCancellationRequested();
                _state.Progress = EventProgress.Completed;
                await SaveCheckpointAsync(waitToken);
            }
            waitToken.ThrowIfCancellationRequested();
            _completion.TrySetResult();
        }
        finally
        {
            if (ReferenceEquals(_runCancellation, lifetime))
            {
                IsOpen = false;
                _ui.Close();
            }
        }
    }

    public bool CanSelect(int choiceIndex, out string reason)
    {
        reason = null;
        if (!IsInitialized || CurrentEvent == null) reason = "진행 중인 이벤트가 없습니다.";
        else if (IsProcessing || IsResolved || _faulted) reason = "처리 중이거나 완료된 이벤트입니다.";
        else if (choiceIndex < 0 || choiceIndex >= CurrentEvent.Choices.Count) reason = "선택지가 올바르지 않습니다.";
        if (reason != null) return false;

        EventChoiceData choice = CurrentEvent.Choices[choiceIndex];
        return CanPay(choice, null, out reason) && choice.Action.CanProceed(CreateContext(choice), out reason);
    }

    public async UniTask<bool> TrySelectAsync(string occurrenceId, int choiceIndex, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (occurrenceId != CurrentOccurrenceId) return Fail("현재 이벤트에 대한 선택이 아닙니다.");
        if (!CanSelect(choiceIndex, out string reason)) return Fail(reason);

        CancellationTokenSource lifetime = _runCancellation;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        CancellationToken selectionToken = request.Token;
        EventChoiceData choice = CurrentEvent.Choices[choiceIndex];
        EventContext context = CreateContext(choice);
        LastFailureReason = null;
        IsProcessing = true;
        try
        {
            // 전략은 선택·추첨만 수행한다. 유물 선택 취소 시 비용은 아직 바뀌지 않았다.
            EventResolution result = await choice.Action.EventProceedAsync(context, selectionToken);
            selectionToken.ThrowIfCancellationRequested();
            if (result == null) return Fail("선택을 취소했습니다.");
            if (!CanPay(choice, result.SelectedArtifactCost, out reason)) return Fail(reason);

            EventResultData reward = result.Reward;
            if (reward == null || !Enum.IsDefined(typeof(EventResourceType), reward.Type) ||
                (reward.Type == EventResourceType.Gold && reward.Gold <= 0))
                throw new InvalidOperationException("이벤트 전략이 유효하지 않은 결과를 반환했습니다.");
            if (reward.Type == EventResourceType.Artifact && !context.CanReceive(reward.Artifact, result.SelectedArtifactCost))
                return Fail("보상 유물을 획득할 수 없습니다.");

            var costs = new List<ArtifactData>(choice.Cost.Artifacts);
            if (result.SelectedArtifactCost != null) costs.Add(result.SelectedArtifactCost);
            int goldReward = reward.Type == EventResourceType.Gold ? reward.Gold : 0;
            ArtifactData artifactReward = reward.Type == EventResourceType.Artifact ? reward.Artifact : null;
            _isApplying = true;
            try
            {
                bool applied = _currencyManager.TryApplyTrade(CurrencyType.Gold, goldReward - choice.Cost.Gold,
                    () => _artifactManager.TryApplyTrade(costs, artifactReward));
                if (!applied) return Fail("선택 결과를 적용하지 못했습니다.");
                _state.SelectedChoiceIndex = choiceIndex;
                _state.GoldCost = choice.Cost.Gold;
                _state.GoldReward = goldReward;
                _state.SpentArtifactIds = costs.ConvertAll(artifact => artifact.Id).ToArray();
                _state.RewardArtifactId = artifactReward == null ? null : artifactReward.Id;
                _state.Progress = IsFreeExit(choice) ? EventProgress.Completed : EventProgress.Result;
            }
            catch
            {
                // 거래 중 예외는 일부 변경 여부를 알 수 없으므로 같은 선택을 다시 실행하지 않는다.
                _faulted = true;
                throw;
            }
            finally { _isApplying = false; }
        }
        finally
        {
            if (ReferenceEquals(_runCancellation, lifetime)) IsProcessing = false;
        }

        // 저장 실패 시 결과 적용을 반복하지 않고 현재 상태의 저장만 재시도한다.
        await SaveCheckpointAsync(selectionToken);
        if (IsCompleted) _completion.TrySetResult();
        return true;
    }

    private EventContext CreateContext(EventChoiceData choice)
        => new EventContext(_artifactManager, _selectionUI, choice.Cost, _random);

    private bool CanPay(EventChoiceData choice, ArtifactData selectedCost, out string reason)
    {
        reason = null;
        if (!_artifactManager.IsInitialized || !_currencyManager.IsInitialized)
            reason = "재화·유물 정보를 사용할 수 없습니다.";
        if (reason != null) return false;
        foreach (EventConditionData condition in choice.Conditions)
        {
            bool hasArtifact = _artifactManager.Contains(condition.Artifact);
            if (hasArtifact == (condition.Type == EventConditionType.HasArtifact)) continue;
            reason = condition.Type == EventConditionType.HasArtifact
                ? $"{condition.Artifact.DisplayName} 유물이 필요합니다."
                : $"{condition.Artifact.DisplayName} 유물을 보유 중이면 선택할 수 없습니다.";
            return false;
        }
        if (!_currencyManager.CanSpend(CurrencyType.Gold, choice.Cost.Gold))
        {
            reason = "골드가 부족합니다.";
            return false;
        }
        var costs = CountArtifacts(choice.Cost.Artifacts);
        if (selectedCost != null)
        {
            costs.TryGetValue(selectedCost, out int count);
            costs[selectedCost] = count + 1;
        }
        foreach (var cost in costs)
        {
            if (_artifactManager.TryGetById(cost.Key.Id, out ArtifactInstance owned) &&
                owned.Data == cost.Key && owned.StackCount >= cost.Value) continue;
            reason = $"소모할 {cost.Key.DisplayName} 유물이 {cost.Value}개 필요합니다.";
            return false;
        }
        return true;
    }

    private async UniTask SaveCheckpointAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_checkpointWriter == null) throw new InvalidOperationException("이벤트 저장기를 먼저 연결해주세요.");
        while (!_checkpointWriter.TrySave(out string error))
        {
            token.ThrowIfCancellationRequested();
            Debug.LogError("[EventManager] 체크포인트 저장 실패: " + error, this);
            await _ui.ShowMessageAsync(CurrentEvent, "진행 상태를 저장하지 못했습니다. 다시 시도해주세요.", "다시 시도", token);
            token.ThrowIfCancellationRequested();
        }
        token.ThrowIfCancellationRequested();
    }

    public UniTask WaitForCompletionAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsInitialized || CurrentEvent == null)
            throw new InvalidOperationException("이벤트를 준비한 뒤 완료를 기다려주세요.");
        return _completion.Task.AttachExternalCancellation(token);
    }

    public void ResetRun()
    {
        if (_isApplying) throw new InvalidOperationException("이벤트 거래 적용 중에는 초기화할 수 없습니다.");
        CancellationTokenSource previous = _runCancellation;
        UniTaskCompletionSource completion = _completion;
        _runCancellation = new CancellationTokenSource();
        _completion = null;
        CurrentEvent = null;
        _state = new EventSaveData();
        IsProcessing = false;
        IsOpen = false;
        LastFailureReason = null;
        _faulted = false;
        _ui?.Close();
        previous?.Cancel();
        previous?.Dispose();
        completion?.TrySetCanceled();
    }

    public EventSaveData CaptureSaveData()
    {
        if (!IsInitialized || IsProcessing || _faulted)
            throw new InvalidOperationException("이벤트 초기화 및 결과 처리가 끝난 뒤 저장해주세요.");
        return CopyState(_state);
    }

    public void RestoreSaveData(EventSaveData data)
    {
        if (!IsInitialized || _isApplying) throw new InvalidOperationException("이벤트 초기화 및 거래 완료 후 복원해주세요.");
        if (data == null || data.SpentArtifactIds == null || !Enum.IsDefined(typeof(EventProgress), data.Progress) ||
            data.GoldCost < 0 || data.GoldReward < 0)
            throw new ArgumentException("저장된 이벤트 상태를 확인해주세요.", nameof(data));
        bool empty = data.EventId == EventId.None;
        EventData restored = null;
        if (empty ? !string.IsNullOrEmpty(data.OccurrenceId) :
            string.IsNullOrWhiteSpace(data.OccurrenceId) || !_eventsById.TryGetValue(data.EventId, out restored))
            throw new ArgumentException("저장된 이벤트 ID 또는 발생 식별자가 올바르지 않습니다.", nameof(data));

        if (data.Progress == EventProgress.Choosing || empty)
        {
            if (data.Progress != EventProgress.Choosing || data.SelectedChoiceIndex != -1 || data.GoldCost != 0 ||
                data.GoldReward != 0 || data.SpentArtifactIds.Length != 0 || !string.IsNullOrEmpty(data.RewardArtifactId))
                throw new ArgumentException("선택 전 이벤트에 지급 결과가 저장되어 있습니다.", nameof(data));
        }
        else if (data.SelectedChoiceIndex < 0 || data.SelectedChoiceIndex >= restored.Choices.Count ||
            (data.GoldReward > 0 && !string.IsNullOrEmpty(data.RewardArtifactId)))
            throw new ArgumentException("저장된 이벤트 선택 또는 지급 결과가 올바르지 않습니다.", nameof(data));
        foreach (string artifactId in data.SpentArtifactIds)
            if (!_artifactManager.ArtifactCatalog.TryGetById(artifactId, out _))
                throw new ArgumentException("소모한 유물 ID를 찾을 수 없습니다: " + artifactId, nameof(data));
        if (!string.IsNullOrEmpty(data.RewardArtifactId) &&
            !_artifactManager.ArtifactCatalog.TryGetById(data.RewardArtifactId, out _))
            throw new ArgumentException("보상 유물 ID를 찾을 수 없습니다: " + data.RewardArtifactId, nameof(data));

        bool resumingEvent = _flow.ResumeStep == RunResumeStep.Event &&
            _flow.CurrentNode?.PostBattleEvent == PostBattleEventType.Event;
        if (resumingEvent ? empty || data.OccurrenceId != GetOccurrenceId() :
            !empty && data.Progress != EventProgress.Completed)
            throw new ArgumentException("저장된 이벤트와 런 재개 위치가 일치하지 않습니다.", nameof(data));

        ResetRun();
        CurrentEvent = restored;
        _state = CopyState(data);
        if (restored != null)
        {
            _completion = new UniTaskCompletionSource();
            if (IsCompleted) _completion.TrySetResult();
        }
    }

    private static EventSaveData CopyState(EventSaveData data) => new EventSaveData
    {
        OccurrenceId = data.OccurrenceId, EventId = data.EventId, Progress = data.Progress,
        SelectedChoiceIndex = data.SelectedChoiceIndex, GoldCost = data.GoldCost, GoldReward = data.GoldReward,
        SpentArtifactIds = (string[])data.SpentArtifactIds.Clone(), RewardArtifactId = data.RewardArtifactId
    };

    private string GetOccurrenceId() => $"{_flow.RunId}:{_flow.CurrentQuarter}:{_flow.CurrentWave}";

    private string BuildResultText()
    {
        var text = new StringBuilder();
        if (_state.GoldCost > 0) text.AppendLine($"골드 {_state.GoldCost} 소모");
        foreach (string id in _state.SpentArtifactIds)
        {
            _artifactManager.ArtifactCatalog.TryGetById(id, out ArtifactData artifact);
            text.AppendLine($"{artifact.DisplayName} 1개 소모");
        }
        if (_state.GoldReward > 0) text.AppendLine($"골드 {_state.GoldReward} 획득");
        if (!string.IsNullOrEmpty(_state.RewardArtifactId))
        {
            _artifactManager.ArtifactCatalog.TryGetById(_state.RewardArtifactId, out ArtifactData artifact);
            text.AppendLine($"{artifact.DisplayName} 획득");
        }
        else if (_state.GoldReward == 0) text.AppendLine("획득한 보상이 없습니다.");
        return text.ToString().TrimEnd();
    }

    private bool TryValidateEvents(out string error)
    {
        error = null;
        foreach (EventData data in _events ?? Array.Empty<EventData>())
        {
            if (data == null || data.Id == EventId.None || !Enum.IsDefined(typeof(EventId), data.Id) ||
                _eventsById.ContainsKey(data.Id))
                return ValidationError("이벤트 목록의 빈 항목 또는 미지정·미정의·중복 ID를 확인해주세요.", out error);
            if (data.Choices == null || data.Choices.Count < 1 || data.Choices.Count > 3)
                return ValidationError($"{data.Id}: 선택지는 1~3개여야 합니다.", out error);
            bool hasExit = false;
            foreach (EventChoiceData choice in data.Choices)
            {
                if (choice == null || choice.Conditions == null || choice.Cost == null || choice.Cost.Gold < 0 ||
                    choice.Cost.Artifacts == null || choice.Action == null)
                    return ValidationError($"{data.Id}: 선택지의 조건·비용·전략을 확인해주세요.", out error);
                if (!choice.Action.TryValidate(_artifactManager.ArtifactCatalog, out error)) return false;
                foreach (EventConditionData condition in choice.Conditions)
                    if (condition == null || !Enum.IsDefined(typeof(EventConditionType), condition.Type) ||
                        !_artifactManager.ArtifactCatalog.Contains(condition.Artifact))
                        return ValidationError($"{data.Id}: 유물 조건과 카탈로그 등록을 확인해주세요.", out error);
                foreach (ArtifactData artifact in choice.Cost.Artifacts)
                    if (!_artifactManager.ArtifactCatalog.Contains(artifact))
                        return ValidationError($"{data.Id}: 비용 유물의 카탈로그 등록을 확인해주세요.", out error);
                foreach (var cost in CountArtifacts(choice.Cost.Artifacts))
                    if (cost.Value > cost.Key.MaxStacks)
                        return ValidationError($"{data.Id}: 유물 비용이 최대 보유 수량을 초과합니다.", out error);
                hasExit |= IsFreeExit(choice);
            }
            if (!hasExit) return ValidationError($"{data.Id}: 조건·비용·보상이 없는 나가기 선택지가 필요합니다.", out error);
            _eventsById.Add(data.Id, data);
        }
        return true;
    }

    private static bool IsFreeExit(EventChoiceData choice) => choice.Conditions.Count == 0 &&
        choice.Cost.Gold == 0 && choice.Cost.Artifacts.Count == 0 && choice.Action.IsFreeExit;

    private static Dictionary<ArtifactData, int> CountArtifacts(IReadOnlyList<ArtifactData> artifacts)
    {
        var counts = new Dictionary<ArtifactData, int>();
        foreach (ArtifactData artifact in artifacts)
        {
            counts.TryGetValue(artifact, out int count);
            counts[artifact] = count + 1;
        }
        return counts;
    }

    private bool Fail(string reason)
    {
        LastFailureReason = reason;
        return false;
    }

    private static bool ValidationError(string message, out string error)
    {
        error = message;
        return false;
    }

    private void OnDestroy()
    {
        IsInitialized = false;
        ResetRun();
        _runCancellation.Dispose();
    }
}
