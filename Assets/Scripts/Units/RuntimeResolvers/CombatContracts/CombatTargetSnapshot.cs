using UnityEngine;

// 객체 신원·수명·위치를 저장해 사망하거나 재사용된 대상과 현재 대상을 구분한다.
namespace Units
{
    // 객체 참조와 수명을 함께 저장한다. 사망 기록은 객체의 다음 수명과 분리한다.
    public readonly struct CombatTargetSnapshot
    {

        // ============================================================
        // Properties
        // ============================================================

        public ICombatTarget Target { get; }

        public int ObjectId { get; }

        public int LifetimeVersion { get; }

        public UnitTeam Team { get; }

        public Vector2 Position { get; }

        public float Hp { get; }

        public float Shield { get; }

        public float MaxHp { get; }

        public float AttackPower { get; }

        public float Defense { get; }

        public bool MatchesLifetime => CombatTargetUtility.Exists(Target) && Target.LifetimeVersion == LifetimeVersion;

        public bool IsTargetable => MatchesLifetime && Target.IsTargetable;

        // ============================================================
        // Constructor
        // ============================================================

        public CombatTargetSnapshot(ICombatTarget target)
        {
            Target = target;

            bool exists = CombatTargetUtility.Exists(target);

            ObjectId = exists && target.Transform != null ? target.Transform.GetInstanceID() : 0;

            LifetimeVersion = exists ? target.LifetimeVersion : 0;

            Team = exists ? target.Team : default;

            Position = exists && target.Transform != null ? (Vector2)target.Transform.position : default;

            Hp = exists ? target.CurrentHp : 0f;

            Shield = exists ? target.CurrentShield : 0f;

            var stats = exists ? target.RuntimeStatus : null;

            MaxHp = stats != null ? stats.MaxHp : 0f;

            AttackPower = stats != null ? stats.AttackPower : 0f;

            Defense = stats != null ? stats.Defense : 0f;
        }
    }
}
