using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 프리팹별 사전 생성 / 반환 인스턴스를 보관하고 전투 세대별로 미사용분을 정리한다.
    internal sealed class VFXPool
    {
        // ============================================================
        // References / Runtime State
        // ============================================================

        private readonly Dictionary<GameObject, List<VFXInstance>> _stored = new();
        private readonly Dictionary<GameObject, Transform> _activeFolders = new();
        private readonly Dictionary<GameObject, Transform> _storedFolders = new();
        private readonly Transform _root;
        private readonly Transform _inactiveRoot;
        private readonly int _limit;
        private readonly int _unusedGenerationsBeforeRemoval;
        private readonly VFXMaterialCache _materials;
        private long _generation;

        internal VFXPool(Transform activeRoot, Transform inactiveRoot, int limit,
            int unusedGenerationsBeforeRemoval = 2,
            IReadOnlyList<VFXShaderReplacement> shaderReplacements = null)
        {
            _root = activeRoot;
            _inactiveRoot = inactiveRoot;
            _limit = Mathf.Max(1, limit);
            _unusedGenerationsBeforeRemoval = Mathf.Max(1, unusedGenerationsBeforeRemoval);
            _materials = new VFXMaterialCache(shaderReplacements);
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
                    item.transform.SetParent(GetPrefabFolder(prefab, false), false);
                    return item;
                }
            }

            var instance = CreateInstance(prefab);
            instance.transform.SetParent(GetPrefabFolder(prefab, false), false);
            return instance;
        }

        // 대여 / 반환을 반복하면 같은 인스턴스만 재사용되므로 부족한 수량을 직접 생성한다.
        internal void PrewarmOne(GameObject prefab, int targetCount, int activeCount)
        {
            if (prefab == null)
                return;

            if (!_stored.TryGetValue(prefab, out var stored))
                _stored.Add(prefab, stored = new());

            stored.RemoveAll(item => item == null);
            if (stored.Count + activeCount >= Mathf.Min(targetCount, _limit))
                return;

            stored.Add(CreateInstance(prefab));
        }

        private VFXInstance CreateInstance(GameObject prefab)
        {
            var go = Object.Instantiate(prefab, GetPrefabFolder(prefab, true));
            go.SetActive(false);
            var instance = go.GetComponent<VFXInstance>() ?? go.AddComponent<VFXInstance>();
            instance.Configure(prefab);
            // 최초 생성에서만 교체한다. 풀 재대여 시 머티리얼을 다시 만들지 않는다.
            _materials.Apply(instance.gameObject);
            instance.LastUsedGeneration = _generation;
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
                instance.transform.SetParent(GetPrefabFolder(prefab, true), false);
                stored.Add(instance);
                RemoveUnused(stored);
            }
        }

        // ============================================================
        // Hierarchy
        // ============================================================

        private Transform GetPrefabFolder(GameObject prefab, bool stored)
        {
            var folders = stored ? _storedFolders : _activeFolders;
            if (folders.TryGetValue(prefab, out var folder) && folder != null)
                return folder;

            // 이름이 같더라도 다른 프리팹은 별도의 폴더와 풀을 사용한다.
            folder = new GameObject(prefab.name).transform;
            folder.SetParent(stored ? _inactiveRoot : _root, false);
            folders[prefab] = folder;
            return folder;
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
            _materials.Clear();
        }

        private static void DestroyInstance(VFXInstance instance)
        {
            // 공유 머티리얼은 개별 인스턴스가 아닌 풀 전체 정리 시 해제한다.
            Object.Destroy(instance.gameObject);
        }
    }
}
