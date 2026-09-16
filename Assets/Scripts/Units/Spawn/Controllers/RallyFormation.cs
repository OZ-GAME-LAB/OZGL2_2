using System.Collections.Generic;
using UnityEngine;

namespace Units
{
    internal class RallyFormation
    {
        // ============================================================
        // Formation Data
        // ============================================================

        private readonly Vector2[] _formation1 =
        {
            new Vector2(0f, 0f)
        };

        private readonly Vector2[] _formation2 =
        {
            new Vector2(-0.5f, 0f),
            new Vector2(0.5f, 0f)
        };

        private readonly Vector2[] _formation3 =
        {
            new Vector2(-1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };

        private readonly Vector2[] _formation4 =
        {
            new Vector2(-0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-0.5f, -0.5f),
            new Vector2(0.5f, -0.5f)
        };

        private readonly Vector2[] _formation5 =
        {
            new Vector2(0f, 1f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f)
        };


        // ============================================================
        // Formation
        // ============================================================

        public IReadOnlyList<Vector2> GetFormation(
            int unitCount)
        {
            return unitCount switch
            {
                1 => _formation1,
                2 => _formation2,
                3 => _formation3,
                4 => _formation4,
                5 => _formation5,

                _ => null
            };
        }
    }
}