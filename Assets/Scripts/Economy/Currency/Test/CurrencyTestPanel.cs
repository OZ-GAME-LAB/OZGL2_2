using System;
using System.Collections.Generic;
using UnityEngine;

// EconomyTestScene 전용 도구. 재화 변경은 매니저의 공개 API로만 수행합니다.
public class CurrencyTestPanel : MonoBehaviour
{
    [SerializeField] private RunCurrencyManager _run;
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
    private int _passed;
    private int _failed;

    private void Start() => Subscribe();

    private void OnEnable()
    {
        // 첫 활성화에서는 모든 매니저의 Awake가 끝난 Start에서 구독합니다.
        if (RunCurrencyManager.Instance == _run && _run != null)
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
            RunCurrencyManager.Instance != _run || PersistentCurrencyManager.Instance != _persistent)
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
        Report("Run 시작", _run.TryInitialize());
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
        GUILayout.BeginArea(new Rect(12, 12, 480, 640), GUI.skin.box);
        GUILayout.Label("재화 테스트 — EconomyTestScene");
        if (!Ready())
        {
            GUILayout.Label(_result);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"Run: {(_run.IsInitialized ? "활성" : "비활성")}");
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
        if (GUILayout.Button("웨이브 골드 +100"))
            Report("웨이브 보상", _run.TryApplyWaveReward(new CurrencyAmount(_gold, 100)));
        if (GUILayout.Button("생산 골드 +50"))
            Report("생산 보상", _run.TryApplyProductionReward(new CurrencyAmount(_gold, 50)));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("적 드랍 골드 +10"))
            Report("적 골드 드랍", _run.TryApplyEnemyDropReward(new CurrencyAmount(_gold, 10)));
        if (GUILayout.Button("적 드랍 보석 +1"))
            Report("적 보석 드랍", _run.TryApplyEnemyDropReward(new CurrencyAmount(_gem, 1)));
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Run 종료 보상 혈석 +30 (지급만 테스트)"))
            Report("혈석 정산 보상", _persistent.TryApplyReward(new CurrencyAmount(_bloodstone, 30)));
        if (GUILayout.Button("골드 501 소비 가능 여부 확인"))
            Report("CanSpend 골드 501", _run.CanSpend(new CurrencyAmount(_gold, 501)));
        GUILayout.Space(8);
        GUILayout.Label("시작 재화는 Inspector 설정을 사용하고, 지급·소비 검사용 잔액은 자동 준비합니다.");
        GUILayout.Label("기본 검증은 현재 Run을 초기화합니다. 혈석은 검증 후 복원합니다.");
        if (GUILayout.Button("기본 시나리오 전체 검증")) RunChecks();
        GUILayout.Label(_result);
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
        CurrencyAmount change = new CurrencyAmount(currency, amount);
        if (GUILayout.Button($"{currency.DisplayName} +{amount}"))
            Report("지급", persistent ? _persistent.TryAdd(change) : _run.TryAdd(change));
        if (GUILayout.Button($"{currency.DisplayName} -{amount}"))
            Report("소비", persistent ? _persistent.TrySpend(change) : _run.TrySpend(change));
        GUILayout.EndHorizontal();
    }

    [ContextMenu("Test/Run Basic Checks (Resets Run)")]
    public void RunChecks()
    {
        if (!Ready()) return;
        _passed = 0;
        _failed = 0;
        int savedBloodstone = _persistent.GetBalance(_bloodstone);

        try
        {
            if (_run.IsInitialized) _run.TryEndRun();
            _persistent.TrySpend(new CurrencyAmount(_bloodstone, savedBloodstone));
            int events = _eventCount;
            Check(_run.Balances == null && _run.GetBalance(CurrencyType.Gold) == 0 &&
                _run.GetBalance(CurrencyType.Gem) == 0, "초기화 전 Balances null 및 Type 조회 0");
            Check(!_run.CanSpend(new CurrencyAmount(_gold, 0)), "Run 시작 전 소비 불가");
            Check(_run.TryInitialize(), "Run 초기화");
            if (!_run.IsInitialized)
                throw new InvalidOperationException("Run 초기화 실패: 카탈로그 및 기본 시작 재화 설정을 확인하세요.");
            int startingGold = _run.GetBalance(_gold);
            int startingGem = _run.GetBalance(_gem);
            Check(_eventCount == events, "초기화 중 변경 이벤트 없음");
            Check(!_run.TryInitialize() && _run.GetBalance(_gold) == startingGold &&
                _run.GetBalance(_gem) == startingGem && _eventCount == events, "중복 초기화 시 잔액 및 이벤트 유지");

            // Inspector 설정은 수정하지 않고 이번 지갑의 잔액만 검사에 맞게 준비합니다.
            PrepareRunBalance(_gold, 500);
            PrepareRunBalance(_gem, 1);
            var firstRunBalances = _run.Balances;
            CheckBalanceAccess(_gold, CurrencyType.Gold);
            CheckBalanceAccess(_gem, CurrencyType.Gem);
            CheckBalanceAccess(_bloodstone, CurrencyType.Bloodstone);
            CheckChange("소비 가능 조회", _gold, () => _run.CanSpend(new CurrencyAmount(_gold, 500)), true, 500);
            CheckChange("골드 지급", _gold, () => _run.TryAdd(new CurrencyAmount(_gold, 100)), true, 600);
            CheckChange("골드 소비", _gold, () => _run.TrySpend(new CurrencyAmount(_gold, 500)), true, 100);
            CheckChange("잔액 부족", _gold, () => _run.TrySpend(new CurrencyAmount(_gold, 101)), false, 100);
            CheckChange("0 지급", _gold, () => _run.TryAdd(new CurrencyAmount(_gold, 0)), true, 100);
            CheckChange("0 소비", _gold, () => _run.TrySpend(new CurrencyAmount(_gold, 0)), true, 100);
            CheckChange("음수 지급", _gold, () => _run.TryAdd(new CurrencyAmount(_gold, -1)), false, 100);
            CheckChange("음수 소비", _gold, () => _run.TrySpend(new CurrencyAmount(_gold, -1)), false, 100);
            CheckChange("지급 오버플로", _gold, () => _run.TryAdd(new CurrencyAmount(_gold, int.MaxValue)), false, 100);
            CheckChange("보석 소비", _gem, () => _run.TrySpend(new CurrencyAmount(_gem, 1)), true, 0);
            CheckChange("혈석 지급", _bloodstone, () => _persistent.TryAdd(new CurrencyAmount(_bloodstone, 30)), true, 30);
            CheckChange("혈석 조회", _bloodstone, () => _persistent.CanSpend(new CurrencyAmount(_bloodstone, 30)), true, 30);
            CheckChange("혈석 소비", _bloodstone, () => _persistent.TrySpend(new CurrencyAmount(_bloodstone, 10)), true, 20);
            CheckChange("혈석 부족", _bloodstone, () => _persistent.TrySpend(new CurrencyAmount(_bloodstone, 21)), false, 20);
            CheckChange("혈석 0 지급", _bloodstone, () => _persistent.TryAdd(new CurrencyAmount(_bloodstone, 0)), true, 20);
            CheckChange("혈석 0 소비", _bloodstone, () => _persistent.TrySpend(new CurrencyAmount(_bloodstone, 0)), true, 20);
            CheckChange("혈석 음수 지급", _bloodstone, () => _persistent.TryAdd(new CurrencyAmount(_bloodstone, -1)), false, 20);
            CheckChange("혈석 음수 소비", _bloodstone, () => _persistent.TrySpend(new CurrencyAmount(_bloodstone, -1)), false, 20);
            CheckChange("혈석 오버플로", _bloodstone, () => _persistent.TryAdd(new CurrencyAmount(_bloodstone, int.MaxValue)), false, 20);
            // 현재 보상 함수는 입력 수량을 그대로 지급합니다. 계산기 연결 시 기대값도 조정합니다.
            CheckReward("웨이브 보상", _gold, _run.TryApplyWaveReward);
            CheckReward("생산 보상", _gold, _run.TryApplyProductionReward);
            CheckReward("적 골드 드랍", _gold, _run.TryApplyEnemyDropReward);
            CheckReward("적 보석 드랍", _gem, _run.TryApplyEnemyDropReward);
            CheckReward("혈석 정산 보상", _bloodstone, _persistent.TryApplyReward);
            events = _eventCount;
            Check(_run.TryEndRun() && !_run.IsInitialized && _run.GetBalance(_gold) == 0 &&
                _run.GetBalance(_gem) == 0 && _persistent.GetBalance(_bloodstone) == 20 && _eventCount == events,
                "Run 종료 시 Run 재화만 폐기");
            Check(_run.Balances == null && _persistent.Balances != null &&
                _persistent.Balances.TryGetValue(_bloodstone, out int remainingBloodstone) && remainingBloodstone == 20,
                "종료 후 Run 목록 null, Persistent 목록 유지");
            Check(!_run.CanSpend(new CurrencyAmount(_gold, 1)), "종료 후 소비 불가");
            Check(_run.TryInitialize() && _run.GetBalance(_gold) == startingGold &&
                _run.GetBalance(_gem) == startingGem && _eventCount == events,
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
            bool cleared = _persistent.TrySpend(new CurrencyAmount(_bloodstone, _persistent.GetBalance(_bloodstone)));
            Check(cleared && _persistent.TryAdd(new CurrencyAmount(_bloodstone, savedBloodstone)) &&
                _persistent.GetBalance(_bloodstone) == savedBloodstone, "테스트 전 혈석 복원");
        }

        _result = $"기본 검증 완료: PASS {_passed}, FAIL {_failed} — Run 비활성";
        if (_failed == 0) Debug.Log($"[Economy/Test] {_result}", this);
        else Debug.LogError($"[Economy/Test] {_result}", this);
    }

    private void CheckBalanceAccess(CurrencyData currency, CurrencyType expectedType)
    {
        var balances = currency.Lifetime == CurrencyLifetime.Run ? _run.Balances : _persistent.Balances;
        int balance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency) : _persistent.GetBalance(currency);
        int typedBalance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(expectedType) : _persistent.GetBalance(expectedType);
        Check(currency.Type == expectedType && balances != null &&
            balances.TryGetValue(currency, out int listedBalance) && listedBalance == balance && typedBalance == balance,
            $"{expectedType}: 아이콘 정보를 가진 CurrencyData 키 및 목록·SO·Type 잔액 일치");
    }

    private void CheckReward(string label, CurrencyData currency, Func<CurrencyAmount, bool> grant)
    {
        int before = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency) : _persistent.GetBalance(currency);
        CheckChange(label, currency, () => grant(new CurrencyAmount(currency, 10)), true, before + 10);
        CheckChange($"{label} 0 지급", currency, () => grant(new CurrencyAmount(currency, 0)), true, before + 10);
        CheckChange($"{label} 음수 거부", currency, () => grant(new CurrencyAmount(currency, -1)), false, before + 10);
        CheckChange($"{label} 오버플로 거부", currency, () => grant(new CurrencyAmount(currency, int.MaxValue)), false, before + 10);

        int balance = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency) : _persistent.GetBalance(currency);
        bool restored = balance >= before && (currency.Lifetime == CurrencyLifetime.Run
            ? _run.TrySpend(new CurrencyAmount(currency, balance - before))
            : _persistent.TrySpend(new CurrencyAmount(currency, balance - before)));
        if (!restored)
            throw new InvalidOperationException($"{label} 검사 후 잔액 복원 실패");
    }

    private void PrepareRunBalance(CurrencyData currency, int targetBalance)
    {
        int balance = _run.GetBalance(currency);
        bool succeeded = balance > targetBalance
            ? _run.TrySpend(new CurrencyAmount(currency, balance - targetBalance))
            : _run.TryAdd(new CurrencyAmount(currency, targetBalance - balance));

        if (!succeeded || _run.GetBalance(currency) != targetBalance)
            throw new InvalidOperationException($"검사용 잔액 준비 실패: {currency.DisplayName}");
    }

    private void CheckChange(string label, CurrencyData currency, Func<bool> operation, bool expected, int after)
    {
        int before = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency) : _persistent.GetBalance(currency);
        int events = _eventCount;
        bool result = operation();
        int actual = currency.Lifetime == CurrencyLifetime.Run ? _run.GetBalance(currency) : _persistent.GetBalance(currency);
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
