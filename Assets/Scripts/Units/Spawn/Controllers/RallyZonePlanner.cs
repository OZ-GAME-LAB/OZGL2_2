using System;
using System.Collections.Generic;

namespace Units
{
    internal static class RallyZonePlanner
    {
        internal static int[] Plan(IReadOnlyList<int> sectors, int columns, int rows,
            IReadOnlyList<RallyZone> zones, IReadOnlyList<bool> bosses, bool frontOnRight = false)
        {
            if (sectors == null || zones == null || bosses == null ||
                sectors.Count < zones.Count || bosses.Count != zones.Count || columns < 1 || rows < 1)
                throw new ArgumentException("Insufficient or invalid rally layout.");
            var available = new List<int>(sectors);
            var result = new int[zones.Count];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            float middle = (columns - 1) * 0.5f;
            // 보스부터 자리를 예약하되 결과는 원래 스폰 요청 순서로 반환한다.
            // 홀수 격자의 중앙 열은 공유하고, 전후열이 가득 차면 남은 구역을 사용한다.
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < zones.Count; i++)
            {
                bool bossPass = pass == 0;
                if (result[i] >= 0 || bosses[i] != bossPass) continue;
                int best = -1;
                bool right = zones[i] == RallyZone.Front ? frontOnRight : !frontOnRight;
                for (int overflow = 0; overflow < 2 && best < 0; overflow++)
                foreach (int candidate in available)
                {
                    int x = candidate % columns;
                    bool preferred = right ? x >= middle : x <= middle;
                    if (overflow == 0 && !preferred) continue;
                    if (best < 0 || Compare(candidate, best, columns, rows, right) < 0)
                        best = candidate;
                }
                if (best >= 0) { result[i] = best; available.Remove(best); }
            }
            return result;
        }

        private static int Compare(int a, int b, int columns, int rows, bool right)
        {
            // 격자 중앙에 가까운 칸부터 채우며 동률은 Y 중앙과 팀별 전후 방향으로 결정한다.
            float centerX = (columns - 1) * 0.5f;
            float center = (rows - 1) * 0.5f;
            float ax = a % columns - centerX, ay = a / columns - center;
            float bx = b % columns - centerX, by = b / columns - center;
            int distance = (ax * ax + ay * ay).CompareTo(bx * bx + by * by);
            if (distance != 0) return distance;
            int lateral = Math.Abs(a / columns - center).CompareTo(Math.Abs(b / columns - center));
            if (lateral != 0) return lateral;
            int depth = (a % columns).CompareTo(b % columns);
            return depth != 0 ? (right ? -depth : depth) : b.CompareTo(a);
        }
    }
}
