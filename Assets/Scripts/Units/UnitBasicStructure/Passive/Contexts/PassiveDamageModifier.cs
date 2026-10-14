using Units.Skills;



namespace Units
{
    public readonly struct PassiveDamageModifier
    {
        // ============================================================
        // Runtime
        // ============================================================

        private readonly RuntimePassiveSkill _runtimePassive;

        private readonly PassiveDamageModifierActionData _action;


        // ============================================================
        // Properties
        // ============================================================

        public RuntimePassiveSkill RuntimePassive =>
            _runtimePassive;

        public PassiveDamageModifierActionData Action =>
            _action;


        // ============================================================
        // Constructor
        // ============================================================

        public PassiveDamageModifier(
            RuntimePassiveSkill runtimePassive,
            PassiveDamageModifierActionData action)
        {
            _runtimePassive =
                runtimePassive;

            _action =
                action;
        }
    }
}