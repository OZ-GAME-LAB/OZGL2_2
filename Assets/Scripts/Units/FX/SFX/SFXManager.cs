using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

namespace Units.FX
{
    public sealed class SFXManager : MonoBehaviour
    {
        // ============================================================
        // References / Runtime State
        // ============================================================
        [SerializeField] private SFXCatalogSO _catalog;
        [SerializeField, Min(0)] private int _maxStored = 32;
        [SerializeField, Min(1)] private int _maxActive = 64;
        private readonly Dictionary<FXHandle, SFXInstance> _active = new();
        private readonly List<FXHandle> _completed = new();
        private readonly HashSet<string> _missing = new();
        private readonly Dictionary<string, float> _lastPlayed = new();
        private SFXPool _pool;
        public static SFXManager Instance { get; private set; }
        public int ActiveCount => _active.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Instance = null;
        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            { Debug.LogError("[SFXManager] 활성 매니저가 중복되었습니다.", this); enabled = false; return; }
            Instance = this;
            if (_pool == null) _pool = new SFXPool(transform, _maxStored);
        }

        // ============================================================
        // Playback / Cleanup
        // ============================================================
        public FXHandle Play(SkillFXRequest request)
        {
            if (!isActiveAndEnabled || request.Entry == null || request.Entry.Kind != FXKind.SFX) return default;
            if (request.IsCleanup) { Cleanup(request); return default; }
            if (string.IsNullOrWhiteSpace(request.Entry.Key)) return default;
            // 지연 콜백이 재사용된 유닛에 부착 연출을 새로 생성하지 못하게 한다.
            if (!request.Scope.IsProjectile && request.Metadata.Owner.ObjectId != 0
                && !request.Metadata.Owner.MatchesLifetime && request.Entry.EndPolicy != SkillFXEndPolicy.Independent)
                return default;
            if (_catalog == null || !_catalog.TryGet(request.Entry.Key, out var definition))
            {
                if (_missing.Add(request.Entry.Key)) Debug.LogWarning($"[SFXManager] 카탈로그에 Key가 없습니다: {request.Entry.Key}", this);
                return default;
            }
            if (_active.Count >= Mathf.Max(1, _maxActive)) return default;
            int count = 0;
            foreach (var voice in _active.Values) if (voice != null && voice.Key == definition.Key) count++;
            if (count >= definition.MaxConcurrent || (_lastPlayed.TryGetValue(definition.Key, out var time)
                && Time.time - time < definition.MinInterval)) return default;
            _lastPlayed[definition.Key] = Time.time;
            var instance = _pool.Rent();
            var handle = FXHandle.Create();
            instance.Play(definition, request);
            _active.Add(handle, instance);
            return handle;
        }

        public void Stop(FXHandle handle, bool immediate = false)
        {
            if (!_active.TryGetValue(handle, out var instance)) return;
            if (immediate) Release(handle);
            else if (instance != null) instance.Stop();
        }

        private void Cleanup(SkillFXRequest request)
        {
            var immediate = new List<FXHandle>();
            foreach (var pair in _active)
            {
                var instance = pair.Value;
                if (instance == null) continue;
                var playing = instance.Request;
                if (!playing.Scope.Equals(request.Scope) || !ReferenceEquals(playing.Entry, request.Entry)) continue;
                if (playing.Entry.EndPolicy == SkillFXEndPolicy.ClearImmediately)
                { immediate.Add(pair.Key); continue; }
                if (playing.Entry.EndPolicy == SkillFXEndPolicy.Independent)
                { if (request.CleanupReason == FXCleanupReason.ScopeEnded) instance.Detach(); continue; }
                if (playing.Entry.EndPolicy == SkillFXEndPolicy.StopEmission || request.CleanupReason == FXCleanupReason.ScopeEnded)
                    instance.Stop();
            }
            foreach (var handle in immediate) Release(handle);
        }

        public void ReleaseOwner(CombatTargetSnapshot owner)
        {
            _completed.Clear();
            foreach (var pair in _active)
            {
                if (pair.Value == null) { _completed.Add(pair.Key); continue; }
                var request = pair.Value.Request;
                if (request.Scope.IsProjectile || request.Scope.OwnerId != owner.ObjectId
                    || request.Scope.LifetimeVersion != owner.LifetimeVersion) continue;
                if (request.Entry.EndPolicy == SkillFXEndPolicy.Independent) pair.Value.Detach();
                else _completed.Add(pair.Key);
            }
            foreach (var handle in _completed) Release(handle);
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f || AudioListener.pause) return;
            _completed.Clear();
            foreach (var pair in _active)
                if (pair.Value == null || !pair.Value.gameObject.activeInHierarchy || pair.Value.Tick(Time.deltaTime))
                    _completed.Add(pair.Key);
            foreach (var handle in _completed) Release(handle);
        }

        private void Release(FXHandle handle)
        {
            if (!_active.TryGetValue(handle, out var instance)) return;
            _active.Remove(handle);
            if (instance != null) _pool.Return(instance);
        }

        public void Clear(bool clearPool = true)
        {
            foreach (var instance in _active.Values) if (instance != null) _pool.Return(instance);
            _active.Clear();
            _lastPlayed.Clear();
            if (clearPool) _pool?.Clear();
        }
        private void OnDisable()
        {
            Clear();
            if (Instance == this) Instance = null;
        }
    }
}
