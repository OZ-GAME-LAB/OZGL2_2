// Current date KDH 2026-09-29
using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using UnityEngine;

/// <summary>
/// 이번 Run에 켠 토템 레벨만큼 유닛 스탯을 넣고, 끝날 때 해제합니다.
/// 추가 혈석 퍼센트는 여기서 계산만 합니다. 정산 혈석이 나온 뒤에 TryGrantBonus로 지급합니다.
/// PhaseChanged만 구독하며 Update에서 레벨을 확인하지 않습니다.
/// </summary>
public class TotemRunApplier : MonoBehaviour
{
    [Tooltip("아웃게임 시작 정보 없이 인게임 씬만 테스트할 때 사용할 레벨입니다. SetLevels가 호출되면 무시됩니다.")]
    [SerializeField] private List<TotemLevelEntry> _testLevels = new List<TotemLevelEntry>();

    // 이번 Run의 추가 혈석 퍼센트입니다. 격노 3레벨(10%×3)이면 30입니다.
    public int RewardBonusPercent => _rewardBonusPercent;

    private TotemEffectCatalog _catalog;
    private UnitStatModifierManager _unitStatModifierManager;
    private GameFlowController _gameFlow;

    // 아웃게임에서 전달받은 레벨 사본입니다. 원본 목록이 나중에 바뀌어도 이번 Run에 영향이 없습니다.
    private readonly List<TotemLevelEntry> _levels = new List<TotemLevelEntry>();
    private bool _hasLevels;

    // 이번 Run에 스탯을 넣은 토템입니다. 리스트를 재사용해 Run마다 새로 만들지 않습니다.
    private readonly List<TotemId> _appliedIds = new List<TotemId>();
    private readonly List<object> _appliedSources = new List<object>();
    // TotemData가 없을 때만 쓰는 해제용 출처입니다. ID마다 하나를 만들어 두고 다시 쓰지 않습니다.
    private readonly Dictionary<TotemId, TotemEffectSource> _fallbackSources = new Dictionary<TotemId, TotemEffectSource>();

    private int _rewardBonusPercent;
    private bool _bonusGranted;
    private bool _isApplied;

    // unitStatModifierManager는 SpawnManager가 사용하는 것과 같은 인스턴스여야 스폰 유닛에 반영됩니다.
    // catalog가 없어도 격노·취약 기본 스탯은 적용됩니다. 혈석 퍼센트는 TotemData가 있어야 계산됩니다.
    public void Initialize(
        TotemEffectCatalog catalog,
        UnitStatModifierManager unitStatModifierManager,
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

        if (catalog == null)
        {
            Debug.LogWarning("[OutGame/TotemRunApplier] TotemEffectCatalog가 없습니다. 혈석 보너스 퍼센트는 0으로 계산됩니다.", this);
        }

        // 재초기화 시 이벤트가 두 번 구독되지 않도록 먼저 해제합니다.
        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged -= HandlePhaseChanged;
        }

        _catalog = catalog;
        _unitStatModifierManager = unitStatModifierManager;
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
            if (_isApplied)
            {
                Debug.Log($"[OutGame/TotemRunApplier] 토템 스탯을 해제했습니다. 이번 Run 혈석 보너스: {_rewardBonusPercent}%", this);
            }

            ClearAll();
        }
    }

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
                    _levels.Add(new TotemLevelEntry { Id = entry.Id, Level = entry.Level });
                }
            }
        }

        _hasLevels = true;
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

    private void ApplyAll()
    {
        _isApplied = true;
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

            int level = totem != null ? Mathf.Min(entry.Level, totem.MaxLevel) : entry.Level;
            if (level <= 0)
            {
                continue;
            }

            _appliedIds.Add(entry.Id);
            AddRewardPercent(totem, entry.Id, level);
            if (AddStatModifiers(entry.Id, totem, level))
            {
                appliedStatCount++;
            }
        }

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

        float perLevel = totem.RewardBonusPerLevel;
        if (float.IsNaN(perLevel) || float.IsInfinity(perLevel) || perLevel <= 0f)
        {
            return;
        }

        // 10% × 3레벨 = 30처럼, 표시 퍼센트를 정수로 모읍니다.
        long bonus = (long)Math.Round(perLevel * level, MidpointRounding.AwayFromZero);
        if (bonus <= 0)
        {
            return;
        }

        long sum = (long)_rewardBonusPercent + bonus;
        _rewardBonusPercent = (int)Math.Min(sum, int.MaxValue);
    }

    // 군세·궁핍·메아리처럼 스탯이 아닌 토템은 목록이 비어 있어 여기서 넘어갑니다.
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

        // 스탯이 있는 토템만 해제용 출처를 만듭니다. 혈석만 있는 토템은 객체를 만들지 않습니다.
        object source = GetSource(id, totem);

        bool added = false;
        for (int i = 0; i < effects.Count; i++)
        {
            TotemStatEffect effect = effects[i];
            float value = effect.ValuePerLevel * level;
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

    private object GetSource(TotemId id, TotemData totem)
    {
        if (totem != null)
        {
            return totem;
        }

        if (!_fallbackSources.TryGetValue(id, out TotemEffectSource source))
        {
            source = new TotemEffectSource(id);
            _fallbackSources.Add(id, source);
        }

        return source;
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

        _appliedSources.Clear();
        _appliedIds.Clear();
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

    // TotemData 없이 기본 스탯만 넣을 때, 넣을 때와 뺄 때 같은 객체를 쓰기 위한 표시입니다.
    private sealed class TotemEffectSource
    {
        public TotemEffectSource(TotemId id)
        {
            Id = id;
        }

        public TotemId Id { get; }
    }
}
