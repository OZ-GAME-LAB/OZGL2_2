using System.Collections.Generic;
using UnityEngine;


namespace Units
{
    internal class RuntimeUnitRegistrator
    {
        // =========================
        // Runtime Units
        // =========================

        private readonly List<Unit_Gateway> _allyUnits;

        private readonly List<Unit_Gateway> _enemyUnits;


        // =========================
        // Runtime Groups
        // =========================

        private readonly List<Unit_GroupAI> _allyGroups;

        private readonly List<Unit_GroupAI> _enemyGroups;


        // =========================
        // Constructor
        // =========================

        internal RuntimeUnitRegistrator(
            List<Unit_Gateway> allyUnits,
            List<Unit_Gateway> enemyUnits,
            List<Unit_GroupAI> allyGroups,
            List<Unit_GroupAI> enemyGroups)
        {
            _allyUnits =
                allyUnits;

            _enemyUnits =
                enemyUnits;

            _allyGroups =
                allyGroups;

            _enemyGroups =
                enemyGroups;
        }


        // =========================
        // Unit Registration
        // =========================

        internal bool RegisterUnit(
            Unit_Gateway unit)
        {
            if (!ValidateUnitRegistration(
                unit))
            {
                return false;
            }


            List<Unit_Gateway> units =
                GetUnitList(
                    unit.Team
                );


            units.Add(
                unit
            );


            return true;
        }


        internal bool UnregisterUnit(
            Unit_Gateway unit)
        {
            if (unit == null)
                return false;


            List<Unit_Gateway> units =
                GetUnitList(
                    unit.Team
                );


            if (units == null)
                return false;


            return units.Remove(
                unit
            );
        }


        // =========================
        // Group Registration
        // =========================

        internal bool RegisterGroup(
            Unit_GroupAI group)
        {
            if (!ValidateGroupRegistration(
                group))
            {
                return false;
            }


            List<Unit_GroupAI> groups =
                GetGroupList(
                    group.Team
                );


            groups.Add(
                group
            );


            List<Unit_Gateway> registeredUnits =
                new();


            IReadOnlyList<Unit_Gateway> members =
                group.Members;


            for (int i = 0;
                 i < members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    members[i];


                if (!RegisterUnit(
                    unit))
                {
                    Debug.LogError(
                        $"[RuntimeUnitRegistrator] " +
                        $"Group Member 등록 실패 : " +
                        $"{group.name} / " +
                        $"{unit?.name}"
                    );


                    RollbackGroupRegistration(
                        group,
                        registeredUnits
                    );


                    return false;
                }


                registeredUnits.Add(
                    unit
                );
            }


            return true;
        }


        internal bool UnregisterGroup(
            Unit_GroupAI group)
        {
            if (group == null)
                return false;


            List<Unit_GroupAI> groups =
                GetGroupList(
                    group.Team
                );


            if (groups == null)
                return false;


            return groups.Remove(
                group
            );
        }


        // =========================
        // Validation
        // =========================

        private bool ValidateUnitRegistration(
            Unit_Gateway unit)
        {
            if (unit == null)
            {
                Debug.LogError(
                    "[RuntimeUnitRegistrator] " +
                    "등록할 Unit이 null입니다."
                );

                return false;
            }


            List<Unit_Gateway> units =
                GetUnitList(
                    unit.Team
                );


            if (units == null)
            {
                Debug.LogError(
                    $"[RuntimeUnitRegistrator] " +
                    $"지원하지 않는 Unit Team입니다. : " +
                    $"{unit.Team}"
                );

                return false;
            }


            if (units.Contains(
                unit))
            {
                Debug.LogWarning(
                    $"[RuntimeUnitRegistrator] " +
                    $"이미 등록된 Unit입니다. : " +
                    $"{unit.name}"
                );

                return false;
            }


            return true;
        }


        private bool ValidateGroupRegistration(
            Unit_GroupAI group)
        {
            if (group == null)
            {
                Debug.LogError(
                    "[RuntimeUnitRegistrator] " +
                    "등록할 Group이 null입니다."
                );

                return false;
            }


            List<Unit_GroupAI> groups =
                GetGroupList(
                    group.Team
                );


            if (groups == null)
            {
                Debug.LogError(
                    $"[RuntimeUnitRegistrator] " +
                    $"지원하지 않는 Group Team입니다. : " +
                    $"{group.Team}"
                );

                return false;
            }


            if (groups.Contains(
                group))
            {
                Debug.LogWarning(
                    $"[RuntimeUnitRegistrator] " +
                    $"이미 등록된 Group입니다. : " +
                    $"{group.name}"
                );

                return false;
            }


            IReadOnlyList<Unit_Gateway> members =
                group.Members;


            if (members == null ||
                members.Count == 0)
            {
                Debug.LogError(
                    $"[RuntimeUnitRegistrator] " +
                    $"Group Member가 없습니다. : " +
                    $"{group.name}"
                );

                return false;
            }


            for (int i = 0;
                 i < members.Count;
                 i++)
            {
                Unit_Gateway unit =
                    members[i];


                if (unit == null)
                {
                    Debug.LogError(
                        $"[RuntimeUnitRegistrator] " +
                        $"Group에 null Member가 있습니다. : " +
                        $"{group.name} / Index={i}"
                    );

                    return false;
                }


                if (unit.Team !=
                    group.Team)
                {
                    Debug.LogError(
                        $"[RuntimeUnitRegistrator] " +
                        $"Group과 Unit의 Team이 다릅니다. : " +
                        $"Group={group.Team}, " +
                        $"Unit={unit.Team}, " +
                        $"UnitName={unit.name}"
                    );

                    return false;
                }


                List<Unit_Gateway> units =
                    GetUnitList(
                        unit.Team
                    );


                if (units == null)
                    return false;


                if (units.Contains(
                    unit))
                {
                    Debug.LogError(
                        $"[RuntimeUnitRegistrator] " +
                        $"이미 Runtime에 등록된 Unit입니다. : " +
                        $"{unit.name}"
                    );

                    return false;
                }
            }


            return true;
        }


        // =========================
        // Rollback
        // =========================

        private void RollbackGroupRegistration(
            Unit_GroupAI group,
            IReadOnlyList<Unit_Gateway> registeredUnits)
        {
            for (int i =
                     registeredUnits.Count - 1;
                 i >= 0;
                 i--)
            {
                Unit_Gateway unit =
                    registeredUnits[i];


                if (unit == null)
                    continue;


                UnregisterUnit(
                    unit
                );
            }


            List<Unit_GroupAI> groups =
                GetGroupList(
                    group.Team
                );


            groups?.Remove(
                group
            );


            Debug.LogWarning(
                $"[RuntimeUnitRegistrator] " +
                $"Group 등록 Rollback : " +
                $"{group.name}"
            );
        }


        // =========================
        // Runtime Lists
        // =========================

        private List<Unit_Gateway> GetUnitList(
            UnitTeam team)
        {
            switch (team)
            {
                case UnitTeam.Ally:
                    return _allyUnits;

                case UnitTeam.Enemy:
                    return _enemyUnits;

                default:
                    return null;
            }
        }


        private List<Unit_GroupAI> GetGroupList(
            UnitTeam team)
        {
            switch (team)
            {
                case UnitTeam.Ally:
                    return _allyGroups;

                case UnitTeam.Enemy:
                    return _enemyGroups;

                default:
                    return null;
            }
        }
    }
}