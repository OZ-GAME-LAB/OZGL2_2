using System;
using System.Collections.Generic;

namespace Units
{
    public class EnemyStatModifierContainer
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly List<EnemyStatModifier> _allModifiers =
            new();

        private readonly Dictionary<
            EnemyUnitClass,
            List<EnemyStatModifier>> _classModifiers =
            new();

        private readonly Dictionary<
            EnemyUnitType,
            List<EnemyStatModifier>> _typeModifiers =
            new();


        // ============================================================
        // Public Methods
        // ============================================================

        private readonly Dictionary<EnemyUnitFaction, List<EnemyStatModifier>> _factionModifiers = new();

        public void AddToFaction(EnemyUnitFaction unitFaction, EnemyStatModifier modifier)
        {
            if (!_factionModifiers.TryGetValue(unitFaction, out var modifiers))
            {
                modifiers = new List<EnemyStatModifier>();
                _factionModifiers.Add(unitFaction, modifiers);
            }
            modifiers.Add(modifier);
        }

        public IReadOnlyList<EnemyStatModifier> GetFromFactionModifiers(EnemyUnitFaction unitFaction)
        {
            if (_factionModifiers.TryGetValue(unitFaction, out var modifiers))
                return modifiers;
            return System.Array.Empty<EnemyStatModifier>();
        }

        public void AddToAll(
            EnemyStatModifier modifier)
        {
            _allModifiers.Add(modifier);
        }


        public void AddToClass(
            EnemyUnitClass unitClass,
            EnemyStatModifier modifier)
        {
            if (!_classModifiers.TryGetValue(
                    unitClass,
                    out List<EnemyStatModifier> modifiers))
            {
                modifiers =
                    new List<EnemyStatModifier>();

                _classModifiers.Add(
                    unitClass,
                    modifiers
                );
            }

            modifiers.Add(modifier);
        }


        public void AddToType(
            EnemyUnitType unitType,
            EnemyStatModifier modifier)
        {
            if (!_typeModifiers.TryGetValue(
                    unitType,
                    out List<EnemyStatModifier> modifiers))
            {
                modifiers =
                    new List<EnemyStatModifier>();

                _typeModifiers.Add(
                    unitType,
                    modifiers
                );
            }

            modifiers.Add(modifier);
        }


        public IReadOnlyList<EnemyStatModifier> GetFromAllModifiers()
        {
            return _allModifiers;
        }


        public IReadOnlyList<EnemyStatModifier> GetFromClassModifiers(
            EnemyUnitClass unitClass)
        {
            if (_classModifiers.TryGetValue(
                    unitClass,
                    out List<EnemyStatModifier> modifiers))
            {
                return modifiers;
            }

            return Array.Empty<EnemyStatModifier>();
        }


        public IReadOnlyList<EnemyStatModifier> GetFromTypeModifiers(
            EnemyUnitType unitType)
        {
            if (_typeModifiers.TryGetValue(
                    unitType,
                    out List<EnemyStatModifier> modifiers))
            {
                return modifiers;
            }

            return Array.Empty<EnemyStatModifier>();
        }


        public void RemoveBySource(
            object source)
        {
            foreach (var modifiers in _factionModifiers.Values)
                modifiers.RemoveAll(modifier => Equals(modifier.Source, source));

            _allModifiers.RemoveAll(
                modifier =>
                    Equals(
                        modifier.Source,
                        source
                    )
            );

            foreach (List<EnemyStatModifier> modifiers
                     in _classModifiers.Values)
            {
                modifiers.RemoveAll(
                    modifier =>
                        Equals(
                            modifier.Source,
                            source
                        )
                );
            }

            foreach (List<EnemyStatModifier> modifiers
                     in _typeModifiers.Values)
            {
                modifiers.RemoveAll(
                    modifier =>
                        Equals(
                            modifier.Source,
                            source
                        )
                );
            }
        }
    }
}