using TMPro;
using Units;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>클릭 선택을 위한 독립 샘플. 전투 AI가 아닌 버튼으로 실제 Life 메서드를 호출한다.</summary>
    public sealed class MvpRuntimeUnitSelectionSample : MonoBehaviour
    {
        public bool IsReady { get; private set; }

        [SerializeField] private UnitInfoPanel _panel;
        [SerializeField] private RuntimeUnitInfoBinding _binding;
        [SerializeField] private Unit_Core[] _units;
        [SerializeField] private RuntimeUnitInfoSource[] _sources;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _damageButton;
        [SerializeField] private Button _healButton;
        [SerializeField] private Button _shieldButton;
        [SerializeField] private Button _respawnButton;
        [SerializeField] private RuntimeUnitCountHud _countHud;

        private void OnEnable()
        {
            _damageButton.onClick.AddListener(HandleDamage);
            _healButton.onClick.AddListener(HandleHeal);
            _shieldButton.onClick.AddListener(HandleShield);
            _respawnButton.onClick.AddListener(HandleRespawn);
        }

        private void Start()
        {
            _panel.HideUnitInfo();
            _binding.Initialize(_panel);
            foreach (var unit in _units) unit.Initialize();
            RegisterCounts();
            IsReady = true;
            _statusText.text = "유닛을 클릭해서 선택하세요. 수치와 전투 입력은 테스트용입니다.";
        }

        private void OnDisable()
        {
            _damageButton.onClick.RemoveListener(HandleDamage);
            _healButton.onClick.RemoveListener(HandleHeal);
            _shieldButton.onClick.RemoveListener(HandleShield);
            _respawnButton.onClick.RemoveListener(HandleRespawn);
        }

        private void HandleDamage()
        {
            if (!TryGetSelectedUnit(out var unit)) return;
            unit.TakeDamage(25);
            _statusText.text = "선택 유닛에 테스트 피해 25 적용 (보호막 우선).";
        }

        private void HandleHeal()
        {
            if (!TryGetSelectedUnit(out var unit)) return;
            unit.Heal(20);
            _statusText.text = "선택 유닛에 테스트 회복 20 요청 (사망 시 회복 불가).";
        }

        private void HandleShield()
        {
            if (!TryGetSelectedUnit(out var unit)) return;
            unit.AddShield(20);
            _statusText.text = "선택 유닛에 테스트 보호막 20 요청.";
        }

        private void HandleRespawn()
        {
            foreach (var unit in _units)
            {
                unit.gameObject.SetActive(false);
                unit.gameObject.SetActive(true);
                unit.Initialize();
            }
            RegisterCounts();
            _statusText.text = "두 유닛 재사용 완료. 이전 선택은 해제되며 다시 클릭할 수 있습니다.";
        }

        private void RegisterCounts()
        {
            // 선택 전용 씬에는 집계 HUD가 없다. 스포너 연결 순서만 샘플에서 보여준다.
            if (_countHud == null) return;
            foreach (var source in _sources) _countHud.TryRegister(source);
        }

        private bool TryGetSelectedUnit(out Unit_Core unit)
        {
            unit = null;
            for (int i = 0; i < _sources.Length; i++)
                if (_sources[i].isActiveAndEnabled && _sources[i].SelectionId == _binding.SelectionId)
                {
                    unit = _units[i];
                    return true;
                }
            return false;
        }
    }
}
