using Units.Skills;
using UnityEngine;

namespace Units.FX
{
    // 초기 패시브보다 먼저 연결한다. 투사체는 정적 전달 경로를 캡처하여 발사자 재사용과 분리한다.
    [DisallowMultipleComponent]
    public sealed class UnitFXBridge : MonoBehaviour
    {
        [SerializeField] private BasicAttackFXMappingSO _basicAttackMapping;
        private Unit_Combat _combat;
        private Unit_Passive _passive;
        private CombatTargetSnapshot _owner;
        public BasicAttackFXMappingSO BasicAttackMapping => _basicAttackMapping;

        public void Initialize(Unit_Core core, Unit_Combat combat, Unit_Passive passive)
        {
            Release();
            _owner = new CombatTargetSnapshot(core.CombatTarget);
            _combat = combat;
            _passive = passive;
            if (_combat != null) _combat.FXRequested += Dispatch;
            if (_passive != null) _passive.FXRequested += Dispatch;
        }

        public static void Dispatch(SkillFXRequest request)
        {
            if (request.Entry == null) return;
            if (request.Entry.Kind == FXKind.VFX) VFXManager.Instance?.Play(request);
            else SFXManager.Instance?.Play(request);
        }

        public void Release()
        {
            if (_combat != null) _combat.FXRequested -= Dispatch;
            if (_passive != null) _passive.FXRequested -= Dispatch;
            if (_owner.ObjectId != 0)
            {
                VFXManager.Instance?.ReleaseOwner(_owner);
                SFXManager.Instance?.ReleaseOwner(_owner);
            }
            _combat = null;
            _passive = null;
            _owner = default;
        }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
