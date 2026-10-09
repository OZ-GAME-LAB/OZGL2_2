// Current date KDH 2026-09-16
using System;
using System.Collections.Generic;
using Game.Core;
using Units;
using UnityEngine;

/// <summary>
/// 게임 시작 전 제단 1개를 선택하고, Run 동안 효과를 적용합니다.
/// Update에서 효과를 계산하지 않고 PhaseChanged 이벤트만 구독합니다.
/// 저장 파일 입출력은 하지 않습니다. 세이브 담당이 CaptureSaveData와 RestoreSaveData만 호출합니다.
/// </summary>
public class AltarManager : MonoBehaviour, ISaveDataProvider<AltarRunSaveData>
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

        if (gameFlow == null)
        {
            Debug.LogWarning(
                "[OutGame/AltarManager] GameFlowController 참조가 없습니다. 웨이브 종료 이자와 Run 종료 해제를 자동으로 처리할 수 없습니다.",
                this);
        }

        _effectManager = effectManager;
        _runCurrency = runCurrency;
        // 기존 직접 적용 함수 호환용으로 보존합니다. 현재 적용은 EffectManager 공통 연결에서 담당합니다.
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

    // Current date KDH 2026-09-29
    // 아웃게임 시작 정보(OutGameStartContext)는 SO 참조 대신 ID만 담으므로, 카탈로그에서 찾아 선택합니다.
    public bool TrySelectById(AltarId id)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[OutGame/AltarManager] 초기화 후 제단을 선택할 수 있습니다.", this);
            return false;
        }
        if (id == AltarId.None)
        {
            return false;
        }
        if (!_catalog.TryGetById(id, out AltarData altar))
        {
            Debug.LogWarning($"[OutGame/AltarManager] Catalog에 없는 제단 ID입니다. ID: {id}", this);
            return false;
        }
        return TrySelect(altar);
    }

    // Current date KDH 2026-10-02
    // 저장 시점에만 객체를 만듭니다. Update에서는 호출하지 않습니다.
    // 파일에는 AltarId만 담습니다. 에셋 참조는 다음 실행에서 복원되지 않습니다.
    public AltarRunSaveData CaptureSaveData()
    {
        return new AltarRunSaveData
        {
            SelectedAltar = _selected != null ? _selected.Id : AltarId.None
        };
    }

    // Current date KDH 2026-10-02
    // ID로 카탈로그의 AltarData를 다시 고릅니다. 인게임은 Selected로 그 에셋을 읽습니다.
    // 세이브 담당은 GameFlowController.Continue()보다 먼저 호출해야 합니다.
    public void RestoreSaveData(AltarRunSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[OutGame/AltarManager] 복원할 제단 저장 데이터가 없습니다.", this);
            return;
        }

        if (data.SelectedAltar == AltarId.None)
        {
            TryClearSelection();
            return;
        }

        TrySelectById(data.SelectedAltar);
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

    // 선택분을 EffectManager에 등록하고 OnRunStart 지급을 한 번 실행합니다.
    public bool TryApplySelected()
    {
        if (!TryRegisterSelectedEffects(out AltarInstance instance)) return false;
        // 새 게임의 시작 지급입니다. 이어하기는 TryApplyOngoingEffects만 호출합니다.
        ApplyTriggered(AltarTriggerMoment.OnRunStart);
        Applied?.Invoke(instance);
        Debug.Log($"[OutGame/AltarManager] 제단을 적용했습니다. ID: {_selected.Id}", this);
        return true;
    }

    // Current date KDH 2026-10-08
    // 이어하기는 시작 재화가 세이브 잔액에 이미 있으므로 효과만 등록합니다.
    // 등록은 로드 때 한 번이고, Update에서 매 프레임 다시 계산하지 않습니다.
    public bool TryApplyOngoingEffects()
    {
        if (!TryRegisterSelectedEffects(out AltarInstance instance)) return false;
        Applied?.Invoke(instance);
        Debug.Log($"[OutGame/AltarManager] 제단 효과를 복원했습니다. ID: {_selected.Id}", this);
        return true;
    }

    // 효과 변환이 성공한 뒤에만 적용 상태로 바꿉니다. 실패하면 기존 선택을 유지합니다.
    private bool TryRegisterSelectedEffects(out AltarInstance instance)
    {
        instance = null;
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

        instance = new AltarInstance(_selected);
        if (instance.Data == null)
        {
            Debug.LogError("[OutGame/AltarManager] 제단 인스턴스에 AltarData가 없습니다.", this);
            return false;
        }

        if (!_effectManager.TryConvertEffects(
            instance,
            new EffectDataGroup
            {
                StatEffects = _selected.UnitStatEffects,
                CurrencyEffects = _selected.CurrencyEffects
            },
            1,
            out ConvertedEffects converted))
        {
            Debug.LogError(
                $"[OutGame/AltarManager] 제단 효과 변환에 실패했습니다. ID: {_selected.Id}. 유닛/재화 효과 설정을 확인해주세요.",
                _selected);
            return false;
        }

        _isApplying = true;
        _instance = instance;
        _effectManager.RegisterEffects(instance, converted);
        // AddUnitModifiers는 보존하되 호출하지 않습니다. 공통 연결에서 유닛 효과를 반영합니다.
        _isApplying = false;
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
        // 유닛 효과 해제도 EffectManager 변경을 받는 공통 연결에서 담당합니다.

        Cleared?.Invoke();
        Debug.Log($"[OutGame/AltarManager] 제단 효과를 해제했습니다. ID: {altarId}", this);
        return true;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        // Current date KDH 2026-09-29
        // 첫 Preparation(스폰 전)에 1회 적용합니다. ResetRun으로 None에서 해제된 뒤에도 여기서 다시 적용됩니다.
        // 웨이브마다 Preparation이 다시 오지만, IsApplied 확인으로 중복 적용을 막습니다.
        if (phase == GamePhase.Preparation)
        {
            if (_selected != null && !IsApplied)
            {
                TryApplySelected();
            }
            return;
        }

        // Reward = 웨이브 승리 후입니다. 패배 경로에는 Reward가 없습니다.
        if (phase == GamePhase.Reward)
        {
            // Current date KDH 2026-10-08
            // 보상 화면에서 이어하면 Reward가 다시 옵니다. 이자는 세이브 잔액에 이미 있으므로 건너뜁니다.
            if (_gameFlow != null && _gameFlow.IsResumingStep) return;
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

    // 기존 직접 적용 함수 보존용(현재 호출하지 않음).
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

        return instance.Data.Id == AltarId.None ? instance.Data.name : instance.Data.Id.ToString();
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

// Current date KDH 2026-10-02
// 인게임 저장에 실을 이번 판 제단입니다. AltarData 참조는 담지 않습니다.
[Serializable]
public class AltarRunSaveData
{
    public AltarId SelectedAltar;
}
