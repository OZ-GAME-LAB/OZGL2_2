using System.Collections.Generic;



namespace Units
{
    public class MemberController
    {
        // =========================
        // References
        // =========================

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
        // Member Management
        // =========================

        public bool Add(Unit_Gateway unit)
        {
            if (unit == null || _members.Contains(unit))
                return false;

            _members.Add(unit);

            _assignmentContexts.Add(
                unit,
                new UnitAssignmentContext());

            return true;
        }

        public bool Remove(Unit_Gateway unit)
        {
            if (unit == null || !_members.Remove(unit))
                return false;

            _assignmentContexts.Remove(unit);

            return true;
        }

        public bool Contains(Unit_Gateway unit)
        {
            return unit != null && _members.Contains(unit);
        }

        public void Clear()
        {
            _members.Clear();
            _assignmentContexts.Clear();
        }
    }
}