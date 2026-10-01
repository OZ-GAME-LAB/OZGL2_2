using Units.Skills;

namespace Units
{
    public class RuntimePassiveSkill
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly PassiveSkillData _data;


        // ============================================================
        // Runtime State
        // ============================================================

        private bool _isActive;

        private bool _hasExecutedOnce;

        private bool _isExecuting;

        public bool IsExecuting => _isExecuting;

        public int PendingApplications { get; private set; }

        public bool IsStopped { get; private set; }

        public CombatEventMetadata ActivationMetadata { get; set; }

        public void BeginPending()
        {
            PendingApplications++;
        }

        public void EndPending()
        {
            if (PendingApplications > 0)
                PendingApplications--;
        }

        public void StopExecution()
        {
            IsStopped = true;
        }

        public PassiveConditionWatch Watch { get; set; }

        public bool MaintenancePending { get; set; }

        public bool TryBeginExecution()
        {
            if (_isExecuting || IsStopped || (Data.EffectMode == PassiveSkillEffectMode.Once && PendingApplications > 0))
                return false;

            _isExecuting = true;

            return true;
        }

        public void EndExecution()
        {
            _isExecuting = false;
        }

        private float _tickElapsedTime;


        // ============================================================
        // Properties
        // ============================================================

        public PassiveSkillData Data => _data;

        public bool IsActive => _isActive;

        public bool HasExecutedOnce => _hasExecutedOnce;


        // ============================================================
        // Constructor
        // ============================================================

        public RuntimePassiveSkill(PassiveSkillData data)
        {
            _data = data;

            _isActive = false;

            _hasExecutedOnce = false;

            _tickElapsedTime = 0f;
        }


        // ============================================================
        // Active State
        // ============================================================

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }


        // ============================================================
        // Once State
        // ============================================================

        public void MarkExecutedOnce()
        {
            _hasExecutedOnce = true;
        }


        // ============================================================
        // Tick
        // ============================================================

        public void AddTickTime(float deltaTime)
        {
            _tickElapsedTime += deltaTime;
        }

        public bool IsTickReady()
        {
            return _tickElapsedTime >= _data.TickInterval;
        }

        public void ResetTickTime()
        {
            _tickElapsedTime = 0f;
        }
    }
}