using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 클립과 무관하게 AudioSource를 재사용한다.
    internal sealed class SFXPool
    {
        private readonly Stack<SFXInstance> _stored = new();
        private readonly Transform _root;
        private readonly Transform _inactiveRoot;
        private readonly int _limit;
        internal SFXPool(Transform root, int limit)
        {
            _root = CreateFolder("SFX Voice", CreateFolder("Active SFX", root));
            var storedRoot = CreateFolder("Stored SFX", root);
            storedRoot.gameObject.SetActive(false);
            _inactiveRoot = CreateFolder("SFX Voice", storedRoot);
            _limit = Mathf.Max(0, limit);
        }

        private static Transform CreateFolder(string name, Transform parent)
        {
            var folder = new GameObject(name).transform;
            folder.SetParent(parent, false);
            return folder;
        }

        internal SFXInstance Rent()
        {
            while (_stored.Count > 0)
            {
                var item = _stored.Pop();
                if (item == null) continue;
                item.transform.SetParent(_root, false);
                return item;
            }
            var instance = CreateInstance();
            instance.transform.SetParent(_root, false);
            return instance;
        }
        internal void PrewarmOne(int targetCount, int activeCount)
        {
            if (_stored.Count + activeCount >= Mathf.Min(targetCount, _limit)) return;
            _stored.Push(CreateInstance());
        }
        private SFXInstance CreateInstance()
        {
            var go = new GameObject("SFX Voice");
            go.SetActive(false);
            go.transform.SetParent(_inactiveRoot, false);
            return go.AddComponent<SFXInstance>();
        }
        internal void Return(SFXInstance instance)
        {
            instance.ResetPlayback();
            if (_stored.Count >= _limit) Object.Destroy(instance.gameObject);
            else
            {
                instance.transform.SetParent(_inactiveRoot, false);
                _stored.Push(instance);
            }
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
