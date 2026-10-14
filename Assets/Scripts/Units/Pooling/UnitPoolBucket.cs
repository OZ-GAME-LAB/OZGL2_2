using System.Collections.Generic;
using UnityEngine;

namespace Units
{
    // 프리팹 하나의 회수 인스턴스와 Hierarchy 보관 위치를 관리한다.
    internal sealed class UnitPoolBucket
    {
        internal readonly GameObject Prefab;
        internal readonly UnitTeam Team;
        internal readonly Transform Root;
        internal readonly List<UnitPoolMember> Available = new();

        internal UnitPoolBucket(GameObject prefab, UnitTeam team, Transform parent)
        {
            Prefab = prefab;
            Team = team;
            var root = new GameObject(prefab.name);
            root.transform.SetParent(parent, false);
            // 신규 생성 시에도 Spawner가 배치하기 전에는 OnEnable이 실행되지 않는다.
            root.SetActive(false);
            Root = root.transform;
        }
    }
}
