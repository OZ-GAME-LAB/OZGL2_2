// Current date KDH 2026-09-14
// 씬에 올라간 건물 실물. SO는 읽기만 하고, 점유/소환 위치/집결지는 이 인스턴스가 가집니다.
using UnityEngine;

namespace OZGL.KDH
{
    public class Building : MonoBehaviour
    {
        [SerializeField] private BuildingData data;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("비우면 Front Offset으로 병영 앞을 계산합니다.")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("spawnPoint가 없을 때 로컬 기준 앞 방향. 2D에서 적이 오는 쪽.")]
        [SerializeField] private Vector2 frontOffset = new Vector2(0f, -1f);
        [Tooltip("개발자가 맵/프리팹에 둔 기본 집결지. 플레이어 지정이 있으면 이쪽은 쓰지 않습니다.")]
        [SerializeField] private Transform defaultRally;

        private IBuildingModule[] _modules;
        private bool _initialized;
        private bool _hasPlayerRally;
        private Vector2 _playerRally;

        public BuildingData Data => data;
        public Transform SpawnPoint => spawnPoint;

        public Vector2 SpawnWorldPosition
        {
            get
            {
                if (spawnPoint != null)
                    return spawnPoint.position;

                return transform.TransformPoint(frontOffset);
            }
        }

        public Vector2 RallyWorldPosition
        {
            get
            {
                if (_hasPlayerRally)
                    return _playerRally;

                if (defaultRally != null)
                    return defaultRally.position;

                return SpawnWorldPosition;
            }
        }

        public bool HasPlayerRally => _hasPlayerRally;

        private void Awake()
        {
            CacheRefs();
        }

        private void Start()
        {
            if (!_initialized && data != null)
                Initialize(data);
        }

        private void OnDestroy()
        {
            UnregisterCore();
            TeardownModules();
        }

        public void Initialize(BuildingData buildingData)
        {
            if (buildingData == null)
            {
                Debug.LogWarning("[Building] Initialize에 BuildingData가 null입니다.", this);
                return;
            }

            UnregisterCore();
            data = buildingData;
            CacheRefs();
            ApplyWorldSprite();
            EnsureFeatureModules();
            SetupModules();
            _initialized = true;
            RegisterCore();
        }

        // Current date KDH 2026-09-14
        // 플레이어가 찍은 집결지. 이동은 유닛이 하고, 여기엔 좌표만 둡니다.
        public void SetPlayerRally(Vector2 worldPosition)
        {
            _playerRally = worldPosition;
            _hasPlayerRally = true;
        }

        public void ClearPlayerRally()
        {
            _hasPlayerRally = false;
        }

        private void CacheRefs()
        {
            // GetComponent는 여기서 한 번만. Update에서 호출하면 매 프레임 비용이 납니다.
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void ApplyWorldSprite()
        {
            if (spriteRenderer == null || data == null || data.WorldSprite == null)
                return;

            spriteRenderer.sprite = data.WorldSprite;
        }

        private void EnsureFeatureModules()
        {
            if (data == null)
                return;

            if (data.HasProduction && GetComponent<BuildingProducer>() == null)
                gameObject.AddComponent<BuildingProducer>();

            if (data.HasSpawn && GetComponent<BuildingSpawner>() == null)
                gameObject.AddComponent<BuildingSpawner>();

            _modules = GetComponents<IBuildingModule>();
        }

        private void SetupModules()
        {
            if (_modules == null)
                return;

            for (int i = 0; i < _modules.Length; i++)
            {
                if (_modules[i] != null)
                    _modules[i].Setup(this);
            }
        }

        private void TeardownModules()
        {
            if (_modules == null)
                return;

            for (int i = 0; i < _modules.Length; i++)
            {
                if (_modules[i] != null)
                    _modules[i].Teardown();
            }
        }

        // Current date KDH 2026-09-17
        // 코어만 해금 진행에 등록합니다. Find는 생성/파괴 때 한 번만 합니다.
        private void RegisterCore()
        {
            if (data == null || !data.IsCore)
                return;

            BuildingCoreProgress progress = FindFirstObjectByType<BuildingCoreProgress>();
            if (progress == null)
            {
                Debug.LogWarning("[Building] BuildingCoreProgress가 없어 코어 해금을 등록하지 못했습니다.", this);
                return;
            }

            progress.Register(this);
        }

        private void UnregisterCore()
        {
            if (data == null || !data.IsCore)
                return;

            BuildingCoreProgress progress = FindFirstObjectByType<BuildingCoreProgress>();
            if (progress == null)
                return;

            progress.Unregister(this);
        }
    }
}
