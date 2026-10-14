using UnityEngine;

// 선택한 XY 위치에서 한 번 재생하는 표시 전용 효과입니다.
public sealed class ConsumableUseVFX : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField, Min(0.05f)] private float _duration = 0.6f;
    [SerializeField, Min(0.01f)] private float _previewRadius = 3f;
    [Tooltip("생성 위치 기준의 표시 오프셋입니다. 머리 위 연출은 Y를 올려 설정합니다.")]
    [SerializeField] private Vector2 _visualOffset;
    [SerializeField, Min(0f)] private float _riseDistance;
    private MaterialPropertyBlock _properties;
    private float _elapsed;
    private Transform _followTarget;

    private void OnEnable()
    {
        _elapsed = 0f;
        ApplyRadius(_previewRadius);
        SetProgress(0f);
    }

    public void Play(Vector2 position, float radius)
    {
        _followTarget = null;
        transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.identity);
        _elapsed = 0f;
        ApplyRadius(radius);
        SetProgress(0f);
    }

    public void PlayTarget(Vector2 position, Transform followTarget)
    {
        Play(position, _previewRadius);
        _followTarget = followTarget;
    }

    private void LateUpdate()
    {
        if (_followTarget == null) return;
        // 비활성화된 풀링 유닛을 따라가지 않고 마지막 위치에서 연출을 마칩니다.
        if (!_followTarget.gameObject.activeInHierarchy)
        {
            _followTarget = null;
            return;
        }
        Vector3 position = _followTarget.position;
        transform.position = new Vector3(position.x, position.y, 0f);
    }

    private void ApplyRadius(float radius)
    {
        // 부모 없이 생성하므로 Quad의 지름을 월드 반경에 직접 맞춥니다.
        transform.localScale = Vector3.one;
        if (_renderer != null)
            _renderer.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, _duration));
        SetProgress(progress);
        if (progress >= 1f) Destroy(gameObject);
    }

    private void SetProgress(float progress)
    {
        if (_renderer == null) return;
        _renderer.transform.localPosition = new Vector3(
            _visualOffset.x, _visualOffset.y + _riseDistance * progress, 0f);
        if (_properties == null) _properties = new MaterialPropertyBlock();
        _properties.SetFloat("_Progress", progress);
        _renderer.SetPropertyBlock(_properties);
    }
}
