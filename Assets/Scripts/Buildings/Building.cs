// Current date KDH 2026-09-11
// 씬에 올라간 건물 실물. SO는 읽기만 하고, 점유/상태는 이 인스턴스가 가집니다.
// 생산/소환은 모듈이 웨이브 이벤트를 구독하므로 Update에서 폴링하지 않습니다.
using UnityEngine;

namespace OZGL.KDH
{
    public class Building : MonoBehaviour
    {
        [SerializeField] private BuildingData data;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform spawnPoint;

        private IBuildingModule[] _modules;
        private bool _initialized;

        public BuildingData Data => data;
        public Transform SpawnPoint => spawnPoint;

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
            TeardownModules();
        }

        public void Initialize(BuildingData buildingData)
        {
            if (buildingData == null)
            {
                Debug.LogWarning("[Building] Initialize에 BuildingData가 null입니다.", this);
                return;
            }

            data = buildingData;
            CacheRefs();
            ApplyWorldSprite();
            EnsureFeatureModules();
            SetupModules();
            _initialized = true;
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
    }
}
