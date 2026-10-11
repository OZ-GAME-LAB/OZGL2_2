using System;
using System.Collections.Generic;

namespace Units
{
    internal static class RallyZonePlanner
    {
        internal static int[] Plan(IReadOnlyList<int> sectors, int columns, int rows,
            IReadOnlyList<RallyZone> zones, IReadOnlyList<bool> bosses)
        {
            if (sectors.Count < zones.Count || bosses.Count != zones.Count || columns < 1)
                throw new ArgumentException("Insufficient or invalid rally layout.");
            var available = new List<int>(sectors);
            var result = new int[zones.Count];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            int minX = int.MaxValue, maxX = int.MinValue;
            foreach (int sector in sectors)
            {
                minX = Math.Min(minX, sector % columns);
                maxX = Math.Max(maxX, sector % columns);
            }
            float middle = (minX + maxX) * 0.5f;
            // Bosses first, stable request order; then normal groups. A single
            // column is shared by both zones. No permanent boss-only sectors.
            for (int pass = 0; pass < 4; pass++)
            for (int i = 0; i < zones.Count; i++)
            {
                bool bossPass = pass == 0 || pass == 2;
                if (result[i] >= 0 || bosses[i] != bossPass) continue;
                bool overflow = pass >= 2;
                int best = -1;
                foreach (int candidate in available)
                {
                    int x = candidate % columns;
                    bool preferred = minX == maxX || (zones[i] == RallyZone.Front
                        ? x <= middle : x > middle);
                    if (!overflow && !preferred) continue;
                    if (best < 0 || Compare(candidate, best, columns, rows, zones[i]) < 0)
                        best = candidate;
                }
                if (best >= 0) { result[i] = best; available.Remove(best); }
            }
            return result;
        }

        private static int Compare(int a, int b, int columns, int rows, RallyZone zone)
        {
            // Forward edge for Front, rear edge for Rear; center-out on Y.
            int depth = (a % columns).CompareTo(b % columns);
            if (depth != 0) return zone == RallyZone.Front ? depth : -depth;
            float center = (rows - 1) * 0.5f;
            int lateral = Math.Abs(a / columns - center).CompareTo(Math.Abs(b / columns - center));
            return lateral != 0 ? lateral : b.CompareTo(a);
        }
    }
}
