using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using OZGL.KDH;
using UnityEngine;

// EconomyTestScene 전용 도구. 재화 변경은 매니저의 공개 API로만 수행합니다.
public class CurrencyTestPanel : MonoBehaviour
{
    [SerializeField] private RunCurrencyManager _run;
    [SerializeField] private WaveController _waveController;
    // 켜면 외부에서 초기화한 WaveController 사용. 인스펙터에 같은 컴포넌트 연결
    [SerializeField] private bool _useInitializedWaveController = true;
    [SerializeField] private BuildingCoreProgress _buildingCoreProgress;
    [SerializeField] private EffectManager _effectManager;
    [SerializeField] private ArtifactTestPanel _artifactTestPanel;
    [SerializeField] private PersistentCurrencyManager _persistent;
    [SerializeField] private CurrencyData _gold;
    [SerializeField] private CurrencyData _gem;
    [SerializeField] private CurrencyData _bloodstone;

    private bool _subscribed;
    [SerializeField] private ShopTestPanel _shopTestPanel;
    private bool _waitingForContent;
    private int _eventCount;
    private CurrencyData _lastCurrency;
    private int _lastBefore;
    private int _lastAfter;
    private CurrencyLifetime _lastSource;
    private string _lastEvent = "아직 변경 이벤트가 없습니다.";
    private string _result = "Run 시작 버튼으로 테스트를 시작하세요.";
    private string _lastWaveReward = "마지막 웨이브 지급: 없음";
    private Vector2 _scrollPosition;
    private int _passed;
    private int _failed;
    private GameFlowController _testFlow;

    private void Start() => Subscribe();

    private void OnEnable()
    {
        // 연결된 매니저에 구독하며 중복 구독은 Subscribe에서 방지합니다.
        if (_run != null)
            Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || _run == null || _persistent == null)
            return;

        _run.BalanceChanged += OnRunChanged;
        _persistent.BalanceChanged += OnPersistentChanged;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (!_subscribed)
            return;

        if (_run != null) _run.BalanceChanged -= OnRunChanged;
        if (_persistent != null) _persistent.BalanceChanged -= OnPersistentChanged;
        _subscribed = false;
    }

    private bool Ready()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _run == null || _persistent == null ||
            _gold == null || _gem == null || _bloodstone == null ||
            _waveController == null || _buildingCoreProgress == null)
        {
            _result = "Play 모드에서 매니저·재화·BuildingCoreProgress 연결을 확인하세요.";
            return false;
        }

        Subscribe();
        return true;
    }

    private void OnRunChanged(CurrencyData currency, int before, int after)
        => RecordEvent(CurrencyLifetime.Run, currency, before, after);

    private void OnPersistentChanged(CurrencyData currency, int before, int after)
        => RecordEvent(CurrencyLifetime.Persistent, currency, before, after);

    private void RecordEvent(CurrencyLifetime source, CurrencyData currency, int before, int after)
    {
        _eventCount++;
        _lastSource = source;
        _lastCurrency = currency;
        _lastBefore = before;
        _lastAfter = after;
        _lastEvent = $"{source} | {currency.DisplayName}: {before} → {after}";
        Debug.Log($"[Economy/Test] 이벤트 #{_eventCount} | {_lastEvent}", this);
    }

    [ContextMenu("Test/Start Run (Inspector Settings)")]
    public void StartRun()
    {
        if (!Ready()) return;
        if (_waitingForContent) return;
        bool newRun = !_run.IsInitialized;
        if (newRun && _shopTestPanel != null) { _shopTestPanel.ResetPanel(); }
        _persistent.Initialize(waveController: _waveController, gameFlowController: null, effectManager: _effectManager);
        _run.Initialize(waveController: _waveController, gameFlowController: null, effectManager: _effectManager, buildingCoreProgress: _buildingCoreProgress);
        if (_run.IsInitialized)
        {
            if (newRun && !_useInitializedWaveController)
            {
                if (_testFlow != null)
                {
                    _testFlow.ResetRun();
                }
                else
                {
                    _waveController.BeginRun();
                }
                _lastWaveReward = "마지막 웨이브 지급: 없음";
            }
            if (!EnsureWaveStarted())
            {
                Report("웨이브 시작 — 외부 초기화 또는 Wave Catalog 설정 확인", false);
                return;
            }
            if (_artifactTestPanel != null)
            {
                _artifactTestPanel.Initialize(_waveController, _effectManager);
            }
        }
        Report("Run 시작", _run.IsInitialized);
    }

    [ContextMenu("Test/End Run")]
    public void EndRun()
    {
        if (!Ready())
        {
            return;
        }
        if (_artifactTestPanel != null)
        {
            _artifactTestPanel.EndRun();
        }
        _waitingForContent = false;
        if (_shopTestPanel != null) { _shopTestPanel.ResetPanel(); }
        Report("Run 종료", _run.TryEndRun());
    }

    private void Report(string action, bool succeeded)
    {
        _result = $"{action}: {(succeeded ? "성공" : "실패")}";
        Debug.Log($"[Economy/Test] {_result}", this);
    }

    // 세 패널을 한 화면에서 배치. 작은 화면은 전체 UI를 같은 비율로 축소
    private void OnGUI()
    {
        float scale = Mathf.Min(1f, Screen.width / 1200f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float height = Mathf.Max(150, Screen.height / scale - 24);
        GUILayout.BeginArea(new Rect(12, 12, 376, height), GUI.skin.box);
        DrawCurrencyPanel();
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(400, 12, 376, height), GUI.skin.box);
        if (_artifactTestPanel != null)
        {
            _artifactTestPanel.DrawPanel(_run != null && _run.IsInitialized);
        }
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(788, 12, 400, height), GUI.skin.box);
        if (_shopTestPanel != null)
        {
            _shopTestPanel.DrawPanel();
        }
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }

    private void DrawCurrencyPanel()
    {
        GUILayout.Label("재화 / 진행");
        if (!Ready())
        {
            GUILayout.Label(_result);
            return;
        }
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        GUILayout.Label($"현재: {_waveController.CurQuarter}분기 / {_waveController.CurWave}웨이브");
        GUILayout.Label($"골드 {_run.GetBalance(CurrencyType.Gold)} / 보석 {_run.GetBalance(CurrencyType.Gem)}");
        GUILayout.Label($"혈석 {_persistent.GetBalance(CurrencyType.Bloodstone)}");
        GUILayout.Label(_result);
        if (!_run.IsInitialized)
        {
            if (GUILayout.Button("Run 시작"))
            {
                StartRun();
                GUIUtility.ExitGUI();
            }
        }
        else
        {
            if (GUILayout.Button("Run 종료"))
            {
                EndRun();
                GUIUtility.ExitGUI();
            }
            if (!_waitingForContent)
            {
                if (GUILayout.Button("웨이브 보상 지급"))
                {
                    BeginWaveReward();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("이번 웨이브 = 상점"))
                {
                    bool opened = _shopTestPanel != null && _shopTestPanel.TryOpen(CompleteContent);
                    _waitingForContent = opened;
                    Report("상점 열기", opened);
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                GUILayout.Label("아티팩트 선택 또는 상점 종료를 기다리는 중");
            }
            if (GUILayout.Button("테스트 골드 +1000"))
            {
                Report("골드 지급", _run.TryAdd(CurrencyType.Gold, 1000));
                GUIUtility.ExitGUI();
            }
        }
        GUILayout.Label(_lastWaveReward);
        GUILayout.EndScrollView();
    }

    private void BeginWaveReward()
    {
        if (_waitingForContent || _artifactTestPanel == null || !EnsureWaveStarted() ||
            !_artifactTestPanel.TryPrepareReward())
        {
            Report("보상 후보 준비", false);
            return;
        }
        if (!TryApplyCurrentWaveReward())
        {
            _artifactTestPanel.CancelReward();
            Report("웨이브 보상 지급", false);
            return;
        }
        _waitingForContent = true;
        _artifactTestPanel.ShowReward(_run.CurrentGoldReward, _run.CurrentGemReward, CompleteContent);
        Report("웨이브 보상 지급 — 아티팩트를 선택하세요", true);
    }

    private void CompleteContent()
    {
        if (!_waitingForContent || !_run.IsInitialized)
        {
            return;
        }
        int quarter = _waveController.CurQuarter;
        int wave = _waveController.CurWave;
        if (_waveController.IsLastWave)
        {
            _waveController.ProgressQuarter();
        }
        else
        {
            _waveController.ProgressStage();
        }
        _waitingForContent = false;
        Report("다음 웨이브 진행", quarter != _waveController.CurQuarter || wave != _waveController.CurWave);
    }
    private void DrawBalances(string label, IReadOnlyDictionary<CurrencyData, int> balances)
    {
        if (balances == null)
        {
            GUILayout.Label($"{label} Balances: null (지갑 없음)");
            return;
        }

        foreach (var entry in balances)
        {
            string iconName = entry.Key.Icon != null ? entry.Key.Icon.name : "미설정";
            GUILayout.Label($"{label} 목록 — {entry.Key.DisplayName}: {entry.Value} / 아이콘: {iconName}");
        }
    }

    private void DrawCurrencyButtons(CurrencyData currency, int amount, bool persistent)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"{currency.DisplayName} +{amount}"))
            Report("지급", persistent ? _persistent.TryAdd(currency.Type, amount) : _run.TryAdd(currency.Type, amount));
        if (GUILayout.Button($"{currency.DisplayName} -{amount}"))
            Report("소비", persistent ? _persistent.TrySpend(currency.Type, amount) : _run.TrySpend(currency.Type, amount));
        GUILayout.EndHorizontal();
    }

    [ContextMenu("Test/Run Basic Checks (Resets Run)")]
    public void RunChecks()
    {
        RunChecksAsync().Forget();
    }

    private async UniTask RunChecksAsync()
    {
        if (!Ready()) return;
        if (_effectManager != null && _effectManager.CurrencyModifiers.Count > 0)
        {
            _result = "기본 검증은 효과 없는 수량 기준입니다. 아티팩트 탭에서 아티팩트를 초기화하고 다른 재화 효과도 제거하세요.";
            Debug.LogWarning($"[Economy/Test] {_result}", this);
            return;
        }
        _passed = 0;
        _failed = 0;
        int savedBloodstone = _persistent.GetBalance(_bloodstone.Type);

        try
        {
            if (_run.IsInitialized) _run.TryEndRun();
            _persistent.TrySpend(_bloodstone.Type, savedBloodstone);
            int events = _eventCount;
            Check(_run.Balances == null && _run.GetBalance(CurrencyType.Gold) == 0 &&
                _run.GetBalance(CurrencyType.Gem) == 0, "초기화 전 Balances null 및 Type 조회 0");
            Check(!_run.CanSpend(_gold.Type, 0), "Run 시작 전 소비 불가");
            _persistent.Initialize(waveController: _waveController, gameFlowController: null, effectManager: _effectManager);
            _run.Initialize(waveController: _waveController, gameFlowController: null, effectManager: _effectManager, buildingCoreProgress: _buildingCoreProgress);
            Check(_run.IsInitialized, "Run 초기화");
            if (!_run.IsInitialized)
                throw new InvalidOperationException("Run 초기화 실패: 카탈로그 및 기본 시작 재화 설정을 확인하세요.");
            EnsureWaveStarted();
            int startingGold = _run.GetBalance(_gold.Type);
            int startingGem = _run.GetBalance(_gem.Type);
            Check(_eventCount == events, "초기화 중 변경 이벤트 없음");
            var initializedBalances = _run.Balances;
            _run.Initialize(waveController: _waveController, gameFlowController: null, effectManager: _effectManager, buildingCoreProgress: _buildingCoreProgress);
            Check(_run.IsInitialized && ReferenceEquals(initializedBalances, _run.Balances) &&
                _run.GetBalance(_gold.Type) == startingGold &&
                _run.GetBalance(_gem.Type) == startingGem && _eventCount == events, "중복 초기화 시 잔액 및 이벤트 유지");

            // Inspector 설정은 수정하지 않고 이번 지갑의 잔액만 검사에 맞게 준비합니다.
            PrepareRunBalance(_gold, 500);
            PrepareRunBalance(_gem, 1);
            var firstRunBalances = _run.Balances;
            CheckBalanceAccess(_gold, CurrencyType.Gold);
            CheckBalanceAccess(_gem, CurrencyType.Gem);
            CheckBalanceAccess(_bloodstone, CurrencyType.Bloodstone);
            CheckChange("소비 가능 조회", _gold, () => _run.CanSpend(_gold.Type, 500), true, 500);
            CheckChange("골드 지급", _gold, () => _run.TryAdd(_gold.Type, 100), true, 600);
            CheckChange("골드 소비", _gold, () => _run.TrySpend(_gold.Type, 500), true, 100);
            CheckChange("잔액 부족", _gold, () => _run.TrySpend(_gold.Type, 101), false, 100);
            CheckChange("0 지급", _gold, () => _run.TryAdd(_gold.Type, 0), true, 100);
            CheckChange("0 소비", _gold, () => _run.TrySpend(_gold.Type, 0), true, 100);
            CheckChange("음수 지급", _gold, () => _run.TryAdd(_gold.Type, -1), false, 100);
            CheckChange("음수 소비", _gold, () => _run.TrySpend(_gold.Type, -1), false, 100);
            CheckChange("지급 오버플로", _gold, () => _run.TryAdd(_gold.Type, int.MaxValue), false, 100);
            CheckChange("보석 소비", _gem, () => _run.TrySpend(_gem.Type, 1), true, 0);
            CheckChange("혈석 지급", _bloodstone, () => _persistent.TryAdd(_bloodstone.Type, 30), true, 30);
            CheckChange("혈석 조회", _bloodstone, () => _persistent.CanSpend(_bloodstone.Type, 30), true, 30);
            CheckChange("혈석 소비", _bloodstone, () => _persistent.TrySpend(_bloodstone.Type, 10), true, 20);
            CheckChange("혈석 부족", _bloodstone, () => _persistent.TrySpend(_bloodstone.Type, 21), false, 20);
            CheckChange("혈석 0 지급", _bloodstone, () => _persistent.TryAdd(_bloodstone.Type, 0), true, 20);
            CheckChange("혈석 0 소비", _bloodstone, () => _persistent.TrySpend(_bloodstone.Type, 0), true, 20);
            CheckChange("혈석 음수 지급", _bloodstone, () => _persistent.TryAdd(_bloodstone.Type, -1), false, 20);
            CheckChange("혈석 음수 소비", _bloodstone, () => _persistent.TrySpend(_bloodstone.Type, -1), false, 20);
            CheckChange("혈석 오버플로", _bloodstone, () => _persistent.TryAdd(_bloodstone.Type, int.MaxValue), false, 20);
            // 웨이브 보상은 연결된 WaveController의 현재 위치를 사용합니다.
            Check(TryApplyCurrentWaveReward(), "현재 분기 / 웨이브 보상 지급");
            CheckBalanceAccess(_gold, CurrencyType.Gold);
            CheckBalanceAccess(_gem, CurrencyType.Gem);
            PrepareRunBalance(_gold, 100);
            PrepareRunBalance(_gem, 0);
            CheckReward("생산 보상", _gold, _run.TryApplyProductionReward);
            await CheckSettlementReward();
            events = _eventCount;
            Check(_run.TryEndRun() && !_run.IsInitialized && _run.GetBalance(_gold.Type) == 0 &&
                _run.GetBalance(_gem.Type) == 0 && _persistent.GetBalance(_bloodstone.Type) == 20 && _eventCount == events,
                "Run 종료 시 Run 재화만 폐기");
            Check(_run.Balances == null && _persistent.Balances != null &&
                _persistent.Balances.TryGetValue(_bloodstone, out int remainingBloodstone) && remainingBloodstone == 20,
                "종료 후 Run 목록 null, Persistent 목록 유지");
            Check(!_run.CanSpend(_gold.Type, 1), "종료 후 소비 불가");
            _run.Initialize(waveController: _waveController, gameFlowController: null, effectManager: _effectManager, buildingCoreProgress: _buildingCoreProgress);
            Check(_run.IsInitialized && _run.GetBalance(_gold.Type) == startingGold &&
                _run.GetBalance(_gem.Type) == startingGem && _eventCount == events,
                "다음 Run은 최초 시작 잔액으로 초기화하고 이벤트 없음");
            Check(_run.Balances != null && !ReferenceEquals(firstRunBalances, _run.Balances),
                "재시작 시 새로운 Balances 제공");
        }
        catch (Exception exception)
        {
            Check(false, $"예외 발생: {exception.Message}");
        }
        finally
        {
            if (_run.IsInitialized) _run.TryEndRun();
            bool cleared = _persistent.TrySpend(_bloodstone.Type, _persistent.GetBalance(_bloodstone.Type));
            Check(cleared && _persistent.TryAdd(_bloodstone.Type, savedBloodstone) &&
                _persistent.GetBalance(_bloodstone.Type) == savedBloodstone, "테스트 전 혈석 복원");
        }

        _result = $"기본 검증 완료: PASS {_passed}, FAIL {_failed} — Run 비활성";
        if (_failed == 0) Debug.Log($"[Economy/Test] {_result}", this);
        else Debug.LogError($"[Economy/Test] {_result}", this);
    }

    private bool EnsureWaveStarted()
    {
        // 외부 초기화 모드에서는 진행 상태만 확인하고 재초기화하지 않음
        if (_useInitializedWaveController)
        {
            return _waveController != null &&
                _waveController.CurQuarter > 0 && _waveController.CurWave > 0;
        }

        if (_waveController.CurQuarter < 1 || _waveController.CurWave < 1)
        {
            // 실제 플로우가 연결된 경우 먼저 기존 BeginRun 사용
            _waveController.BeginRun();
            if (_waveController.CurQuarter < 1 && _testFlow == null)
            {
                // 독립 테스트 씬도 현재 진행 상태를 소유하는 GameFlow 연결 필요
                var host = new GameObject("EconomyTestFlow");
                host.transform.SetParent(transform);
                _testFlow = host.AddComponent<GameFlowController>();
                var waiting = host.AddComponent<TestWaitingScript>();
                waiting.Initialize(_testFlow, _waveController);
                _testFlow.Initialize(_waveController, waiting,
                    _artifactTestPanel != null ? _artifactTestPanel.Artifacts : null);
                _testFlow.BeginRun();
            }
        }
        return _waveController.CurQuarter > 0 && _waveController.CurWave > 0;
    }

    private bool TryApplyCurrentWaveReward(bool advanceAfterReward = false)
    {
        if (!_run.IsInitialized)
            return false;

        if (!EnsureWaveStarted())
        {
            return false;
        }
        int quarter = _waveController.CurQuarter;
        int wave = _waveController.CurWave;
        // 게임 플로우 없는 테스트 씬에서는 준비 단계를 직접 실행
        if (!_run.TryPrepareWaveReward() || !_run.TryApplyWaveReward())
            return false;

        int rewardQuarter = Mathf.Min(quarter, WaveController.MAIN_QUARTERS);
        _lastWaveReward = $"마지막 웨이브 지급: {quarter}분기 / {wave}웨이브 (보상 테이블: {rewardQuarter}분기)";
        Debug.Log($"[Economy/Test] {_lastWaveReward}", this);

        if (advanceAfterReward)
        {
            if (_waveController.IsLastWave)
            {
                _waveController.ProgressQuarter();
            }
            else
            {
                _waveController.ProgressStage();
            }
        }

        return true;
    }

    private bool _settlementPending;

    private async UniTask ApplySettlementAsync()
    {
        if (!Ready() || _settlementPending)
        {
            return;
        }
        _settlementPending = true;
        int before = _persistent.GetBalance(_bloodstone.Type);
        await _persistent.TryApplyReward();
        int after = _persistent.GetBalance(_bloodstone.Type);
        _result = $"정산 호출 완료: 혈석 {before} → {after} (실패 여부는 Console 확인)";
        _settlementPending = false;
    }

    private async UniTask CheckSettlementReward()
    {
        int before = _persistent.GetBalance(_bloodstone.Type);
        int clearedWaves = (_waveController.CurQuarter - 1) * WaveController.MAX_WAVE + _waveController.CurWave;
        int expectedReward = clearedWaves * (1 + clearedWaves / WaveController.MAX_WAVE);
        await _persistent.TryApplyReward();
        int balance = _persistent.GetBalance(_bloodstone.Type);
        Check(balance == before + expectedReward, "현재 웨이브 포함 임시 정산 수량 확인");
        if (balance < before || !_persistent.TrySpend(_bloodstone.Type, balance - before))
        {
            throw new InvalidOperationException("혈석 정산 검사 후 잔액 복원 실패");
        }
    }
    private void CheckBalanceAccess(CurrencyData currency, CurrencyType expectedType)
    {
        var balances = currency.Lifetime == CurrencyLifetime.Run ? _run.Balances : _persistent.Balances;
        int balance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency.Type) : _persistent.GetBalance(currency.Type);
        int typedBalance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(expectedType) : _persistent.GetBalance(expectedType);
        Check(currency.Type == expectedType && balances != null &&
            balances.TryGetValue(currency, out int listedBalance) && listedBalance == balance && typedBalance == balance,
            $"{expectedType}: 아이콘 정보를 가진 CurrencyData 키 및 목록·Type 잔액 일치");
    }

    private void CheckReward(string label, CurrencyData currency, Func<CurrencyType, int, bool> grant)
    {
        int before = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency.Type) : _persistent.GetBalance(currency.Type);
        CheckChange(label, currency, () => grant(currency.Type, 10), true, before + 10);
        CheckChange($"{label} 0 지급", currency, () => grant(currency.Type, 0), true, before + 10);
        CheckChange($"{label} 음수 거부", currency, () => grant(currency.Type, -1), false, before + 10);
        CheckChange($"{label} 오버플로 거부", currency, () => grant(currency.Type, int.MaxValue), false, before + 10);

        int balance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency.Type) : _persistent.GetBalance(currency.Type);
        bool restored = balance >= before && (currency.Lifetime == CurrencyLifetime.Run
            ? _run.TrySpend(currency.Type, balance - before)
            : _persistent.TrySpend(currency.Type, balance - before));
        if (!restored)
            throw new InvalidOperationException($"{label} 검사 후 잔액 복원 실패");
    }

    private void PrepareRunBalance(CurrencyData currency, int targetBalance)
    {
        int balance = _run.GetBalance(currency.Type);
        bool succeeded = balance > targetBalance
            ? _run.TrySpend(currency.Type, balance - targetBalance)
            : _run.TryAdd(currency.Type, targetBalance - balance);

        if (!succeeded || _run.GetBalance(currency.Type) != targetBalance)
            throw new InvalidOperationException($"검사용 잔액 준비 실패: {currency.DisplayName}");
    }

    private void CheckChange(string label, CurrencyData currency, Func<bool> operation, bool expected, int after)
    {
        int before = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency.Type) : _persistent.GetBalance(currency.Type);
        int events = _eventCount;
        bool result = operation();
        int actual = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency.Type) : _persistent.GetBalance(currency.Type);
        int typedBalance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency.Type) : _persistent.GetBalance(currency.Type);
        var balances = currency.Lifetime == CurrencyLifetime.Run ? _run.Balances : _persistent.Balances;
        bool validLookup = balances != null && balances.TryGetValue(currency, out int listedBalance) &&
            listedBalance == after && typedBalance == after;
        bool changed = before != after;
        bool validEvent = _eventCount == events + (changed ? 1 : 0) &&
            (!changed || (_lastCurrency == currency && _lastBefore == before && _lastAfter == after && _lastSource == currency.Lifetime));
        Check(result == expected && actual == after && validEvent && validLookup,
            $"{label} / 결과·잔액·이벤트·목록·Type 조회");
    }

    private void Check(bool passed, string label)
    {
        if (passed)
        {
            _passed++;
            Debug.Log($"[Economy/Test] PASS | {label}", this);
        }
        else
        {
            _failed++;
            Debug.LogError($"[Economy/Test] FAIL | {label}", this);
        }
    }
}
