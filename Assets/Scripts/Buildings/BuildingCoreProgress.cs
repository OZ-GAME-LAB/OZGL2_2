// Current date KDH 2026-09-17
// 지금 필드에 있는 코어만 기억합니다. 해금 여부를 SO에 쓰지 않습니다.
using System;
using UnityEngine;

namespace OZGL.KDH
{
    public class BuildingCoreProgress : MonoBehaviour
    {
        private Building _core;

        public event Action Changed;

        public Building CurrentCore => _core;

        public int CurrentLevel
        {
            get
            {
                if (_core == null || _core.Data == null)
                    return 0;

                return _core.Data.CoreLevel;
            }
        }

        public void Register(Building building)
        {
            if (building == null || building.Data == null || !building.Data.IsCore)
            {
                Debug.LogWarning("[BuildingCoreProgress] 코어가 아닌 건물은 등록하지 않습니다.", this);
                return;
            }

            if (_core != null && _core != building)
                Debug.LogWarning("[BuildingCoreProgress] 코어가 둘입니다. 새 코어로 바꿉니다.", this);

            _core = building;
            Changed?.Invoke();
        }

        public void Unregister(Building building)
        {
            if (_core != building)
                return;

            _core = null;
            Changed?.Invoke();
        }
    }
}
