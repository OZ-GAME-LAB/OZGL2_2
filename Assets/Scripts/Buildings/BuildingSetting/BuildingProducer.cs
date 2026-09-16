// Current date KDH 2026-09-14
// 전투 후 다시 준비 페이즈가 될 때 생산 보상을 넣습니다. 첫 BeginRun의 Preparation은 건너뜁니다.
using Game.Core;
using UnityEngine;

namespace OZGL.KDH
{
    public class BuildingProducer : MonoBehaviour, IBuildingModule
    {
        private Building _owner;
        private RunCurrencyManager _wallet;
        private GameFlowController _gameFlow;
        private GamePhase _previousPhase = GamePhase.None;
        private bool _setup;

        public void Setup(Building owner)
        {
            Teardown();

            _owner = owner;
            if (_owner == null || _owner.Data == null)
            {
                Debug.LogWarning("[BuildingProducer] Setup에 BuildingData가 없습니다.", this);
                return;
            }

            if (!_owner.Data.HasProduction)
                return;

            _wallet = FindFirstObjectByType<RunCurrencyManager>();
            if (_wallet == null)
            {
                Debug.LogWarning("[BuildingProducer] RunCurrencyManager를 찾지 못해 생산할 수 없습니다.", this);
                return;
            }

            _gameFlow = FindFirstObjectByType<GameFlowController>();
            if (_gameFlow == null)
            {
                Debug.LogWarning("[BuildingProducer] GameFlowController를 찾지 못해 생산 시점을 알 수 없습니다.", this);
                return;
            }

            _previousPhase = _gameFlow.CurPhase;
            _gameFlow.PhaseChanged += OnPhaseChanged;
            _setup = true;
        }

        public void Teardown()
        {
            if (_setup && _gameFlow != null)
                _gameFlow.PhaseChanged -= OnPhaseChanged;

            _setup = false;
            _owner = null;
            _wallet = null;
            _gameFlow = null;
            _previousPhase = GamePhase.None;
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            bool returnedToPrep = phase == GamePhase.Preparation
                && _previousPhase != GamePhase.None
                && _previousPhase != GamePhase.Preparation;

            _previousPhase = phase;
            if (!returnedToPrep)
                return;

            ApplyProduction();
        }

        private void ApplyProduction()
        {
            if (_owner == null || _owner.Data == null || !_owner.Data.HasProduction)
                return;

            BuildingProductionSettings settings = _owner.Data.Production;
            if (settings.trigger != ProductionTrigger.PerWave)
                return;

            if (_wallet == null)
            {
                Debug.LogWarning("[BuildingProducer] RunCurrencyManager가 없어 생산을 건너뜁니다.", this);
                return;
            }

            if (!_wallet.IsInitialized)
            {
                Debug.LogWarning("[BuildingProducer] RunCurrencyManager가 아직 초기화되지 않았습니다.", this);
                return;
            }

            CurrencyType currency = ToCurrency(settings.resourceType);
            if (currency == CurrencyType.None)
            {
                Debug.LogWarning($"[BuildingProducer] 생산할 수 없는 재화 종류입니다: {settings.resourceType}", this);
                return;
            }

            if (!_wallet.TryApplyProductionReward(currency, settings.amount))
                Debug.LogWarning("[BuildingProducer] 생산 보상 지급에 실패했습니다.", this);
        }

        private static CurrencyType ToCurrency(BuildingResourceType type)
        {
            switch (type)
            {
                case BuildingResourceType.Gold:
                    return CurrencyType.Gold;
                case BuildingResourceType.Gem:
                    return CurrencyType.Gem;
                default:
                    return CurrencyType.None;
            }
        }
    }
}
