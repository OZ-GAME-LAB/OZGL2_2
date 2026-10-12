using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Units.FX
{
    // 전투 요청과 무관한 씬 전용 프리팹 미리보기. 매 회 새 인스턴스로 처음부터 재생한다.
    public sealed class VFXLoopPreview : MonoBehaviour
    {
        [Header("Preview Prefabs")]
        [SerializeField, Tooltip("0: Vefects Sword Slash, 1: Anime Slash. 비어 있는 슬롯은 건너뛰고 가로로 나란히 동시에 반복합니다.")]
        private GameObject[] _prefabs = new GameObject[2];
        [SerializeField, Min(0.01f)] private float _playSeconds = 1f;
        [SerializeField, Min(0.01f)] private float _restSeconds = 1f;
        [SerializeField] private Vector3 _scale = Vector3.one;
        [SerializeField] private Vector3 _rotationOffset;
        [SerializeField] private VFXShaderReplacement[] _shaderReplacements;

        [SerializeField, Min(0f), Tooltip("프리팹 사이의 가로 간격. 오브젝트 위치를 중심으로 배치합니다.")]
        private float _spacing = 4f;

        private readonly List<GameObject> _instances = new();
        private GameObject _inactiveRoot;
        private readonly List<AudioSource> _audioSources = new();
        private Coroutine _routine;
        private VFXMaterialCache _materials;

        private void OnEnable()
        {
            _inactiveRoot = new GameObject("VFX Preview Inactive");
            _inactiveRoot.transform.SetParent(transform, false);
            _inactiveRoot.SetActive(false);
            _materials = new VFXMaterialCache(_shaderReplacements);
            _routine = StartCoroutine(Repeat());
        }

        private IEnumerator Repeat()
        {
            while (true)
            {
                int count = 0;
                if (_prefabs != null)
                    foreach (var prefab in _prefabs)
                        if (prefab != null) count++;
                if (count == 0)
                {
                    yield return null;
                    continue;
                }

                int index = 0;
                foreach (var prefab in _prefabs)
                {
                    if (prefab == null) continue;
                    float x = (index++ - (count - 1) * 0.5f) * Mathf.Max(0f, _spacing);
                    CreatePreview(prefab, x);
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, _playSeconds));
                ClearInstances();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, _restSeconds));
            }
        }

        private void CreatePreview(GameObject prefab, float x)
        {
            var instance = Instantiate(prefab, _inactiveRoot.transform);
            _instances.Add(instance);
            instance.SetActive(false);
            _audioSources.AddRange(instance.GetComponentsInChildren<AudioSource>(true));
            SilenceAudio(); // 비활성 부모 아래에서 생성해 Play On Awake보다 먼저 차단한다.
            _materials.Apply(instance);
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = new Vector3(x, 0f, 0f);
            instance.transform.localRotation = prefab.transform.localRotation * Quaternion.Euler(_rotationOffset);
            instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, _scale);
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.useUnscaledTime = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            instance.SetActive(true);
            SilenceAudio();
            foreach (var particle in particles)
                if (particle != null && particle.gameObject.activeInHierarchy) particle.Play(false);

        }

        private void LateUpdate() => SilenceAudio();

        private void SilenceAudio()
        {
            if (_audioSources == null) return;
            foreach (var source in _audioSources)
            {
                if (source == null) continue;
                source.playOnAwake = false;
                source.mute = true;
                source.Stop();
                source.enabled = false;
            }
        }

        private void ClearInstances()
        {
            foreach (var instance in _instances)
            {
                if (instance == null) continue;
                instance.SetActive(false);
                Destroy(instance);
            }
            _instances.Clear();
            _audioSources.Clear();
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            ClearInstances();
            if (_inactiveRoot != null) Destroy(_inactiveRoot);
            _inactiveRoot = null;
            _materials?.Clear();
            _materials = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
