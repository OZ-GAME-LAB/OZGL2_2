using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public sealed class UnlockManager : MonoBehaviour, ISaveDataProvider<UnlockSaveData>
    {
        [SerializeField] private UnlockData[] _definitions;
        private HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        public event Action Changed;
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            // 같은 씬에서 다시 호출되어도 복원한 해금 상태는 유지합니다.
            if (IsInitialized) return;
            if (_definitions == null || _definitions.Length == 0)
                throw new ArgumentException("해금 정의를 연결해주세요.");
            var seen = new HashSet<string>();
            var initialIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnlockData definition in _definitions)
            {
                if (definition == null) throw new ArgumentException("해금 정의가 없습니다.");
                definition.Validate();
                if (!seen.Add(definition.Id)) throw new ArgumentException("중복 해금 정의: " + definition.Id);
                if (definition.DefaultUnlocked) initialIds.Add(definition.Id);
            }
            _ids = initialIds;
            IsInitialized = true;
        }

        public bool IsUnlocked(string id)
        {
            EnsureInitialized();
            return id != null && _ids.Contains(id);
        }
        public UnlockSaveData CaptureSaveData()
        {
            EnsureInitialized();
            var data = new UnlockSaveData { Version = 1, Ids = new List<string>(_ids) };
            data.Ids.Sort(StringComparer.Ordinal);
            return data;
        }

        public UnlockSaveData Evaluate(PlayerRecordSaveData records, UnlockSaveData previous)
        {
            EnsureInitialized();
            records.Validate();
            previous.Validate();
            var next = previous.Copy();
            foreach (UnlockData definition in _definitions)
                if (definition.IsMet(records) && !next.Ids.Contains(definition.Id)) next.Ids.Add(definition.Id);
            return next;
        }

        public void RestoreSaveData(UnlockSaveData data)
        {
            EnsureInitialized();
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            _ids = new HashSet<string>(data.Ids, StringComparer.Ordinal);
            foreach (UnlockData definition in _definitions)
                if (definition.DefaultUnlocked) _ids.Add(definition.Id);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized) throw new InvalidOperationException("UnlockManager를 먼저 초기화해주세요.");
        }

        public void NotifyChanged()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
