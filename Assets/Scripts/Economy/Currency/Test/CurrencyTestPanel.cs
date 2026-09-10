using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

// EconomyTestScene 전용 도구. 재화 변경은 매니저의 공개 API로만 수행합니다.
public class CurrencyTestPanel : MonoBehaviour
{
    [SerializeField] private RunCurrencyManager _run;
    [SerializeField] private WaveController _waveController;
    [SerializeField] private PersistentCurrencyManager _persistent;
    [SerializeField] private CurrencyData _gold;
    [SerializeField] private CurrencyData _gem;
    [SerializeField] private CurrencyData _bloodstone;

    private bool _subscribed;
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
            _waveController == null || PersistentCurrencyManager.Instance != _persistent)
        {
            _result = "테스트 씬을 단독으로 Play하고 매니저 및 재화 연결을 확인하세요.";
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
        _run.Initialize(_waveController);
        if (_run.IsInitialized)
            EnsureWaveStarted();
        Report("Run 시작", _run.IsInitialized);
    }

    [ContextMenu("Test/End Run")]
    public void EndRun()
    {
        if (Ready()) Report("Run 종료", _run.TryEndRun());
    }

    private void Report(string action, bool succeeded)
    {
        _result = $"{action}: {(succeeded ? "성공" : "실패")}";
        Debug.Log($"[Economy/Test] {_result}", this);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12, 12, 480, Mathf.Max(100, Screen.height - 24)), GUI.skin.box);
        GUILayout.Label("재화 테스트 — EconomyTestScene");
        if (!Ready())
        {
            GUILayout.Label(_result);
            GUILayout.EndArea();
            return;
        }

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        GUILayout.Label($"Run: {(_run.IsInitialized ? "활성" : "비활성")}");
        if (_waveController.CurQuarter > 0 && _waveController.CurWave > 0)
        {
            int rewardQuarter = Mathf.Min(_waveController.CurQuarter, WaveController.MAIN_QUARTERS);
            GUILayout.Label($"다음 지급: {_waveController.CurQuarter}분기 / {_waveController.CurWave}웨이브 (보상 테이블: {rewardQuarter}분기)");
        }
        else
        {
            GUILayout.Label("다음 지급: Run 시작 시 1분기 / 1웨이브 준비");
        }
        GUILayout.Label(_lastWaveReward);
        GUILayout.Label($"Type 조회 — 골드: {_run.GetBalance(CurrencyType.Gold)} / 보석: {_run.GetBalance(CurrencyType.Gem)} / 혈석: {_persistent.GetBalance(CurrencyType.Bloodstone)}");
        DrawBalances("Run", _run.Balances);
        DrawBalances("Persistent", _persistent.Balances);
        GUILayout.Label($"이벤트 수: {_eventCount}");
        GUILayout.Label(_lastEvent);
        GUILayout.Space(8);
        if (GUILayout.Button("Run 시작 — 매니저 Inspector 설정 사용")) StartRun();
        if (GUILayout.Button("Run 종료")) EndRun();
        DrawCurrencyButtons(_gold, 100, false);
        DrawCurrencyButtons(_gem, 1, false);
        DrawCurrencyButtons(_bloodstone, 10, true);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("웨이브 보상 지급 → 다음 웨이브"))
            Report("웨이브 보상", TryApplyCurrentWaveReward(advanceAfterReward: true));
        if (GUILayout.Button("생산 골드 +50"))
            Report("생산 보상", _run.TryApplyProductionReward(_gold.Type, 50));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("적 드랍 골드 +10"))
            Report("적 골드 드랍", _run.TryApplyEnemyDropReward(_gold.Type, 10));
        if (GUILayout.Button("적 드랍 보석 +1"))
            Report("적 보석 드랍", _run.TryApplyEnemyDropReward(_gem.Type, 1));
        GUILayout.EndHorizontal();
        if (GUILayout.Button("혈석 정산 (웨이브 5, 처치 10, 보스 2 → +30)"))
            Report("혈석 정산 보상", _persistent.TryApplyReward(5, 10, 2));
        if (GUILayout.Button("골드 501 소비 가능 여부 확인"))
            Report("CanSpend 골드 501", _run.CanSpend(_gold.Type, 501));
        GUILayout.Space(8);
        GUILayout.Label("시작 재화는 Inspector 설정을 사용하고, 지급·소비 검사용 잔액은 자동 준비합니다.");
        GUILayout.Label("웨이브 보상은 지급 성공 시에만 진행합니다. Wave Catalog와 보상 테이블을 연결하세요.");
        GUILayout.Label("기본 검증은 현재 Run을 초기화합니다. 혈석은 검증 후 복원합니다.");
        if (GUILayout.Button("기본 시나리오 전체 검증")) RunChecks();
        GUILayout.Label(_result);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
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
        if (!Ready()) return;
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
            _run.Initialize(_waveController);
            Check(_run.IsInitialized, "Run 초기화");
            if (!_run.IsInitialized)
                throw new InvalidOperationException("Run 초기화 실패: 카탈로그 및 기본 시작 재화 설정을 확인하세요.");
            EnsureWaveStarted();
            int startingGold = _run.GetBalance(_gold.Type);
            int startingGem = _run.GetBalance(_gem.Type);
            Check(_eventCount == events, "초기화 중 변경 이벤트 없음");
            var initializedBalances = _run.Balances;
            _run.Initialize(_waveController);
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
            CheckReward("적 골드 드랍", _gold, _run.TryApplyEnemyDropReward);
            CheckReward("적 보석 드랍", _gem, _run.TryApplyEnemyDropReward);
            CheckSettlementReward();
            events = _eventCount;
            Check(_run.TryEndRun() && !_run.IsInitialized && _run.GetBalance(_gold.Type) == 0 &&
                _run.GetBalance(_gem.Type) == 0 && _persistent.GetBalance(_bloodstone.Type) == 20 && _eventCount == events,
                "Run 종료 시 Run 재화만 폐기");
            Check(_run.Balances == null && _persistent.Balances != null &&
                _persistent.Balances.TryGetValue(_bloodstone, out int remainingBloodstone) && remainingBloodstone == 20,
                "종료 후 Run 목록 null, Persistent 목록 유지");
            Check(!_run.CanSpend(_gold.Type, 1), "종료 후 소비 불가");
            _run.Initialize(_waveController);
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

    private void EnsureWaveStarted()
    {
        if (_waveController.CurQuarter < 1 || _waveController.CurWave < 1)
        {
            _waveController.BeginRun();
        }
    }

    private bool TryApplyCurrentWaveReward(bool advanceAfterReward = false)
    {
        if (!_run.IsInitialized)
            return false;

        EnsureWaveStarted();
        int quarter = _waveController.CurQuarter;
        int wave = _waveController.CurWave;
        if (!_run.TryApplyWaveReward())
            return false;

        int rewardQuarter = Mathf.Min(quarter, WaveController.MAIN_QUARTERS);
        _lastWaveReward = $"마지막 웨이브 지급: {quarter}분기 / {wave}웨이브 (보상 테이블: {rewardQuarter}분기)";
        Debug.Log($"[Economy/Test] {_lastWaveReward}", this);

        if (advanceAfterReward)
        {
            if (_waveController.IsLastWave)
                _waveController.ProgressQuarter();
            else
                _waveController.ProgressStage();
        }

        return true;
    }

    private void CheckSettlementReward()
    {
        int before = _persistent.GetBalance(_bloodstone.Type);
        // 현재 정산식: (웨이브 클리어 수 + 유닛 처치 수) * 보스 처치 수
        CheckChange("혈석 정산 (5 + 10) * 2", _bloodstone,
            () => _persistent.TryApplyReward(5, 10, 2), true, before + 30);
        CheckChange("보스 처치 없는 정산", _bloodstone,
            () => _persistent.TryApplyReward(5, 10, 0), true, before + 30);
        CheckChange("기록 없는 정산", _bloodstone,
            () => _persistent.TryApplyReward(0, 0, 0), true, before + 30);
        CheckChange("혈석 잔액 오버플로 거부", _bloodstone,
            () => _persistent.TryApplyReward(int.MaxValue, 0, 1), false, before + 30);

        int balance = _persistent.GetBalance(_bloodstone.Type);
        if (balance < before || !_persistent.TrySpend(_bloodstone.Type, balance - before))
            throw new InvalidOperationException("혈석 정산 검사 후 잔액 복원 실패");
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
