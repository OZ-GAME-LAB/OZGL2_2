using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 사전 생성 없이 프리팹별 반환 인스턴스를 보관하고 전투 세대별로 미사용분을 정리한다.
    internal sealed class VFXPool
    {
        // ============================================================
        // References / Runtime State
        // ============================================================

        private readonly Dictionary<GameObject, List<VFXInstance>> _stored = new();
        private readonly Transform _root;
        private readonly Transform _inactiveRoot;
        private readonly int _limit;
        private readonly int _unusedGenerationsBeforeRemoval;
        private readonly IReadOnlyList<VFXShaderReplacement> _shaderReplacements;
        private long _generation;

        internal VFXPool(Transform root, int limit, int unusedGenerationsBeforeRemoval = 2,
            IReadOnlyList<VFXShaderReplacement> shaderReplacements = null)
        {
            _root = root;
            // 기존 씬의 0 설정도 최소 보관 1개 정책으로 처리한다.
            _limit = Mathf.Max(1, limit);
            _unusedGenerationsBeforeRemoval = Mathf.Max(1, unusedGenerationsBeforeRemoval);
            _shaderReplacements = shaderReplacements;
            var inactive = new GameObject("Stored VFX");
            inactive.transform.SetParent(root, false);
            inactive.SetActive(false);
            _inactiveRoot = inactive.transform;
        }

        // ============================================================
        // Rent / Return
        // ============================================================

        internal VFXInstance Rent(GameObject prefab)
        {
            if (_stored.TryGetValue(prefab, out var stored))
            {
                while (stored.Count > 0)
                {
                    int index = stored.Count - 1;
                    var item = stored[index];
                    stored.RemoveAt(index);
                    if (item == null) continue;
                    item.LastUsedGeneration = _generation;
                    item.transform.SetParent(_root, false);
                    return item;
                }
            }

            var go = Object.Instantiate(prefab, _inactiveRoot);
            go.SetActive(false);
            var instance = go.GetComponent<VFXInstance>() ?? go.AddComponent<VFXInstance>();
            instance.Configure(prefab);
            // 최초 생성에서만 교체한다. 풀 재대여 시 머티리얼을 다시 만들지 않는다.
            instance.ApplyShaderReplacements(_shaderReplacements);
            instance.LastUsedGeneration = _generation;
            go.transform.SetParent(_root, false);
            return instance;
        }

        internal void Return(VFXInstance instance)
        {
            var prefab = instance.Prefab;
            // 여러 세대에 걸쳐 재생된 연출은 반환된 세대까지 사용한 것으로 기록한다.
            instance.LastUsedGeneration = _generation;
            instance.ResetPlayback();
            if (!_stored.TryGetValue(prefab, out var stored))
                _stored.Add(prefab, stored = new());

            if (stored.Count >= _limit)
                DestroyInstance(instance);
            else
            {
                instance.transform.SetParent(_inactiveRoot, false);
                stored.Add(instance);
                RemoveUnused(stored);
            }
        }

        // ============================================================
        // Generation / Cleanup
        // ============================================================

        internal void BeginGeneration()
        {
            _generation++;
            foreach (var stored in _stored.Values)
                RemoveUnused(stored);
        }

        private void RemoveUnused(List<VFXInstance> stored)
        {
            stored.RemoveAll(item => item == null);
            VFXInstance retained = null;
            foreach (var item in stored)
                if (retained == null || item.LastUsedGeneration >= retained.LastUsedGeneration)
                    retained = item;

            // 최근 사용한 1개는 남긴다. 대여 중인 인스턴스는 이 목록에 없으므로 정리되지 않는다.
            for (int i = stored.Count - 1; i >= 0; i--)
            {
                var item = stored[i];
                if (item == retained || _generation - item.LastUsedGeneration < _unusedGenerationsBeforeRemoval)
                    continue;

                stored.RemoveAt(i);
                DestroyInstance(item);
            }
        }

        internal void Clear()
        {
            // 명시적 전체 정리와 매니저 비활성화에서는 최소 보관분도 해제한다.
            foreach (var stored in _stored.Values)
                foreach (var item in stored)
                    if (item != null) DestroyInstance(item);
            _stored.Clear();
            _generation = 0;
        }

        private static void DestroyInstance(VFXInstance instance)
        {
            // 한 번도 활성화되지 않은 오브젝트는 OnDestroy가 생략될 수 있어 소유 자원을 먼저 해제한다.
            instance.DisposeMaterials();
            Object.Destroy(instance.gameObject);
        }
    }
}
