// Current date KDH 2026-09-28
using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using UnityEngine;

/// <summary>
/// Run 시작 시 특성 레벨만큼 효과를 적용하고, Run 종료 시 해제합니다.
/// PhaseChanged 이벤트만 구독하며 Update를 사용하지 않습니다.
/// </summary>
public class TraitRunApplier : MonoBehaviour
{
    // Current date KDH 2026-09-29
    [Tooltip("아웃게임 시작 정보 없이 인게임 씬만 테스트할 때 사용할 레벨입니다. SetLevels가 호출되면 무시됩니다.")]
    [SerializeField] private List<TraitLevelEntry> _testLevels = new List<TraitLevelEntry>();

    private TraitCatalog _catalog;
    private EffectManager _effectManager;
    private UnitStatModifierManager _unitStatModifierManager;
    private RunCurrencyManager _runCurrency;
    private GameFlowController _gameFlow;

    // Current date KDH 2026-09-29
    // 아웃게임에서 전달받은 레벨 사본입니다. 원본 목록이 나중에 바뀌어도 이번 Run에 영향이 없습니다.
    private readonly List<TraitLevelEntry> _levels = new List<TraitLevelEntry>();
    private bool _hasLevels;

    // 이번 Run에 처리한 특성(Source)입니다. 리스트를 재사용해 Run마다 새로 만들지 않습니다.
    private readonly List<TraitData> _appliedTraits = new List<TraitData>();
    private bool _isApplied;

    // Current date KDH 2026-09-29
    // unitStatModifierManager는 SpawnManager가 사용하는 것과 같은 인스턴스여야 스폰 유닛에 반영됩니다.
    public void Initialize(
        TraitCatalog catalog,
        EffectManager effectManager,
        UnitStatModifierManager unitStatModifierManager,
        RunCurrencyManager runCurrency,
        GameFlowController gameFlow)
    {
        if (catalog == null || effectManager == null || gameFlow == null)
        {
            Debug.LogError("[OutGame/TraitRunApplier] TraitCatalog, EffectManager, GameFlowController 참조가 필요합니다.", this);
            return;
        }

        if (unitStatModifierManager == null)
        {
            Debug.LogWarning("[OutGame/TraitRunApplier] UnitStatModifierManager 참조가 없습니다. 유닛 스탯 효과를 적용할 수 없습니다.", this);
        }

        if (runCurrency == null)
        {
            Debug.LogWarning("[OutGame/TraitRunApplier] RunCurrencyManager 참조가 없습니다. 시작 재화를 지급할 수 없습니다.", this);
        }

        // 재초기화 시 이벤트가 두 번 구독되지 않도록 먼저 해제합니다.
        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged -= HandlePhaseChanged;
        }

        _catalog = catalog;
        _effectManager = effectManager;
        _unitStatModifierManager = unitStatModifierManager;
        _runCurrency = runCurrency;
        _gameFlow = gameFlow;
        _gameFlow.PhaseChanged += HandlePhaseChanged;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        // BeginRun 직후 첫 Preparation에서 1회만 적용합니다.
        // 스탯 보정은 스폰 시점(BattlePreparing)에 한 번 계산되므로 그보다 먼저 등록해야 합니다.
        if (phase == GamePhase.Preparation)
        {
            if (!_isApplied)
            {
                ApplyAll();
            }
            return;
        }

        if (phase == GamePhase.None || phase == GamePhase.Finished)
        {
            ClearAll();
        }
    }

    // Current date KDH 2026-09-29
    // OutGameStartContext.Traits를 그대로 넘기면 됩니다. BeginRun 전에 호출해야 이번 Run에 반영됩니다.
    public void SetLevels(IReadOnlyList<TraitLevelEntry> levels)
    {
        _levels.Clear();
        if (levels != null)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                TraitLevelEntry entry = levels[i];
                if (entry != null)
                {
                    _levels.Add(new TraitLevelEntry { Id = entry.Id, Level = entry.Level });
                }
            }
        }

        _hasLevels = true;
    }

    // Current date KDH 2026-09-29
    private void ApplyAll()
    {
        _isApplied = true;
        List<TraitLevelEntry> levels = _hasLevels ? _levels : _testLevels;
        if (levels == null)
        {
            return;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            TraitLevelEntry entry = levels[i];
            if (entry == null || entry.Level <= 0)
            {
                continue;
            }

            if (!_catalog.TryGetById(entry.Id, out TraitData trait))
            {
                Debug.LogWarning($"[OutGame/TraitRunApplier] 카탈로그에 없는 특성은 건너뜁니다. ID: {entry.Id}", this);
                continue;
            }

            // 같은 ID가 두 번 들어와도 효과·시작 재화가 중복 적용되지 않도록 막습니다.
            if (_appliedTraits.Contains(trait))
            {
                continue;
            }

            int level = Mathf.Min(entry.Level, trait.MaxLevel);
            AddCurrencyEffects(trait, level);
            AddStatModifiers(trait, level);
            GrantStartingCurrencies(trait, level);
            _appliedTraits.Add(trait);
        }

        Debug.Log($"[OutGame/TraitRunApplier] 특성 효과를 적용했습니다. 적용 수: {_appliedTraits.Count}", this);
    }

    // 재화 계산기는 EffectManager.CurrencyModifiers를 읽으므로 재화 효과만 EffectManager에 등록합니다.
    private bool AddCurrencyEffects(TraitData trait, int level)
    {
        IReadOnlyList<CurrencyEffectData> currencyEffects = trait.CurrencyEffects;
        if (currencyEffects == null || currencyEffects.Count == 0)
        {
            return false;
        }

        // 레벨을 stackCount로 넘겨 "1레벨당 수치 × 레벨"로 변환합니다.
        if (!_effectManager.TryConvertEffects(
            trait, new EffectDataGroup { CurrencyEffects = currencyEffects }, level, out ConvertedEffects converted))
        {
            Debug.LogError($"[OutGame/TraitRunApplier] 특성 재화 효과 변환 실패. ID: {trait.Id}", trait);
            return false;
        }

        _effectManager.RegisterEffects(trait, converted);
        return true;
    }

    // 스폰 시 GetAllyFinalModifier가 읽는 곳이 UnitStatModifierManager이므로 여기에 직접 넣습니다.
    // Tier/Faction 값을 생성자에 넘기지 않으면 default(Tier1)로 들어가므로 반드시 전달합니다.
    private bool AddStatModifiers(TraitData trait, int level)
    {
        IReadOnlyList<TraitStatEffect> effects = trait.StatEffects;
        if (effects == null || effects.Count == 0)
        {
            return false;
        }

        if (_unitStatModifierManager == null)
        {
            Debug.LogWarning($"[OutGame/TraitRunApplier] UnitStatModifierManager가 없어 스탯 효과를 적용하지 못했습니다. ID: {trait.Id}", this);
            return false;
        }

        bool added = false;
        for (int i = 0; i < effects.Count; i++)
        {
            TraitStatEffect effect = effects[i];
            float value = effect.ValuePerLevel * level;

            if (!effect.IsValidTarget() || float.IsNaN(value) || float.IsInfinity(value))
            {
                Debug.LogError($"[OutGame/TraitRunApplier] 잘못된 스탯 효과를 건너뜁니다. ID: {trait.Id}, Index: {i}", trait);
                continue;
            }

            // struct라서 new를 해도 힙 할당(GC)이 생기지 않습니다.
            if (effect.TargetTeam == UnitTeam.Ally)
            {
                _unitStatModifierManager.AddAllyModifier(new AllyStatModifier(
                    trait, effect.ApplyType, effect.AllyClass, effect.AllyType,
                    effect.StatType, effect.ModifierType, value, effect.AllyTier));
            }
            else
            {
                _unitStatModifierManager.AddEnemyModifier(new EnemyStatModifier(
                    trait, effect.ApplyType, effect.EnemyClass, effect.EnemyType,
                    effect.StatType, effect.ModifierType, value, effect.EnemyFaction));
            }

            added = true;
        }

        return added;
    }

    private void GrantStartingCurrencies(TraitData trait, int level)
    {
        IReadOnlyList<TraitStartingCurrency> grants = trait.StartingCurrencies;
        if (grants == null || grants.Count == 0)
        {
            return;
        }

        if (_runCurrency == null || !_runCurrency.IsInitialized)
        {
            Debug.LogWarning($"[OutGame/TraitRunApplier] Run 재화가 준비되지 않아 시작 재화를 지급하지 못했습니다. ID: {trait.Id}", this);
            return;
        }

        for (int i = 0; i < grants.Count; i++)
        {
            // long으로 곱해 int 범위를 넘는 오버플로를 막습니다.
            long amount = (long)grants[i].AmountPerLevel * level;
            if (amount <= 0)
            {
                continue;
            }

            if (!_runCurrency.TryAdd(grants[i].CurrencyType, (int)Math.Min(amount, int.MaxValue)))
            {
                Debug.LogError($"[OutGame/TraitRunApplier] 시작 재화 지급 실패. ID: {trait.Id}, Currency: {grants[i].CurrencyType}", trait);
            }
        }
    }

    private void ClearAll()
    {
        for (int i = 0; i < _appliedTraits.Count; i++)
        {
            TraitData trait = _appliedTraits[i];
            // 재화 효과가 없던 특성은 등록되지 않았으므로 false만 반환하고 넘어갑니다.
            _effectManager.RemoveEffects(trait);
            if (_unitStatModifierManager != null)
            {
                _unitStatModifierManager.RemoveModifiersBySource(trait);
            }
        }

        _appliedTraits.Clear();
        _isApplied = false;
    }

    private void OnDestroy()
    {
        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged -= HandlePhaseChanged;
        }

        if (_effectManager != null)
        {
            ClearAll();
        }
    }
}
