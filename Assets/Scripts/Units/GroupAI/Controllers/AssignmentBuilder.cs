using System.Collections.Generic;



namespace Units
{
    public class AssignmentBuilder
    {
        // =========================
        // References
        // =========================

        private readonly Dictionary<Unit_Gateway, UnitAssignmentContext>
            _assignmentContexts;


        // =========================
        // Constructor
        // =========================

        public AssignmentBuilder(
            Dictionary<Unit_Gateway, UnitAssignmentContext> assignmentContexts)
        {
            _assignmentContexts = assignmentContexts;
        }


        // =========================
        // Assignment Build
        // =========================

        public bool TryBuild(
            Unit_Gateway unit)
        {
            if (unit == null)
                return false;

            if (!_assignmentContexts.TryGetValue(
                unit,
                out UnitAssignmentContext context))
            {
                return false;
            }

            if (!context.HasTarget)
            {
                context.ClearAssignment();

                return false;
            }

            if (!context.HasPosition)
            {
                context.ClearAssignment();

                return false;
            }


            UnitAssignment assignment =
                new UnitAssignment(
                    context.AssignedTarget,
                    context.AssignedPosition.Value,
                    context.PositionTolerance
                );


            context.SetAssignment(
                assignment
            );

            return true;
        }
    }
}