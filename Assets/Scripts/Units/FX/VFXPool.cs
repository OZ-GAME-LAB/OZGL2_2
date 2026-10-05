using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 사전 생성 없이 프리팹별 반환 인스턴스만 보관한다.
    internal sealed class VFXPool
    {
        private readonly Dictionary<GameObject, Stack<VFXInstance>> _stored = new();
        private readonly Transform _root;
        private readonly Transform _inactiveRoot;
        private readonly int _limit;
        internal VFXPool(Transform root, int limit)
        {
            _root = root;
            _limit = Mathf.Max(0, limit);
            var inactive = new GameObject("Stored VFX");
            inactive.transform.SetParent(root, false);
            inactive.SetActive(false);
            _inactiveRoot = inactive.transform;
        }
        internal VFXInstance Rent(GameObject prefab)
        {
            if (_stored.TryGetValue(prefab, out var stack))
                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    if (item == null) continue;
                    item.transform.SetParent(_root, false);
                    return item;
                }
            var go = Object.Instantiate(prefab, _inactiveRoot);
            go.SetActive(false);
            var instance = go.GetComponent<VFXInstance>() ?? go.AddComponent<VFXInstance>();
            instance.Configure(prefab);
            go.transform.SetParent(_root, false);
            return instance;
        }
        internal void Return(VFXInstance instance)
        {
            var prefab = instance.Prefab;
            instance.ResetPlayback();
            if (!_stored.TryGetValue(prefab, out var stack)) _stored.Add(prefab, stack = new());
            if (stack.Count >= _limit) Object.Destroy(instance.gameObject);
            else { instance.transform.SetParent(_inactiveRoot, false); stack.Push(instance); }
        }
        internal void Clear()
        {
            foreach (var stack in _stored.Values)
                foreach (var item in stack) if (item != null) Object.Destroy(item.gameObject);
            _stored.Clear();
        }
    }
}
