using UnityEngine;

// XY 전투 평면의 표시 전용 프리팹. 대상 판정과 입력은 사용 컨트롤러가 담당합니다.
[ExecuteAlways]
public sealed class ItemAreaIndicator : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField, Min(0f)] private float _radius = 3f;
    [SerializeField] private Color _fillColor = new Color(168f / 255f, 70f / 255f, 50f / 255f, 0.12f);
    [SerializeField] private Color _borderColor = new Color(217f / 255f, 174f / 255f, 104f / 255f, 0.9f);
    [SerializeField, Min(0.001f)] private float _borderWidth = 0.06f;
    [SerializeField, Min(0.001f)] private float _edgeSoftness = 0.025f;
    [SerializeField, Range(0, 64)] private int _dashCount;
    [SerializeField, Range(0.05f, 1f)] private float _dashRatio = 0.7f;
    [SerializeField] private float _rotationSpeed = 0.03f;
    [SerializeField, Range(0f, 1f)] private float _pulseStrength = 0.12f;
    [SerializeField, Min(0f)] private float _pulseSpeed = 0.8f;
    [SerializeField] private int _sortingOrder = 100;
    private MaterialPropertyBlock _properties;

    public float Radius => _radius;

    private void OnEnable() => Refresh();
    private void OnValidate()
    {
        _radius = Mathf.Max(0f, _radius);
        _borderWidth = Mathf.Max(0.001f, _borderWidth);
        _edgeSoftness = Mathf.Max(0.001f, _edgeSoftness);
    }

    // Inspector에서 변경한 값을 편집 모드에서도 확인할 수 있습니다.
    // OnValidate에서 렌더러를 조작하지 않고 메인 스레드에서 갱신합니다.
    private void Update() => Refresh();

    public void Show(Vector2 worldPosition, float radius)
    {
        SetArea(worldPosition, radius);
        gameObject.SetActive(true);
    }

    public void SetArea(Vector2 worldPosition, float radius)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) ||
            float.IsNaN(worldPosition.x) || float.IsInfinity(worldPosition.x) ||
            float.IsNaN(worldPosition.y) || float.IsInfinity(worldPosition.y)) return;
        _radius = Mathf.Max(0f, radius);
        transform.SetPositionAndRotation(new Vector3(worldPosition.x, worldPosition.y, 0f), Quaternion.identity);
        Refresh();
    }

    public void SetColors(Color fill, Color border)
    {
        _fillColor = fill;
        _borderColor = border;
        Refresh();
    }

    public void Hide() => gameObject.SetActive(false);

    private void Refresh()
    {
        if (_renderer == null) return;
        // 프리팹의 자식 Quad는 지름 1. 부모는 회전·스케일 없는 월드 루트를 권장합니다.
        Vector3 scale = transform.lossyScale;
        _renderer.transform.localScale = new Vector3(
            _radius * 2f / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            _radius * 2f / Mathf.Max(Mathf.Abs(scale.y), 0.0001f), 1f);
        _renderer.enabled = _radius > 0f;
        _renderer.sortingOrder = _sortingOrder;
        if (_properties == null) _properties = new MaterialPropertyBlock();
        _properties.SetFloat("_Radius", _radius);
        _properties.SetColor("_FillColor", _fillColor);
        _properties.SetColor("_BorderColor", _borderColor);
        _properties.SetFloat("_BorderWidth", _borderWidth);
        _properties.SetFloat("_Softness", _edgeSoftness);
        _properties.SetFloat("_DashCount", _dashCount);
        _properties.SetFloat("_DashRatio", _dashRatio);
        _properties.SetFloat("_RotationSpeed", _rotationSpeed);
        _properties.SetFloat("_PulseStrength", _pulseStrength);
        _properties.SetFloat("_PulseSpeed", _pulseSpeed);
        _renderer.SetPropertyBlock(_properties);
    }
}
