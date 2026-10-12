using System.Collections.Generic;

namespace Units
{
    public class AllyStatModifierContainer
    {
        // ============================================================
        // Data
        // ============================================================

        private readonly List<AllyStatModifier> _allModifiers =
            new();

        private readonly Dictionary<
            AllyUnitClass,
            List<AllyStatModifier>> _classModifiers =
            new();

        private readonly Dictionary<
            AllyUnitType,
            List<AllyStatModifier>> _typeModifiers =
            new();


        // ============================================================
        // Public Methods
        // ============================================================

        private readonly Dictionary<AllyUnitTier, List<AllyStatModifier>> _tierModifiers = new();

        public void AddToTier(AllyUnitTier unitTier, AllyStatModifier modifier)
        {
            if (!_tierModifiers.TryGetValue(unitTier, out var modifiers))
            {
                modifiers = new List<AllyStatModifier>();
                _tierModifiers.Add(unitTier, modifiers);
            }
            modifiers.Add(modifier);
        }

        public IReadOnlyList<AllyStatModifier> GetFromTierModifiers(AllyUnitTier unitTier)
        {
            if (_tierModifiers.TryGetValue(unitTier, out var modifiers))
                return modifiers;
            return System.Array.Empty<AllyStatModifier>();
        }

        public void AddToAll(
            AllyStatModifier modifier)
        {
            _allModifiers.Add(modifier);
        }


        public void AddToClass(
            AllyUnitClass unitClass,
            AllyStatModifier modifier)
        {
            if (!_classModifiers.TryGetValue(
                    unitClass,
                    out List<AllyStatModifier> modifiers))
            {
                modifiers =
                    new List<AllyStatModifier>();

                _classModifiers.Add(
                    unitClass,
                    modifiers
                );
            }

            modifiers.Add(modifier);
        }


        public void AddToType(
            AllyUnitType unitType,
            AllyStatModifier modifier)
        {
            if (!_typeModifiers.TryGetValue(
                    unitType,
                    out List<AllyStatModifier> modifiers))
            {
                modifiers =
                    new List<AllyStatModifier>();

                _typeModifiers.Add(
                    unitType,
                    modifiers
                );
            }

            modifiers.Add(modifier);
        }


        public IReadOnlyList<AllyStatModifier> GetFromAllModifiers()
        {
            return _allModifiers;
        }


        public IReadOnlyList<AllyStatModifier> GetFromClassModifiers(
            AllyUnitClass unitClass)
        {
            if (_classModifiers.TryGetValue(
                    unitClass,
                    out List<AllyStatModifier> modifiers))
            {
                return modifiers;
            }

            return System.Array.Empty<AllyStatModifier>();
        }


        public IReadOnlyList<AllyStatModifier> GetFromTypeModifiers(
            AllyUnitType unitType)
        {
            if (_typeModifiers.TryGetValue(
                    unitType,
                    out List<AllyStatModifier> modifiers))
            {
                return modifiers;
            }

            return System.Array.Empty<AllyStatModifier>();
        }


        // 아티펙트 출처만 제거하여 다른 시스템의 스탯 보정을 유지한다.
        public void RemoveArtifactModifiers()
        {
            _allModifiers.RemoveAll(modifier => modifier.Source is ArtifactInstance);

            foreach (List<AllyStatModifier> modifiers in _classModifiers.Values)
            {
                modifiers.RemoveAll(modifier => modifier.Source is ArtifactInstance);
            }

            foreach (List<AllyStatModifier> modifiers in _typeModifiers.Values)
            {
                modifiers.RemoveAll(modifier => modifier.Source is ArtifactInstance);
            }

            foreach (List<AllyStatModifier> modifiers in _tierModifiers.Values)
            {
                modifiers.RemoveAll(modifier => modifier.Source is ArtifactInstance);
            }

        }


        public void RemoveBySource(
            object source)
        {
            foreach (var modifiers in _tierModifiers.Values)
                modifiers.RemoveAll(modifier => Equals(modifier.Source, source));

            _allModifiers.RemoveAll(
                modifier =>
                    Equals(
                        modifier.Source,
                        source
                    )
            );

            foreach (List<AllyStatModifier> modifiers
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

            foreach (List<AllyStatModifier> modifiers
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