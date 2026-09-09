using UnityEngine;



namespace Units
{
    public class Unit_Animation : MonoBehaviour
    {
        // ============================================================
        // Animator Hash
        // ============================================================

        private static readonly int MoveHash
            = Animator.StringToHash("Move");

        private static readonly int AttackHash
            = Animator.StringToHash("Attack");

        private static readonly int SkillHash
            = Animator.StringToHash("Skill");

        private static readonly int HitHash
            = Animator.StringToHash("Hit");

        private static readonly int DeathHash
            = Animator.StringToHash("Death");


        // ============================================================
        // Components
        // ============================================================

        [SerializeField]
        private Animator _animator;

        private Unit_Core _core;


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(
            Unit_Core core)
        {
            _core = core;

            if (_animator == null)
            {
                Debug.LogError(
                    $"[Unit_Animation] {name} : Animator가 없습니다."
                );
            }
        }


        // ============================================================
        // Animation
        // ============================================================


        public void PlayAnimation_Move()
        {
            if (_animator == null)
                return;

            _animator.SetTrigger(
                MoveHash
            );

            Debug.Log(
                    $"[Unit_Animation] {name} : Move Animation 재생"
                );
        }


        public void PlayAnimation_Attack()
        {
            if (_animator == null)
                return;

            _animator.SetTrigger(
                AttackHash
            );

            Debug.Log(
                   $"[Unit_Animation] {name} : Attack Animation 재생"
               );
        }


        public void PlayAnimation_Skill()
        {
            if (_animator == null)
                return;

            _animator.SetTrigger(
                SkillHash
            );

            Debug.Log(
                   $"[Unit_Animation] {name} : Skill Animation 재생"
               );
        }


        public void PlayAnimation_Hit()
        {
            if (_animator == null)
                return;

            _animator.SetTrigger(
                HitHash
            );

            Debug.Log(
                   $"[Unit_Animation] {name} : Hit Animation 재생"
               );
        }


        public void PlayAnimation_Death()
        {
            if (_animator == null)
                return;

            _animator.SetTrigger(
                DeathHash
            );

            Debug.Log(
                   $"[Unit_Animation] {name} : Death Animation 재생"
               );
        }
    }
}