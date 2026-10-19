using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public sealed class UnlockManager
    {
        private readonly UnlockData[] _definitions;
        private HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        public event Action Changed;

        public UnlockManager(UnlockData[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                throw new ArgumentException("해금 정의를 연결해주세요.");
            _definitions = (UnlockData[])definitions.Clone();
            var seen = new HashSet<string>();
            foreach (UnlockData definition in _definitions)
            {
                if (definition == null) throw new ArgumentException("해금 정의가 없습니다.");
                definition.Validate();
                if (!seen.Add(definition.Id)) throw new ArgumentException("중복 해금 정의: " + definition.Id);
                if (definition.DefaultUnlocked) _ids.Add(definition.Id);
            }
        }

        public bool IsUnlocked(string id) => id != null && _ids.Contains(id);
        public UnlockSaveData CaptureSaveData()
        {
            var data = new UnlockSaveData { Version = 1, Ids = new List<string>(_ids) };
            data.Ids.Sort(StringComparer.Ordinal);
            return data;
        }

        public UnlockSaveData Evaluate(PlayerRecordSaveData records, UnlockSaveData previous)
        {
            records.Validate();
            previous.Validate();
            var next = previous.Copy();
            foreach (UnlockData definition in _definitions)
                if (definition.IsMet(records) && !next.Ids.Contains(definition.Id)) next.Ids.Add(definition.Id);
            return next;
        }

        public void RestoreSaveData(UnlockSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            _ids = new HashSet<string>(data.Ids, StringComparer.Ordinal);
            foreach (UnlockData definition in _definitions)
                if (definition.DefaultUnlocked) _ids.Add(definition.Id);
        }

        public void NotifyChanged()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
