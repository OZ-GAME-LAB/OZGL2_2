// Current date KDH 2026-09-29
using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using UnityEngine;

/// <summary>
/// 이번 Run의 토템을 Stat, Currency, Flow로 나누어 적용하고 끝날 때 해제합니다.
/// 추가 혈석 퍼센트는 여기서 계산만 합니다. 정산 혈석이 나온 뒤에 TryGrantBonus로 지급합니다.
/// PhaseChanged만 구독하며 Update에서 레벨을 확인하지 않습니다.
/// 저장 파일 입출력은 하지 않습니다. 세이브 담당이 CaptureSaveData와 RestoreSaveData만 호출합니다.
/// </summary>
public class TotemRunApplier : MonoBehaviour, ISaveDataProvider<TotemRunSaveData>
{
    [Tooltip("아웃게임 시작 정보 없이 인게임 씬만 테스트할 때 사용할 레벨입니다. SetLevels가 호출되면 무시됩니다.")]
    [SerializeField] private List<TotemLevelEntry> _testLevels = new List<TotemLevelEntry>();

    // 이번 Run의 추가 혈석 퍼센트입니다. 격노 3레벨(10%×3)이면 30입니다.
    public int RewardBonusPercent => _rewardBonusPercent;

    // Current date KDH 2026-10-02
    // 아웃게임에서 레벨 1 이상으로 켠 토템입니다. UI는 Data와 Level만 읽습니다.
    public IReadOnlyList<ActiveTotem> ActiveTotems => _activeTotems;

    public TotemFlowEffects FlowEffects { get; } = new TotemFlowEffects();


    private TotemEffectCatalog _catalog;
    private UnitStatModifierManager _unitStatModifierManager;
    private EffectManager _effectManager;
    private GameFlowController _gameFlow;

    private readonly EffectConverter _effectConverter = new EffectConverter();

    // 아웃게임에서 전달받은 레벨 사본입니다. 원본 목록이 나중에 바뀌어도 이번 Run에 영향이 없습니다.
    private readonly List<TotemLevelEntry> _levels = new List<TotemLevelEntry>();
    private bool _hasLevels;

    // Current date KDH 2026-10-02
    // 선택을 받을 때 한 번 채우고 다시 씁니다. Update마다 새로 만들지 않습니다.
    private readonly List<ActiveTotem> _activeTotems = new List<ActiveTotem>();

    // 이번 Run에 스탯을 넣은 토템입니다. 리스트를 재사용해 Run마다 새로 만들지 않습니다.
    private readonly List<TotemId> _appliedIds = new List<TotemId>();
    private readonly List<object> _appliedSources = new List<object>();

    private int _rewardBonusPercent;
    private bool _bonusGranted;
    private bool _isApplied;

    // unitStatModifierManager는 SpawnManager가 사용하는 것과 같은 인스턴스여야 스폰 유닛에 반영됩니다.
    // 실제 효과 수치와 보상 계산을 위해 TotemData 카탈로그가 필요합니다.
    // 10.9 / 문규성 / 외부 레벨이 없으면 테스트 레벨을 SetLevels로 복사해 저장에도 같은 목록을 사용하도록 변경했습니다.
    public void Initialize(
        TotemEffectCatalog catalog,
        UnitStatModifierManager unitStatModifierManager,
        EffectManager effectManager,
        GameFlowController gameFlow)
    {
        if (gameFlow == null)
        {
            Debug.LogError("[OutGame/TotemRunApplier] GameFlowController 참조가 필요합니다.", this);
            return;
        }

        if (unitStatModifierManager == null)
        {
            Debug.LogWarning("[OutGame/TotemRunApplier] UnitStatModifierManager 참조가 없습니다. 유닛 스탯 효과를 적용할 수 없습니다.", this);
        }

        if (effectManager == null)
        {
            Debug.LogWarning("[OutGame/TotemRunApplier] EffectManager 참조가 없습니다. 궁핍의 골드 감소를 적용할 수 없습니다.", this);
        }

        if (catalog == null)
        {
            Debug.LogWarning("[OutGame/TotemRunApplier] TotemEffectCatalog가 없습니다. 표시 수치를 읽을 수 없어 토템을 적용할 수 없습니다.", this);
        }

        // 재초기화 시 이벤트가 두 번 구독되지 않도록 먼저 해제합니다.
        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged -= HandlePhaseChanged;
        }

        _catalog = catalog;
        _unitStatModifierManager = unitStatModifierManager;
        _effectManager = effectManager;
        _gameFlow = gameFlow;
        if (!_hasLevels)
        {
            SetLevels(_testLevels);
        }
        _gameFlow.PhaseChanged += HandlePhaseChanged;
        BuildActiveTotems();
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
            if (_isApplied)
            {
                Debug.Log($"[OutGame/TotemRunApplier] 토템 스탯을 해제했습니다. 이번 Run 혈석 보너스: {_rewardBonusPercent}%", this);
            }

            ClearAll();
        }
    }

    // Current date KDH 2026-10-08
    // 이어하기는 상점·이벤트에서도 효과가 필요하므로 Preparation을 기다리지 않습니다.
    // 토템 적용은 재화를 넣지 않고, 이미 적용됐으면 목록을 다시 돌지 않습니다.

    // OutGameStartContext.Totems를 그대로 넘기면 됩니다. BeginRun 전에 호출해야 이번 Run에 반영됩니다.
    public void SetLevels(IReadOnlyList<TotemLevelEntry> levels)
    {
        _levels.Clear();
        if (levels != null)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                TotemLevelEntry entry = levels[i];
                if (entry != null)
                {
                    int level = entry.Level;
                    if (_catalog != null && _catalog.TryGetTotem(entry.Id, out TotemData totem) && !totem.IsAvailable)
                        level = 0;
                    _levels.Add(new TotemLevelEntry { Id = entry.Id, Level = level });
                }
            }
        }

        _hasLevels = true;
        BuildActiveTotems();
    }

    // Current date KDH 2026-10-02
    // 저장 시점에만 목록을 새로 만듭니다. Update마다 만들지 않으므로 프레임 중 쓰레기 수집이 늘지 않습니다.
    // 살아있는 _levels를 그대로 넘기면, 저장 뒤에 레벨이 바뀌었을 때 이미 써 둔 데이터까지 같이 바뀝니다.
    public TotemRunSaveData CaptureSaveData()
    {
        TotemRunSaveData data = new TotemRunSaveData();
        for (int i = 0; i < _levels.Count; i++)
        {
            TotemLevelEntry entry = _levels[i];
            if (entry == null)
            {
                continue;
            }

            data.Totems.Add(new TotemLevelEntry { Id = entry.Id, Level = entry.Level });
        }

        return data;
    }

    // Current date KDH 2026-10-02
    // 레벨과 지속 효과를 즉시 되돌립니다. Preparation 이외의 단계에서도 이어갈 수 있습니다.
    // 세이브 담당은 GameFlowController.Continue()보다 먼저 호출해야 합니다.
    // 10.9 / 문규성 / 저장된 레벨로 교체한 뒤 기존 효과를 해제하고 ApplyAll로 즉시 다시 적용하도록 변경했습니다.
    // 준비 단계가 아닌 곳에서 이어가도 토템효과를 복원합니다.
    public void RestoreSaveData(TotemRunSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (_gameFlow == null)
        {
            throw new InvalidOperationException("토템 저장 데이터를 복원하려면 초기화가 필요합니다.");
        }

        // 복원은 필요한 전달 대상이 없으면 실패로 알립니다. 표시 수치를 가진 토템 데이터도 반드시 있어야 합니다.
        if (data.Totems != null)
        {
            foreach (TotemLevelEntry entry in data.Totems)
            {
                if (entry == null || entry.Id == TotemId.None || entry.Level <= 0) continue;
                TotemData totem = null;
                if (_catalog != null) _catalog.TryGetTotem(entry.Id, out totem);
                if (totem == null) throw new InvalidOperationException($"표시 수치를 가진 TotemData가 없습니다. ID: {entry.Id}");
                if (!totem.IsAvailable) continue;
                TotemEffectCategory category = totem.Category;
                if (category == TotemEffectCategory.Currency && _effectManager == null)
                {
                    throw new InvalidOperationException("EffectManager가 없어 토템 재화 효과를 복원하지 못했습니다.");
                }

                IReadOnlyList<TotemStatEffect> effects = _catalog != null
                    ? _catalog.GetStatEffects(entry.Id)
                    : TotemBuiltinEffects.Get(entry.Id);
                if (category == TotemEffectCategory.Stat && effects != null && effects.Count > 0 && _unitStatModifierManager == null)
                {
                    throw new InvalidOperationException($"UnitStatModifierManager가 없어 토템 스탯을 복원하지 못했습니다. ID: {entry.Id}");
                }
            }
        }

        SetLevels(data.Totems);
        ClearAll();
        ApplyAll();
    }

    // 정산으로 받은 혈석에 이번 Run 퍼센트를 곱한 추가분입니다. 10혈석의 10%는 1입니다.
    public int CalculateBonusBloodstone(int settlementBloodstone)
    {
        if (settlementBloodstone <= 0 || _rewardBonusPercent <= 0)
        {
            return 0;
        }

        long amount = (long)settlementBloodstone * _rewardBonusPercent / 100L;
        if (amount <= 0)
        {
            return 0;
        }

        return (int)Math.Min(amount, int.MaxValue);
    }

    // Current date KDH 2026-10-02
    // 레벨 1 이상만 카탈로그의 TotemData와 함께 담습니다. 보너스 수치는 계산하지 않습니다.
    private void BuildActiveTotems()
    {
        _activeTotems.Clear();
        if (_catalog == null)
        {
            return;
        }

        List<TotemLevelEntry> levels = _hasLevels ? _levels : _testLevels;
        if (levels == null)
        {
            return;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            TotemLevelEntry entry = levels[i];
            if (entry == null || entry.Id == TotemId.None || entry.Level <= 0)
            {
                continue;
            }

            if (ContainsActiveId(entry.Id))
            {
                continue;
            }

            if (!_catalog.TryGetTotem(entry.Id, out TotemData totem) || totem == null)
            {
                Debug.LogWarning($"[OutGame/TotemRunApplier] 활성 토템의 TotemData가 없습니다. ID: {entry.Id}", this);
                continue;
            }

            if (!totem.IsAvailable) continue;

            int level = Mathf.Min(entry.Level, totem.MaxLevel);
            if (level <= 0)
            {
                continue;
            }

            _activeTotems.Add(new ActiveTotem { Data = totem, Level = level });
        }
    }

    // Current date KDH 2026-10-02
    private bool ContainsActiveId(TotemId id)
    {
        for (int i = 0; i < _activeTotems.Count; i++)
        {
            if (_activeTotems[i].Data != null && _activeTotems[i].Data.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    // 같은 Run의 보너스를 두 번 넣지 않습니다. 정산 혈석이 0이면 나중에 다시 호출할 수 있습니다.
    public bool TryGrantBonus(PersistentCurrencyManager wallet, int settlementBloodstone)
    {
        if (_bonusGranted)
        {
            return false;
        }

        int amount = CalculateBonusBloodstone(settlementBloodstone);
        if (amount <= 0)
        {
            return false;
        }

        if (wallet == null)
        {
            Debug.LogWarning("[OutGame/TotemRunApplier] PersistentCurrencyManager가 없어 추가 혈석을 지급하지 못했습니다.", this);
            return false;
        }

        if (!wallet.TryAdd(CurrencyType.Bloodstone, amount))
        {
            Debug.LogError($"[OutGame/TotemRunApplier] 추가 혈석 지급에 실패했습니다. Amount: {amount}", this);
            return false;
        }

        _bonusGranted = true;
        Debug.Log($"[OutGame/TotemRunApplier] 토템 추가 혈석을 지급했습니다. +{amount} ({_rewardBonusPercent}%)", this);
        return true;
    }

    // 10.9 / 문규성 / 복원 직후 이미 적용한 효과는 다시 등록하지 않고, 기존 적용을 마친 뒤 완료 상태를 기록하도록 변경했습니다.
    private void ApplyAll()
    {
        if (_isApplied)
        {
            return;
        }
        _bonusGranted = false;
        _rewardBonusPercent = 0;

        List<TotemLevelEntry> levels = _hasLevels ? _levels : _testLevels;
        if (levels == null)
        {
            return;
        }

        int appliedStatCount = 0;
        for (int i = 0; i < levels.Count; i++)
        {
            TotemLevelEntry entry = levels[i];
            if (entry == null || entry.Id == TotemId.None || entry.Level <= 0)
            {
                continue;
            }

            // 같은 ID가 두 번 들어와도 스탯과 혈석이 중복되지 않도록 막습니다.
            if (_appliedIds.Contains(entry.Id))
            {
                continue;
            }

            TotemData totem = null;
            if (_catalog != null)
            {
                _catalog.TryGetTotem(entry.Id, out totem);
            }

            if (totem == null)
            {
                Debug.LogError($"[OutGame/TotemRunApplier] 표시 수치를 가진 TotemData가 없습니다. ID: {entry.Id}", this);
                continue;
            }
            if (!totem.IsAvailable) continue;

            int level = Mathf.Min(entry.Level, totem.MaxLevel);
            if (level <= 0)
            {
                continue;
            }

            _appliedIds.Add(entry.Id);
            AddRewardPercent(totem, entry.Id, level);
            TotemEffectCategory category = totem.Category;
            switch (category)
            {
                case TotemEffectCategory.Stat:
                    if (AddStatModifiers(entry.Id, totem, level)) appliedStatCount++;
                    break;
                case TotemEffectCategory.Currency:
                    AddCurrencyModifiers(entry.Id, totem, level);
                    break;
                case TotemEffectCategory.Flow:
                    AddFlowEffects(totem, level);
                    break;
            }
        }

        _isApplied = true;
        Debug.Log(
            $"[OutGame/TotemRunApplier] 토템을 적용했습니다. 스탯 적용 수: {appliedStatCount}, 혈석 보너스: {_rewardBonusPercent}%",
            this);
    }

    private void AddRewardPercent(TotemData totem, TotemId id, int level)
    {
        if (totem == null)
        {
            Debug.LogWarning($"[OutGame/TotemRunApplier] 혈석 보너스를 계산할 TotemData가 없습니다. ID: {id}", this);
            return;
        }

        // Current date KDH 2026-10-02
        // 결산 항목과 같은 퍼센트를 합산합니다. 10% × 3레벨 = 30처럼 정수로 모읍니다.
        int bonus = totem.GetRewardBonusPercent(level);
        if (bonus <= 0)
        {
            return;
        }

        long sum = (long)_rewardBonusPercent + bonus;
        _rewardBonusPercent = (int)Math.Min(sum, int.MaxValue);
    }

    // 카탈로그는 적용 대상과 종류를, TotemData는 공통 수치를 제공합니다.
    private bool AddStatModifiers(TotemId id, TotemData totem, int level)
    {
        IReadOnlyList<TotemStatEffect> effects = _catalog != null
            ? _catalog.GetStatEffects(id)
            : TotemBuiltinEffects.Get(id);
        if (effects == null || effects.Count == 0)
        {
            return false;
        }

        if (_unitStatModifierManager == null)
        {
            Debug.LogWarning($"[OutGame/TotemRunApplier] UnitStatModifierManager가 없어 스탯 효과를 적용하지 못했습니다. ID: {id}", this);
            return false;
        }

        // TotemData를 출처로 사용해 복원/종료 시 기존 보정을 제거합니다.
        object source = totem;

        bool added = false;
        for (int i = 0; i < effects.Count; i++)
        {
            TotemStatEffect effect = effects[i];
            float value = totem.GetStatValuePerLevel(effect) * level;
            if (!effect.IsValidTarget() || float.IsNaN(value) || float.IsInfinity(value))
            {
                Debug.LogError($"[OutGame/TotemRunApplier] 잘못된 스탯 효과를 건너뜁니다. ID: {id}, Index: {i}", this);
                continue;
            }

            // struct라서 new를 해도 힙 할당(GC)이 생기지 않습니다.
            if (effect.TargetTeam == UnitTeam.Ally)
            {
                _unitStatModifierManager.AddAllyModifier(new AllyStatModifier(
                    source, effect.ApplyType, effect.AllyClass, effect.AllyType,
                    effect.StatType, effect.ModifierType, value, effect.AllyTier));
            }
            else
            {
                _unitStatModifierManager.AddEnemyModifier(new EnemyStatModifier(
                    source, effect.ApplyType, effect.EnemyClass, effect.EnemyType,
                    effect.StatType, effect.ModifierType, value, effect.EnemyFaction));
            }

            added = true;
        }

        if (added)
        {
            _appliedSources.Add(source);
        }

        return added;
    }

    private void AddCurrencyModifiers(TotemId id, TotemData totem, int level)
    {
        if (_effectManager == null)
        {
            Debug.LogWarning("[OutGame/TotemRunApplier] EffectManager가 없어 재화 효과를 적용하지 못했습니다.", this);
            return;
        }

        object source = totem;
        List<CurrencyModifier> modifiers = _effectConverter.ConvertCurrencyEffects(source, totem.CurrencyEffects, level);
        if (modifiers == null) return;
        _effectManager.RegisterEffects(source, new ConvertedEffects(null, null, modifiers));
        _appliedSources.Add(source);
    }

    private void AddFlowEffects(TotemData totem, int level)
    {
        if (totem == null) return;
        FlowEffects.AddEnemyCountPercent(OutGameEffectValue.Resolve(totem.ValuePerLevel, true) * level);
    }

    private void ClearAll()
    {
        if (_unitStatModifierManager != null)
        {
            for (int i = 0; i < _appliedSources.Count; i++)
            {
                _unitStatModifierManager.RemoveModifiersBySource(_appliedSources[i]);
            }
        }

        // Current date KDH 2026-10-08
        // 궁핍 재화 보정도 같은 출처로 제거합니다. 등록되지 않은 출처는 EffectManager가 무시합니다.
        if (_effectManager != null)
        {
            for (int i = 0; i < _appliedSources.Count; i++)
            {
                _effectManager.RemoveEffects(_appliedSources[i]);
            }
        }

        _appliedSources.Clear();
        _appliedIds.Clear();
        FlowEffects.Clear();
        _isApplied = false;
    }

    private void OnDestroy()
    {
        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged -= HandlePhaseChanged;
        }

        ClearAll();
    }


}

// Current date KDH 2026-10-02
// 인게임 저장에 실을 이번 판 토템입니다. TotemData 참조는 담지 않습니다.
[Serializable]
public class TotemRunSaveData
{
    public List<TotemLevelEntry> Totems = new List<TotemLevelEntry>();
}

// Current date KDH 2026-10-02
// 아웃게임에서 켠 토템입니다. 표시 문구와 보너스 계산은 담지 않습니다.
public struct ActiveTotem
{
    public TotemData Data;
    public int Level;
}
