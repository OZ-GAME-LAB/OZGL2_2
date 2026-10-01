using System.Collections.Generic;


namespace Units
{
    public readonly struct DamageRequest
    {

        public CombatEventMetadata Metadata { get; }

        public CombatSourceSnapshot SourceSnapshot { get; }

        // ============================================================
        // Source
        // ============================================================

        public ICombatTarget Attacker { get; }

        public DamageSourceType SourceType { get; }

        public DamageType DamageType { get; }

        public float DamageMultiplier { get; }


        // ============================================================
        // Target
        // ============================================================

        public IReadOnlyList<ICombatTarget> Targets { get; }

        public IReadOnlyList<CombatTargetSnapshot> TargetSnapshots { get; }


        // ============================================================
        // Constructor
        // ============================================================

        public DamageRequest(
            ICombatTarget attacker,
            IReadOnlyList<ICombatTarget> targets,
            DamageSourceType sourceType,
            DamageType damageType,
            float damageMultiplier,
            CombatEventMetadata metadata = default,
            CombatSourceSnapshot sourceSnapshot = null)
        {
            SourceSnapshot = sourceSnapshot;

            Metadata = metadata.EventId != 0 ? metadata : CombatEventMetadata.Create(attacker);

            Attacker = attacker;

            var copies = new List<ICombatTarget>();

            var snapshots = new List<CombatTargetSnapshot>();

            if (targets != null)
                foreach (var target in targets)
                {
                    copies.Add(target);

                    snapshots.Add(new CombatTargetSnapshot(target));
                }

            Targets = copies.AsReadOnly();

            TargetSnapshots = snapshots.AsReadOnly();

            SourceType = sourceType;

            DamageType = damageType;

            DamageMultiplier = damageMultiplier;
        }
    }
}