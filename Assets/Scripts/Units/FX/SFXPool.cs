using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 클립과 무관하게 AudioSource를 재사용한다.
    internal sealed class SFXPool
    {
        private readonly Stack<SFXInstance> _stored = new();
        private readonly Transform _root;
        private readonly int _limit;
        internal SFXPool(Transform root, int limit) { _root = root; _limit = Mathf.Max(0, limit); }
        internal SFXInstance Rent()
        {
            while (_stored.Count > 0)
            {
                var item = _stored.Pop();
                if (item != null) return item;
            }
            var go = new GameObject("SFX Voice");
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            return go.AddComponent<SFXInstance>();
        }
        internal void Return(SFXInstance instance)
        {
            instance.ResetPlayback();
            if (_stored.Count >= _limit) Object.Destroy(instance.gameObject);
            else _stored.Push(instance);
        }
        internal void Clear()
        {
            while (_stored.Count > 0)
            {
                var item = _stored.Pop();
                if (item != null) Object.Destroy(item.gameObject);
            }
        }
    }
}
