// Current date KDH 2026-09-16
using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using UnityEngine;

/// <summary>
/// 게임 시작 전 제단 1개를 선택하고, Run 동안 효과를 적용합니다.
/// Update에서 효과를 계산하지 않고 PhaseChanged 이벤트만 구독합니다.
/// </summary>
public class AltarManager : MonoBehaviour
{
    public bool IsInitialized => _catalog != null && _effectManager != null;
    public bool IsApplied => _instance != null;
    public AltarData Selected => _selected;
    public AltarInstance Instance => _instance;
    public IReadOnlyList<AltarData> AvailableAltars =>
        _catalog != null ? _catalog.Altars : Array.Empty<AltarData>();

    public event Action<AltarData> SelectionChanged;
    public event Action<AltarInstance> Applied;
    public event Action Cleared;

    [SerializeField] private AltarCatalog _catalog;

    private EffectManager _effectManager;
    private RunCurrencyManager _runCurrency;
    private UnitStatModifierManager _unitStatModifierManager;
    private GameFlowController _gameFlow;

    private AltarData _selected;
    private AltarInstance _instance;
    private bool _isApplying;

    // 참조는 한 번만 캐시합니다. 이후 PhaseChanged에서 Find를 다시 하지 않습니다.
    public void Initialize(
        EffectManager effectManager,
        RunCurrencyManager runCurrency,
        UnitStatModifierManager unitStatModifierManager,
        GameFlowController gameFlow)
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[OutGame/AltarManager] 이미 초기화되어 있습니다.", this);
            return;
        }

        if (_catalog == null)
        {
            Debug.LogError("[OutGame/AltarManager] AltarCatalog 참조가 없습니다. Inspector에서 연결해주세요.", this);
            return;
        }

        if (effectManager == null)
        {
            Debug.LogError("[OutGame/AltarManager] EffectManager 참조가 없습니다.", this);
            return;
        }

        if (runCurrency == null)
        {
            Debug.LogWarning(
                "[OutGame/AltarManager] RunCurrencyManager 참조가 없습니다. 시작 지급·이자 효과를 적용할 수 없습니다.",
                this);
        }

        if (unitStatModifierManager == null)
        {
            Debug.LogWarning(
                "[OutGame/AltarManager] UnitStatModifierManager 참조가 없습니다. 유닛 스탯 효과를 실제 유닛에 넣을 수 없습니다.",
                this);
        }

        if (gameFlow == null)
        {
            Debug.LogWarning(
                "[OutGame/AltarManager] GameFlowController 참조가 없습니다. 웨이브 종료 이자와 Run 종료 해제를 자동으로 처리할 수 없습니다.",
                this);
        }

        _effectManager = effectManager;
        _runCurrency = runCurrency;
        _unitStatModifierManager = unitStatModifierManager;
        _gameFlow = gameFlow;

        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged += HandlePhaseChanged;
        }

        Debug.Log("[OutGame/AltarManager] 제단 시스템을 초기화했습니다.", this);
    }

    // 적용 전에는 교체 가능합니다. 한 판에는 제단 1개만 유지합니다.
    public bool TrySelect(AltarData altar)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[OutGame/AltarManager] 초기화 후 제단을 선택할 수 있습니다.", this);
            return false;
        }

        if (_isApplying)
        {
            Debug.LogWarning("[OutGame/AltarManager] 제단 적용 중에는 선택할 수 없습니다.", this);
            return false;
        }

        if (IsApplied)
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] 이미 제단이 적용되어 있습니다. 적용 중: {GetAltarId(_instance)}",
                this);
            return false;
        }

        if (altar == null)
        {
            Debug.LogError("[OutGame/AltarManager] 선택할 제단이 null입니다.", this);
            return false;
        }

        if (!_catalog.Contains(altar))
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] Catalog에 없는 제단은 선택할 수 없습니다. ID: {altar.Id}, Asset: {altar.name}",
                altar);
            return false;
        }

        _selected = altar;
        SelectionChanged?.Invoke(altar);
        Debug.Log($"[OutGame/AltarManager] 제단을 선택했습니다. ID: {altar.Id}", this);
        return true;
    }

    public bool TryClearSelection()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[OutGame/AltarManager] 초기화 후 선택을 해제할 수 있습니다.", this);
            return false;
        }

        if (_isApplying)
        {
            Debug.LogWarning("[OutGame/AltarManager] 제단 적용 중에는 선택을 해제할 수 없습니다.", this);
            return false;
        }

        if (IsApplied)
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] 적용된 제단은 Run 종료 후 선택을 바꿀 수 있습니다. 적용 중: {GetAltarId(_instance)}",
                this);
            return false;
        }

        _selected = null;
        SelectionChanged?.Invoke(null);
        Debug.Log("[OutGame/AltarManager] 제단 선택을 해제했습니다.", this);
        return true;
    }

    // 선택분을 EffectManager·유닛 보정에 등록하고 OnRunStart 지급을 한 번 실행합니다.
    public bool TryApplySelected()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[OutGame/AltarManager] 초기화 후 제단을 적용할 수 있습니다.", this);
            return false;
        }

        if (_isApplying)
        {
            Debug.LogWarning("[OutGame/AltarManager] 제단 적용이 이미 진행 중입니다.", this);
            return false;
        }

        if (IsApplied)
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] 이미 제단이 적용되어 있습니다. 적용 중: {GetAltarId(_instance)}",
                this);
            return false;
        }

        if (_selected == null)
        {
            Debug.LogWarning("[OutGame/AltarManager] 선택된 제단이 없습니다. TrySelect 이후 적용해주세요.", this);
            return false;
        }

        AltarInstance instance = new AltarInstance(_selected);
        if (instance.Data == null)
        {
            Debug.LogError("[OutGame/AltarManager] 제단 인스턴스에 AltarData가 없습니다.", this);
            return false;
        }

        if (!_effectManager.TryConvertEffects(
            instance,
            _selected.UnitStatEffects,
            _selected.CurrencyEffects,
            1,
            out ConvertedEffects converted))
        {
            Debug.LogError(
                $"[OutGame/AltarManager] 제단 효과 변환에 실패했습니다. ID: {_selected.Id}. 유닛/재화 효과 설정을 확인해주세요.",
                _selected);
            return false;
        }

        // 변환이 끝난 뒤에만 보유 상태를 바꿉니다. 실패하면 기존 선택을 유지합니다.
        _isApplying = true;
        _instance = instance;
        _effectManager.RegisterEffects(instance, converted);
        AddUnitModifiers(converted);
        ApplyTriggered(AltarTriggerMoment.OnRunStart);
        _isApplying = false;

        Applied?.Invoke(instance);
        Debug.Log($"[OutGame/AltarManager] 제단을 적용했습니다. ID: {_selected.Id}", this);
        return true;
    }

    // Run 종료·리셋 시 호출합니다. 선택은 남겨 다음 판에서 다시 적용할 수 있습니다.
    public bool TryClearApplied()
    {
        if (!IsInitialized)
        {
            return false;
        }

        if (_isApplying)
        {
            Debug.LogWarning("[OutGame/AltarManager] 제단 적용 중에는 효과를 해제할 수 없습니다.", this);
            return false;
        }

        if (_instance == null)
        {
            return false;
        }

        AltarInstance instance = _instance;
        string altarId = GetAltarId(instance);
        _instance = null;
        _effectManager.RemoveEffects(instance);
        if (_unitStatModifierManager != null)
        {
            _unitStatModifierManager.RemoveModifiersBySource(instance);
        }

        Cleared?.Invoke();
        Debug.Log($"[OutGame/AltarManager] 제단 효과를 해제했습니다. ID: {altarId}", this);
        return true;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        // Reward = 웨이브 승리 후입니다. 패배 경로에는 Reward가 없습니다.
        if (phase == GamePhase.Reward)
        {
            ApplyTriggered(AltarTriggerMoment.OnWaveCleared);
            return;
        }

        // None/Finished면 이번 판 적용만 해제합니다. 선택 자체는 OutGame 상태로 유지합니다.
        if (phase == GamePhase.None || phase == GamePhase.Finished)
        {
            TryClearApplied();
        }
    }

    private void ApplyTriggered(AltarTriggerMoment moment)
    {
        if (_instance == null)
        {
            return;
        }

        if (_instance.Data == null)
        {
            Debug.LogError("[OutGame/AltarManager] 적용된 제단에 AltarData가 없습니다.", this);
            return;
        }

        IReadOnlyList<AltarTriggeredEffect> effects = _instance.Data.TriggeredEffects;
        if (effects == null)
        {
            Debug.LogError(
                $"[OutGame/AltarManager] 트리거 효과 목록이 없습니다. ID: {_instance.Data.Id}",
                _instance.Data);
            return;
        }

        if (!HasTriggeredEffect(effects, moment))
        {
            return;
        }

        if (_runCurrency == null)
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] RunCurrencyManager 참조가 없어 트리거 효과를 지급할 수 없습니다. ID: {_instance.Data.Id}, Moment: {moment}",
                this);
            return;
        }

        if (!_runCurrency.IsInitialized)
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] Run 재화가 초기화되기 전에 트리거 효과를 지급할 수 없습니다. ID: {_instance.Data.Id}, Moment: {moment}",
                this);
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            AltarTriggeredEffect effect = effects[i];
            if (effect.Moment != moment)
            {
                continue;
            }

            int amount = CalculateAmount(effect, i);
            if (amount <= 0)
            {
                continue;
            }

            if (!_runCurrency.TryAdd(effect.CurrencyType, amount))
            {
                Debug.LogError(
                    $"[OutGame/AltarManager] 재화 지급에 실패했습니다. ID: {_instance.Data.Id}, Index: {i}, Currency: {effect.CurrencyType}, Amount: {amount}, Moment: {moment}",
                    _instance.Data);
            }
        }
    }

    private int CalculateAmount(AltarTriggeredEffect effect, int index)
    {
        if (effect.Calc == AltarTriggerCalc.None || effect.Moment == AltarTriggerMoment.None)
        {
            Debug.LogError(
                $"[OutGame/AltarManager] 트리거 시점 또는 계산 방식이 없습니다. ID: {_instance.Data.Id}, Index: {index}",
                _instance.Data);
            return 0;
        }

        if (effect.CurrencyType == CurrencyType.None)
        {
            Debug.LogError(
                $"[OutGame/AltarManager] 트리거 재화 종류가 없습니다. ID: {_instance.Data.Id}, Index: {index}",
                _instance.Data);
            return 0;
        }

        if (float.IsNaN(effect.Value) || float.IsInfinity(effect.Value))
        {
            Debug.LogError(
                $"[OutGame/AltarManager] 트리거 수량은 유한한 값이어야 합니다. ID: {_instance.Data.Id}, Index: {index}, Value: {effect.Value}",
                _instance.Data);
            return 0;
        }

        if (effect.Calc == AltarTriggerCalc.FlatGrant)
        {
            if (effect.Value <= 0f || effect.Value > int.MaxValue)
            {
                Debug.LogError(
                    $"[OutGame/AltarManager] FlatGrant 수량이 올바르지 않습니다. ID: {_instance.Data.Id}, Index: {index}, Value: {effect.Value}",
                    _instance.Data);
                return 0;
            }

            return (int)effect.Value;
        }

        if (effect.Calc == AltarTriggerCalc.PercentOfBalance)
        {
            int balance = _runCurrency.GetBalance(effect.CurrencyType);
            double amount = balance * (double)effect.Value;
            if (amount < 0d || amount > int.MaxValue)
            {
                Debug.LogError(
                    $"[OutGame/AltarManager] 이자 계산 결과가 올바르지 않습니다. ID: {_instance.Data.Id}, Index: {index}, Balance: {balance}, Value: {effect.Value}",
                    _instance.Data);
                return 0;
            }

            // 잔액 0이거나 소수점 이하만 있으면 지급하지 않습니다. 설정 오류가 아니므로 로그를 남기지 않습니다.
            return (int)amount;
        }

        Debug.LogError(
            $"[OutGame/AltarManager] 알 수 없는 트리거 계산 방식입니다. ID: {_instance.Data.Id}, Index: {index}, Calc: {effect.Calc}",
            _instance.Data);
        return 0;
    }

    private void AddUnitModifiers(ConvertedEffects converted)
    {
        // EffectManager는 보정치만 보관하므로, 실제 유닛 합산은 공개 API로 직접 넣습니다.
        bool hasUnitModifiers = converted != null
            && ((converted.AllyModifiers != null && converted.AllyModifiers.Count > 0)
                || (converted.EnemyModifiers != null && converted.EnemyModifiers.Count > 0));

        if (!hasUnitModifiers)
        {
            return;
        }

        if (_unitStatModifierManager == null)
        {
            Debug.LogWarning(
                $"[OutGame/AltarManager] UnitStatModifierManager 참조가 없어 유닛 스탯 효과를 적용하지 못했습니다. ID: {GetAltarId(_instance)}",
                this);
            return;
        }

        if (converted.AllyModifiers != null)
        {
            for (int i = 0; i < converted.AllyModifiers.Count; i++)
            {
                _unitStatModifierManager.AddAllyModifier(converted.AllyModifiers[i]);
            }
        }

        if (converted.EnemyModifiers != null)
        {
            for (int i = 0; i < converted.EnemyModifiers.Count; i++)
            {
                _unitStatModifierManager.AddEnemyModifier(converted.EnemyModifiers[i]);
            }
        }
    }

    private static bool HasTriggeredEffect(
        IReadOnlyList<AltarTriggeredEffect> effects,
        AltarTriggerMoment moment)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Moment == moment)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetAltarId(AltarInstance instance)
    {
        if (instance == null || instance.Data == null)
        {
            return "(없음)";
        }

        return string.IsNullOrWhiteSpace(instance.Data.Id) ? instance.Data.name : instance.Data.Id;
    }

    private void OnDestroy()
    {
        if (_gameFlow != null)
        {
            _gameFlow.PhaseChanged -= HandlePhaseChanged;
        }

        TryClearApplied();
    }
}
