using System;
using System.Collections.Generic;
using UnityEngine;

namespace Units
{
    // 생성과 보관만 담당하며 유닛 활성화와 Initialize는 Spawner에서 처리한다.
    public sealed class UnitPoolManager : MonoBehaviour
    {
        // ============================================================
        // Settings / Runtime State
        // ============================================================

        [Tooltip("정상 스폰 완료 시 마지막 사용 세대와의 차이가 이 값 이상인 보관 유닛을 제거합니다.")]
        [SerializeField, Min(1)]
        private int _unusedGenerationsBeforeRemoval = 1;

        private readonly Dictionary<GameObject, UnitPoolBucket> _buckets = new();
        private readonly Dictionary<UnitTeam, long> _generations = new();
        private readonly HashSet<UnitTeam> _spawningTeams = new();

        // ============================================================
        // Spawn Generation
        // ============================================================

        public long BeginSpawn(UnitTeam team)
        {
            if (!_spawningTeams.Add(team))
                throw new InvalidOperationException($"[UnitPoolManager] {team} 스폰이 이미 진행 중입니다.");

            _generations.TryGetValue(team, out long generation);
            _generations[team] = ++generation;
            return generation;
        }

        public void EndSpawn(UnitTeam team, long generation, bool succeeded)
        {
            if (!_generations.TryGetValue(team, out long current)
                || current != generation || !_spawningTeams.Remove(team))
                return;

            if (!succeeded)
                return;

            // 정상 스폰 완료 시에만 남아 있는 이전 세대 인스턴스를 정리한다.
            foreach (var bucket in _buckets.Values)
            {
                if (bucket.Team != team)
                    continue;

                for (int i = bucket.Available.Count - 1; i >= 0; i--)
                {
                    var member = bucket.Available[i];
                    if (member != null && current - member.LastSpawnGeneration
                        < Mathf.Max(1, _unusedGenerationsBeforeRemoval))
                        continue;

                    bucket.Available.RemoveAt(i);
                    if (member != null)
                        Destroy(member.gameObject);
                }
            }
        }

        // ============================================================
        // Rent / Return
        // ============================================================

        public GameObject Rent(GameObject prefab, UnitTeam team)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (!_spawningTeams.Contains(team))
                throw new InvalidOperationException("[UnitPoolManager] 스폰 세대를 먼저 시작해야 합니다.");

            if (!_buckets.TryGetValue(prefab, out var bucket))
            {
                bucket = new UnitPoolBucket(prefab, team, transform);
                _buckets.Add(prefab, bucket);
            }
            if (bucket.Team != team)
                throw new InvalidOperationException("[UnitPoolManager] 동일 프리팹의 진영이 일치하지 않습니다.");

            UnitPoolMember member = null;
            while (bucket.Available.Count > 0 && member == null)
            {
                int index = bucket.Available.Count - 1;
                member = bucket.Available[index];
                bucket.Available.RemoveAt(index);
            }

            if (member == null)
            {
                var instance = Instantiate(prefab, bucket.Root);
                member = instance.GetComponent<UnitPoolMember>();
                if (member == null)
                    member = instance.AddComponent<UnitPoolMember>();
                member.Owner = this;
                member.Bucket = bucket;
            }

            member.LastSpawnGeneration = _generations[team];
            member.LeaseVersion++;
            member.IsLeased = true;
            return member.gameObject;
        }

        public bool Return(UnitPoolMember member, long leaseVersion)
        {
            if (member == null || member.Owner != this || !member.IsLeased
                || member.LeaseVersion != leaseVersion || member.gameObject.activeSelf)
                return false;

            member.IsLeased = false;
            member.RuntimeManager = null;
            member.transform.SetParent(member.Bucket.Root, false);
            member.Bucket.Available.Add(member);
            return true;
        }
    }
}
