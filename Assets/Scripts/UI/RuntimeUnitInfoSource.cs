using System;
using Units;
using UnityEngine;

namespace Game.UI
{
    /// <summary>실제 유닛의 공개 값/이벤트를 UI 스냅샷으로 변환한다. 유닛을 초기화하거나 변경하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeUnitInfoSource : MonoBehaviour
    {
        public event Action<RuntimeUnitInfoSource> InfoChanged;
        public event Action<RuntimeUnitInfoSource> Unavailable;

        public string SelectionId { get; private set; }

        [SerializeField] private string _roleLabel = "병종 미지정";
        [TextArea] [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;

        private Unit_Core _core;
        private Unit_Life _life;
        private Unit_RuntimeStatus _status;

        private void OnEnable()
        {
            // 풀에서 같은 오브젝트가 다시 나와도 이전 선택과 구분한다.
            SelectionId = Guid.NewGuid().ToString("N");
            _core = GetComponent<Unit_Core>();
            _life = GetComponent<Unit_Life>();
            _status = GetComponent<Unit_RuntimeStatus>();
            if (_life != null)
            {
                _life.HpChanged += HandleLifeChanged;
                _life.ShieldChanged += HandleLifeChanged;
                _life.Died += HandleDied;
            }
            if (_status != null)
            {
                _status.StatChanged += HandleStatChanged;
                _status.MaxHpChanged += HandleLifeChanged;
            }
        }

        private void OnDisable()
        {
            if (_life != null)
            {
                _life.HpChanged -= HandleLifeChanged;
                _life.ShieldChanged -= HandleLifeChanged;
                _life.Died -= HandleDied;
            }
            if (_status != null)
            {
                _status.StatChanged -= HandleStatChanged;
                _status.MaxHpChanged -= HandleLifeChanged;
            }
            SelectionId = null;
            Unavailable?.Invoke(this);
        }

        /// <summary>초기화된 활성 유닛만 조회한다. 실패해도 상태를 변경하지 않는다.</summary>
        public bool TryGetInfo(out UnitInfoData data)
        {
            data = null;
            if (!isActiveAndEnabled || string.IsNullOrEmpty(SelectionId) || _core == null ||
                _life == null || _status == null || _core.RuntimeStatus != _status ||
                _status.UnitData == null || !UnitInfoData.IsValidHealth(_life.CurrentHp, _life.MaxHp)) return false;

            var unitData = _status.UnitData;
            string displayName = string.IsNullOrWhiteSpace(unitData.UnitName) ? gameObject.name : unitData.UnitName;
            string faction = _core.Team == UnitTeam.Ally ? "아군" : _core.Team == UnitTeam.Enemy ? "적군" : "진영 미지정";
            string role = string.IsNullOrWhiteSpace(_roleLabel) ? "병종 미지정" : _roleLabel;
            string combat = $"공격력 {_status.AttackPower:0.##} · 방어력 {_status.Defense:0.##}\n" +
                $"공격 속도 {_status.AttackSpeed:0.##} · 이동 속도 {_status.MoveSpeed:0.##}\n" +
                $"보호막 {_life.CurrentShield:0.##}";
            data = new UnitInfoData(SelectionId, displayName, faction, role, _life.CurrentHp, _life.MaxHp,
                _description, combat, icon: _icon);
            return true;
        }

        private void HandleLifeChanged(float previous, float current)
        {
            // HpChanged의 Initialize/피해 알림 인자가 다르므로 항상 공개 프로퍼티를 다시 읽는다.
            InfoChanged?.Invoke(this);
        }

        private void HandleStatChanged(UnitStatType stat, float previous, float current)
        {
            // MaxHp 별도 이벤트와 제한 처리 후의 HpChanged 알림을 모두 받는다.
            if (stat != UnitStatType.MaxHp) InfoChanged?.Invoke(this);
        }

        private void HandleDied()
        {
            // 사망과 디스폰은 다르다. 체력 0은 표시하고 오브젝트 비활성화 때 선택을 해제한다.
            InfoChanged?.Invoke(this);
        }
    }
}
