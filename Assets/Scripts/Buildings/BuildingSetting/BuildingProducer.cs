// Current date KDH 2026-09-11
// 웨이브가 끝날 때 지갑에 자원을 넣습니다. Update에서 초를 재지 않습니다.
using UnityEngine;

namespace OZGL.KDH
{
    public class BuildingProducer : MonoBehaviour, IBuildingModule
    {
        private Building _owner;
        private PlayerWallet _wallet;
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

            _wallet = FindFirstObjectByType<PlayerWallet>();
            if (_wallet == null)
            {
                Debug.LogWarning("[BuildingProducer] PlayerWallet을 찾지 못해 생산할 수 없습니다.", this);
                return;
            }

            WaveEvents.WaveCleared += OnWaveCleared;
            _setup = true;
        }

        public void Teardown()
        {
            if (_setup)
                WaveEvents.WaveCleared -= OnWaveCleared;

            _setup = false;
            _owner = null;
            _wallet = null;
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void OnWaveCleared(int waveIndex)
        {
            if (_owner == null || _owner.Data == null || !_owner.Data.HasProduction)
                return;

            BuildingProductionSettings settings = _owner.Data.Production;
            if (settings.trigger != ProductionTrigger.PerWave)
                return;

            if (_wallet == null)
            {
                Debug.LogWarning("[BuildingProducer] PlayerWallet이 없어 생산을 건너뜁니다.", this);
                return;
            }

            _wallet.Add(settings.resourceType, settings.amount);
        }
    }
}
