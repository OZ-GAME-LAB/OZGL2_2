using System;
using System.Collections.Generic;



namespace Units
{
    public class MemberController
    {
        // =========================
        // References
        // =========================

        internal event Action<Unit_Gateway, UnitAssignmentContext> AssignmentChanged;

        private readonly List<Unit_Gateway> _members;

        private readonly Dictionary<Unit_Gateway, UnitAssignmentContext>
            _assignmentContexts;


        // =========================
        // Constructor
        // =========================

        public MemberController(
            List<Unit_Gateway> members,
            Dictionary<Unit_Gateway, UnitAssignmentContext> assignmentContexts)
        {
            _members = members;
            _assignmentContexts = assignmentContexts;
        }


        // =========================
        // Assignment Events
        // =========================

        private void HandleAssignmentChanged(UnitAssignmentContext context)
        {
            AssignmentChanged?.Invoke(context.Owner, context);
        }


        // =========================
        // Member Management
        // =========================

        public bool Add(Unit_Gateway unit)
        {
            if (unit == null || _members.Contains(unit))
                return false;

            _members.Add(unit);

            var context = new UnitAssignmentContext(unit);

            context.Changed += HandleAssignmentChanged;

            _assignmentContexts.Add(unit, context);

            return true;
        }

        public bool Remove(Unit_Gateway unit)
        {
            if (unit == null || !_members.Remove(unit))
                return false;

            if (_assignmentContexts.TryGetValue(unit, out UnitAssignmentContext context))
            {
                context.Clear();
                context.Changed -= HandleAssignmentChanged;
            }

            _assignmentContexts.Remove(unit);

            return true;
        }

        public bool Contains(Unit_Gateway unit)
        {
            return unit != null && _members.Contains(unit);
        }

        public void Clear()
        {
            foreach (UnitAssignmentContext context in _assignmentContexts.Values)
            {
                context.Clear();
                context.Changed -= HandleAssignmentChanged;
            }

            _members.Clear();
            _assignmentContexts.Clear();
        }
    }
}