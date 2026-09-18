using UnityEngine;

namespace Units
{
    public class ProjectileManager : MonoBehaviour
    {
        // ============================================================
        // Singleton
        // ============================================================

        public static ProjectileManager Instance { get; private set; }


        // ============================================================
        // Settings
        // ============================================================

        [SerializeField]
        private Projectile_Controller _projectilePrefab;


        [SerializeField, Min(0.01f)]
        private float _collisionRadius = 0.1f;


        [SerializeField, Min(0.1f)]
        private float _maxLifetime = 5f;


        // ============================================================
        // Default Projectile Data
        // ============================================================

        private Projectile_Controller _fallbackPrefab;


        private Sprite _fallbackSprite;


        private Texture2D _fallbackTexture;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
        }


        // ============================================================
        // Projectile Spawn
        // ============================================================

        public static ProjectileManager GetOrCreate()
        {
            if (Instance == null)
            {
                // 발사 유닛과 독립된 씬 단위 Manager를 생성한다.
                var manager = new GameObject(nameof(ProjectileManager));

                manager.AddComponent<ProjectileManager>();
            }

            return Instance;
        }


        public bool Fire(
            ProjectileRequest request)
        {
            if (request.Attacker == null
                || request.Attacker.RuntimeStatus == null
                || !CombatTargetUtility.IsValid(request.Target)
                || !(request.ProjectileSpeed > 0f)
                || float.IsInfinity(request.ProjectileSpeed))
            {
                return false;
            }


            bool hasDamageRequest =
                request.DamageRequest.HasValue;

            bool hasSkillEffectRequest =
                request.SkillEffectRequest.HasValue;


            // Projectile은 하나의 Impact Request만 가져야 한다.
            if (hasDamageRequest == hasSkillEffectRequest)
            {
                Debug.LogError(
                    "[ProjectileManager] ProjectileRequest의 Impact Request가 유효하지 않습니다."
                );

                return false;
            }


            var prefab =
                _projectilePrefab != null
                    ? _projectilePrefab
                    : GetFallbackPrefab();

            var projectile = Instantiate(
                prefab,
                request.Origin,
                Quaternion.identity,
                transform
            );

            projectile.Initialize(
                this,
                request,
                _collisionRadius,
                _maxLifetime
            );

            projectile.gameObject.SetActive(true);

            return true;
        }


        // ============================================================
        // Projectile Return
        // ============================================================

        public void Release(
            Projectile_Controller projectile)
        {
            // 추후 Pooling을 적용할 때는 이 반환 경로에서 처리한다.
            if (projectile == null)
                return;

            projectile.gameObject.SetActive(false);

            Destroy(projectile.gameObject);
        }


        // ============================================================
        // Default Projectile
        // ============================================================

        private Projectile_Controller GetFallbackPrefab()
        {
            if (_fallbackPrefab != null)
                return _fallbackPrefab;

            var template = new GameObject("Default Projectile Template");

            template.SetActive(false);

            template.transform.SetParent(
                transform,
                false
            );

            _fallbackPrefab = template.AddComponent<Projectile_Controller>();

            // 별도 Projectile Prefab이 없으면 기본 표시용 투사체를 생성한다.
            _fallbackTexture = new Texture2D(
                16,
                16,
                TextureFormat.RGBA32,
                false
            );

            var pixels = new Color[256];

            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    pixels[y * 16 + x] = new Vector2(
                        x - 7.5f,
                        y - 7.5f
                    ).sqrMagnitude <= 56f ? Color.white : Color.clear;

            _fallbackTexture.SetPixels(pixels);

            _fallbackTexture.Apply();

            _fallbackSprite = Sprite.Create(
                _fallbackTexture,
                new Rect(0, 0, 16, 16),
                new Vector2(0.5f, 0.5f),
                64f
            );

            var renderer = template.AddComponent<SpriteRenderer>();

            renderer.sprite = _fallbackSprite;

            renderer.color = new Color(
                1f,
                0.8f,
                0.25f
            );

            renderer.sortingOrder = 100;

            return _fallbackPrefab;
        }


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_fallbackSprite != null)
                Destroy(_fallbackSprite);

            if (_fallbackTexture != null)
                Destroy(_fallbackTexture);
        }
    }
}
