// Current date KDH 2026-09-28
using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using UnityEngine;

[Serializable]
public class TraitRunSaveData
{
    public List<TraitLevelEntry> Levels = new List<TraitLevelEntry>();
    // 이번 Run의 시작 효과를 모두 지급했는지 저장합니다.
    public bool IsApplied;
}
/// <summary>
/// Run 시작 시 특성 레벨만큼 효과를 적용하고, Run 종료 시 해제합니다.
/// PhaseChanged 이벤트만 구독하며 Update를 사용하지 않습니다.
/// </summary>
public class TraitRunApplier : MonoBehaviour, ISaveDataProvider<TraitRunSaveData>
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
    private bool _startEffectApplied;

    // Current date KDH 2026-09-29
    // unitStatModifierManager는 SpawnManager가 사용하는 것과 같은 인스턴스여야 스폰 유닛에 반영됩니다.
    // 10.9 / 문규성 / 처음 초기화할 때 테스트 레벨도 SetLevels로 복사해 저장과 효과 적용이 같은 목록을 사용하도록 변경했습니다.
    // 외부에서 전달하거나 복원한 레벨이 있으면 테스트 레벨로 덮어쓰지 않습니다.
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
        if (!_hasLevels)
        {
            SetLevels(_testLevels);
        }
        _gameFlow.PhaseChanged += HandlePhaseChanged;
    }

    // 10.9 / 문규성 / 현재 특성 레벨과 시작 재화 지급 완료 여부를 저장 데이터로 만드는 기능을 추가했습니다.
    // 목록과 각 항목을 새로 복사해 이후 레벨 변경이 저장 사본에 영향을 주지 않도록 합니다.
    public TraitRunSaveData CaptureSaveData()
    {
        TraitRunSaveData data = new TraitRunSaveData
        {
            IsApplied = _startEffectApplied
        };

        foreach (var entry in _levels)
        {
            data.Levels.Add(new TraitLevelEntry
            {
                Id = entry.Id,
                Level = entry.Level
            });
        }

        return data;
    }

    // 10.9 / 문규성 / 저장된 레벨로 교체한 뒤 기존 효과를 해제하고 지속 효과를 즉시 다시 등록하도록 변경했습니다.
    // 시작 재화는 전달받은 IsApplied가 false일 때 지급하므로, 같은 false 데이터를 다시 복원하면 다시 지급됩니다.
    public void RestoreSaveData(TraitRunSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        SetLevels(data.Levels);
        ClearAll();
        _startEffectApplied = data.IsApplied;
        if (!ApplyPersistentEffects())
        {
            throw new InvalidOperationException("특성 지속 효과를 복원하지 못했습니다.");
        }
        ApplyStartEffect();
    }

    // Current date KDH 2026-09-29
    // RestoreSaveData와 지속/시작 효과 분리로 대체된 dev 경로는 중복 적용을 막기 위해 주석으로 보존합니다.
    /*
    // 새 게임은 효과와 시작 재화를 함께 넣습니다.
    private void ApplyAll()
    {
        TryApplyEffects(true);
    }

    // Current date KDH 2026-10-08
    // 이어하기는 시작 재화가 세이브 잔액에 이미 있으므로 효과만 등록합니다.
    // 여기서 _isApplied를 올려 두면 다음 Preparation이 시작 재화를 다시 넣지 않습니다.
    public bool TryApplyOngoingEffects()
    {
        return TryApplyEffects(false);
    }

    // 효과 목록은 로드·준비 때 한 번만 순회합니다. Update에서 레벨을 다시 읽지 않습니다.
    private bool TryApplyEffects(bool grantStartingCurrencies)
    {
        if (_isApplied) return true;
        if (_catalog == null || _effectManager == null)
        {
            Debug.LogError("[OutGame/TraitRunApplier] 특성 효과를 적용하려면 초기화가 필요합니다.", this);
            return false;
        }

        _isApplied = true;
        List<TraitLevelEntry> levels = _hasLevels ? _levels : _testLevels;
        if (levels == null)
        {
            return true;
        }

        bool restored = true;
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
            if (!RegisterTraitEffects(trait, level))
            {
                restored = false;
                continue;
            }

            // 기존 AddCurrencyEffects / AddStatModifiers는 보존하되 직접 적용 경로는 사용하지 않습니다.
            if (grantStartingCurrencies) GrantStartingCurrencies(trait, level);
            _appliedTraits.Add(trait);
        }

        Debug.Log($"[OutGame/TraitRunApplier] 특성 효과를 적용했습니다. 적용 수: {_appliedTraits.Count}", this);
        return restored;
    }
    */

    // 이번 Run 동안 유지할 재화·스탯 효과를 등록합니다.
    // 10.9 / 문규성 / ApplyAll에서 지속 효과 적용을 분리했습니다.
    // 기존 출처 목록으로 중복 등록을 막고 런 종료 시 해제하며, 시작 지급 여부와 별도로 적용 상태를 관리합니다.
    private bool ApplyPersistentEffects()
    {
        if (_isApplied)
        {
            return true;
        }

        if (_catalog == null || _effectManager == null)
        {
            Debug.LogError("[OutGame/TraitRunApplier] 특성 효과를 적용하려면 초기화가 필요합니다.", this);
            return false;
        }

        bool restored = true;
        foreach (var entry in _levels)
        {
            if (entry.Level <= 0)
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
            if (!RegisterTraitEffects(trait, level))
            {
                restored = false;
                continue;
            }

            // 통합 등록 후 재화만 다시 등록하면 같은 Source의 스탯이 지워지므로 중복 호출을 보존만 합니다.
            // AddCurrencyEffects(trait, level);
            // 공통 스폰 수집은 ArtifactInstance만 읽으므로 특성 스탯은 직접 전달합니다.
            if (trait.StatEffects != null && trait.StatEffects.Count > 0 && !AddStatModifiers(trait, level))
            {
                restored = false;
            }
            _appliedTraits.Add(trait);
        }

        if (!restored)
        {
            // 일부만 적용된 상태가 다음 Preparation에서 성공으로 바뀌지 않도록 정리합니다.
            ClearAll();
            return false;
        }

        _isApplied = true;
        Debug.Log($"[OutGame/TraitRunApplier] 특성 효과를 적용했습니다. 적용 수: {_appliedTraits.Count}", this);
        return true;
    }

    // Tier/Faction을 유지한 스탯과 레벨별 재화 보정을 같은 Source에 한 번에 등록합니다.
    private bool RegisterTraitEffects(TraitData trait, int level)
    {
        if (!_effectManager.TryConvertEffects(trait,
            new EffectDataGroup { CurrencyEffects = trait.CurrencyEffects }, level, out var currencyEffects))
        {
            Debug.LogError($"[OutGame/TraitRunApplier] 특성 재화 효과 변환 실패. ID: {trait.Id}", trait);
            return false;
        }

        var allies = new List<AllyStatModifier>();
        var enemies = new List<EnemyStatModifier>();
        IReadOnlyList<TraitStatEffect> effects = trait.StatEffects;
        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                TraitStatEffect effect = effects[i];
                float value = effect.ValuePerLevel * level;
                if (!effect.IsValidTarget() || float.IsNaN(value) || float.IsInfinity(value))
                {
                    Debug.LogError($"[OutGame/TraitRunApplier] 잘못된 스탯 효과를 건너뜁니다. ID: {trait.Id}, Index: {i}", trait);
                    continue;
                }

                if (effect.TargetTeam == UnitTeam.Ally)
                    allies.Add(new AllyStatModifier(trait, effect.ApplyType, effect.AllyClass, effect.AllyType,
                        effect.StatType, effect.ModifierType, value, effect.AllyTier));
                else
                    enemies.Add(new EnemyStatModifier(trait, effect.ApplyType, effect.EnemyClass, effect.EnemyType,
                        effect.StatType, effect.ModifierType, value, effect.EnemyFaction));
            }
        }

        _effectManager.RegisterEffects(trait, new ConvertedEffects(allies, enemies,
            new List<CurrencyModifier>(currencyEffects.CurrencyModifiers)));
        return true;
    }

    // 게임 시작 시 한 번만 재화를 지급합니다.
    // 10.9 / 문규성 / 기존 GrantStartingCurrencies를 재사용하도록 시작 재화 지급을 지속 효과 등록과 분리했습니다.
    // 복원한 지급 완료 상태와 중복 없이 기록한 특성 출처를 사용해 이미 처리한 시작 재화를 다시 지급하지 않습니다.
    private void ApplyStartEffect()
    {
        if (_startEffectApplied)
        {
            return;
        }

        foreach (TraitData trait in _appliedTraits)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                TraitLevelEntry entry = _levels[i];
                if (entry.Id != trait.Id || entry.Level <= 0)
                {
                    continue;
                }

                GrantStartingCurrencies(trait, Mathf.Min(entry.Level, trait.MaxLevel));
                break;
            }
        }

        _startEffectApplied = true;
    }

    // 10.9 / 문규성 / Preparation에서는 지속 효과와 시작 효과 중 아직 처리하지 않은 부분만 각각 적용하도록 변경했습니다.
    // None과 Finished에서는 기존 효과를 해제하고 시작 지급 완료 상태도 초기화해 다음 런에서 다시 지급할 수 있게 합니다.
    private void HandlePhaseChanged(GamePhase phase)
    {
        // BeginRun 직후 첫 Preparation에서 1회만 적용합니다.
        // 스탯 보정은 스폰 시점(BattlePreparing)에 한 번 계산되므로 그보다 먼저 등록해야 합니다.
        if (phase == GamePhase.Preparation)
        {
            if (ApplyPersistentEffects())
            {
                ApplyStartEffect();
            }
            return;
        }

        if (phase == GamePhase.None || phase == GamePhase.Finished)
        {
            ClearAll();
            _startEffectApplied = false;
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

    // RegisterTraitEffects가 재화와 스탯을 함께 등록하므로 재화 단독 등록은 주석으로 보존합니다.
    /*
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
    */

    // 공통 스폰 수집은 ArtifactInstance만 읽으므로 특성 스탯을 직접 전달합니다.
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
            // EffectManager 보관분과 직접 전달한 유닛 스탯을 같은 Source로 해제합니다.
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
