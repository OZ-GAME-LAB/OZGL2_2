using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

namespace Units.FX
{
    public sealed class VFXManager : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField]
        private VFXCatalogSO _catalog;

        [SerializeField, HideInInspector]
        private BasicAttackFXMappingSO _basicAttackMapping;

        [SerializeField, Tooltip("재생 중인 VFX를 모아둘 오브젝트. 비워두면 자동 생성합니다.")]
        private Transform _activeRoot;

        [SerializeField, Tooltip("보관 중인 VFX를 모아둘 오브젝트. 비활성화하여 사용하며, 비워두면 자동 생성합니다.")]
        private Transform _inactiveRoot;

        [SerializeField, Min(1)]
        private int _maxStored = 16;

        [SerializeField, Min(1),
         Tooltip("이 세대 수 이상 사용하지 않은 보관 인스턴스를 정리합니다. 프리팹별 1개는 남깁니다.")]
        private int _unusedGenerationsBeforeRemoval = 2;

        [SerializeField,
         Tooltip("일치하는 원본 셰이더만 풀 최초 생성 시 교체합니다. 원본 에셋은 변경하지 않습니다.")]
        private List<VFXShaderReplacement> _shaderReplacements = new();

        [SerializeField, Min(1)]
        private int _maxActive = 512;

        public BasicAttackFXMappingSO BasicAttackMapping =>
            _basicAttackMapping;

        // ============================================================
        // Runtime State
        // ============================================================

        private readonly Dictionary<FXHandle, VFXInstance> _active = new();
        private readonly List<FXHandle> _completed = new();
        private readonly HashSet<string> _missing = new();

        private VFXPool _pool;

        public static VFXManager Instance { get; private set; }

        public int ActiveCount =>
            _active.Count;

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Instance = null;
        }

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    "[VFXManager] 활성 매니저가 중복되었습니다.",
                    this);

                enabled = false;
                return;
            }

            Instance = this;

            if (_pool == null)
            {
                InitializePool();
            }
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f)
                return;

            if (_active.Count == 0)
                return;

            _completed.Clear();

            foreach (var pair in _active)
            {
                var instance = pair.Value;

                if (instance == null
                    || !instance.gameObject.activeInHierarchy
                    || instance.Tick(Time.deltaTime))
                {
                    _completed.Add(pair.Key);
                }
            }

            foreach (var handle in _completed)
            {
                Release(handle);
            }
        }

        private void OnDisable()
        {
            Clear();

            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        // Playback
        // ============================================================

        public FXHandle Play(SkillFXRequest request)
        {
            if (!isActiveAndEnabled)
                return default;

            var entry = request.Entry;

            if (entry == null
                || entry.Kind != FXKind.VFX)
            {
                return default;
            }

            if (request.IsCleanup)
            {
                Cleanup(request);
                return default;
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
                return default;

            // Independent FX는 Owner의 수명이 끝났더라도
            // 자체 수명 동안 계속 재생될 수 있다.
            if (!request.Scope.IsProjectile
                && request.Metadata.Owner.ObjectId != 0
                && !request.Metadata.Owner.MatchesLifetime
                && entry.EndPolicy != SkillFXEndPolicy.Independent)
            {
                return default;
            }

            if (_catalog == null)
            {
                Debug.LogError(
                    "[VFXManager] VFXCatalog가 설정되지 않았습니다.",
                    this);

                return default;
            }

            if (!_catalog.TryGet(
                    entry.Key,
                    out var definition))
            {
                // 같은 누락 Key에 대해 로그를 반복하지 않는다.
                if (_missing.Add(entry.Key))
                {
                    Debug.LogWarning(
                        $"[VFXManager] 카탈로그에 VFX Key가 없습니다: {entry.Key}",
                        this);
                }

                return default;
            }

            if (definition == null
                || definition.Prefab == null)
            {
                if (_missing.Add(entry.Key))
                {
                    Debug.LogWarning(
                        $"[VFXManager] VFX Definition 또는 Prefab이 없습니다: {entry.Key}",
                        this);
                }

                return default;
            }

            if (_active.Count >= Mathf.Max(1, _maxActive))
            {
                return default;
            }

            if (_pool == null)
            {
                InitializePool();
            }

            var instance =
                _pool.Rent(definition.Prefab);

            if (instance == null)
            {
                Debug.LogWarning(
                    $"[VFXManager] VFX 인스턴스를 대여하지 못했습니다: {entry.Key}",
                    this);

                return default;
            }

            var handle =
                FXHandle.Create();

            instance.Play(
                definition,
                request);

            _active.Add(
                handle,
                instance);

            return handle;
        }

        // ============================================================
        // Stop / Cleanup
        // ============================================================

        public void Stop(
            FXHandle handle,
            bool immediate = false)
        {
            if (!_active.TryGetValue(
                    handle,
                    out var instance))
            {
                return;
            }

            if (immediate)
            {
                Release(handle);
                return;
            }

            if (instance != null)
            {
                instance.Stop();
            }
        }

        private void Cleanup(SkillFXRequest request)
        {
            var immediate = new List<FXHandle>();
            foreach (var pair in _active)
            {
                var instance = pair.Value;
                if (instance == null)
                    continue;

                var playing =
                    instance.Request;

                if (!playing.Scope.Equals(request.Scope))
                    continue;

                if (!ReferenceEquals(
                        playing.Entry,
                        request.Entry))
                {
                    continue;
                }

                var endPolicy =
                    playing.Entry.EndPolicy;

                if (playing.Entry.EndPolicy == SkillFXEndPolicy.ClearImmediately)
                {
                    immediate.Add(pair.Key);
                    continue;
                }

                // Independent는 Scope가 종료되더라도
                // VFX 자체는 종료하지 않고 대상 추적만 해제한다.
                if (endPolicy == SkillFXEndPolicy.Independent)
                {
                    if (request.CleanupReason
                        == FXCleanupReason.ScopeEnded)
                    {
                        instance.Detach();
                    }

                    continue;
                }

                if (endPolicy == SkillFXEndPolicy.StopEmission
                    || request.CleanupReason
                    == FXCleanupReason.ScopeEnded)
                {
                    instance.Stop();
                }
            }
            foreach (var handle in immediate) Release(handle);
        }

        // ============================================================
        // Owner Release
        // ============================================================

        public void ReleaseOwner(
            CombatTargetSnapshot owner)
        {
            _completed.Clear();

            foreach (var pair in _active)
            {
                var instance =
                    pair.Value;

                if (instance == null)
                {
                    _completed.Add(pair.Key);
                    continue;
                }

                var request =
                    instance.Request;

                if (request.Scope.IsProjectile)
                    continue;

                if (request.Scope.OwnerId
                    != owner.ObjectId)
                {
                    continue;
                }

                if (request.Scope.LifetimeVersion
                    != owner.LifetimeVersion)
                {
                    continue;
                }

                if (request.Entry.EndPolicy
                    == SkillFXEndPolicy.Independent)
                {
                    instance.Detach();
                }
                else
                {
                    _completed.Add(pair.Key);
                }
            }

            foreach (var handle in _completed)
            {
                Release(handle);
            }
        }

        // ============================================================
        // Pool
        // ============================================================

        private void InitializePool()
        {
            // 매니저나 활성 루트까지 비활성화하는 계층은 보관 루트로 사용하지 않는다.
            if (_activeRoot == null)
                _activeRoot = CreatePoolRoot("Active VFX");

            if (_inactiveRoot == null || transform.IsChildOf(_inactiveRoot)
                || _activeRoot.IsChildOf(_inactiveRoot))
            {
                _inactiveRoot = CreatePoolRoot("Stored VFX");
            }

            _inactiveRoot.gameObject.SetActive(false);
            _pool = new VFXPool(_activeRoot, _inactiveRoot, _maxStored,
                _unusedGenerationsBeforeRemoval, _shaderReplacements);
        }

        private Transform CreatePoolRoot(string rootName)
        {
            var root = new GameObject(rootName).transform;
            root.SetParent(transform, false);
            return root;
        }

        private void Release(FXHandle handle)
        {
            if (!_active.TryGetValue(
                    handle,
                    out var instance))
            {
                return;
            }

            _active.Remove(handle);

            if (instance != null)
            {
                _pool.Return(instance);
            }
        }

        // 전투 시작 1회를 한 세대로 취급한다.
        // 재생 중인 인스턴스는 정리하지 않는다.
        public void BeginGeneration()
        {
            _pool?.BeginGeneration();
        }

        public void Clear(
            bool clearPool = true)
        {
            if (_pool != null)
            {
                foreach (var instance in _active.Values)
                {
                    if (instance != null)
                    {
                        _pool.Return(instance);
                    }
                }
            }

            _active.Clear();
            _completed.Clear();

            if (clearPool)
            {
                _pool?.Clear();
            }
        }
    }
}