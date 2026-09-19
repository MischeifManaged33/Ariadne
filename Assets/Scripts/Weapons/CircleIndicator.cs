using UnityEngine;

// Circle shapped attack indicator that get filled
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CircleIndicator : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Range(8, 96)]
    private int segments = 48;
    [SerializeField, Min(0.01f)]
    private float ringWidth = 0.12f;
    [SerializeField]
    private Material material;

    [Header("UI")]
    [SerializeField]
    private Color ringColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    [SerializeField]
    private Color fillColor = new Color(1f, 0.3f, 0.3f, 0.3f);
    [SerializeField]
    private Color flashColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Min(0f)]
    private float fadeSpeed = 12f;
    [SerializeField, Min(0f)]
    private float flashDuration = 0.15f;

    [Header("Sorting")]
    [SerializeField]
    private string sortingLayer = "Default";
    [SerializeField, Min(1f)]
    private float sortingPrecision = 16f;
    [SerializeField]
    private int sortingOffset = -1;

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;

    private Vector3[] _vertices;
    private Color[] _colors;

    private float _radius = -1f;
    private float _fill = -1f;
    private float _yScale = 1f;
    private int _builtSegments;

    private bool _visible;
    private float _alpha;
    private float _flashRemaining;

    private Color _appliedFill;
    private Color _appliedRing;
    private bool _tinted;

    private int RingStart => _builtSegments + 2;

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "Indicator Circle" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        _renderer.sharedMaterial = material != null ? material : IndicatorMaterial.CreateDefault();

        _renderer.sortingLayerName = sortingLayer;
        _renderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    public void Aim(Vector2 centre, float radius, float fill, float yScale)
    {
        transform.position = new Vector3(centre.x, centre.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        NeutralizeParentScale();

        var clamped = Mathf.Clamp01(fill);

        if (!Mathf.Approximately(radius, _radius) || !Mathf.Approximately(clamped, _fill) ||
            !Mathf.Approximately(yScale, _yScale) || _builtSegments != segments) {
            _radius = radius;
            _fill = clamped;
            _yScale = yScale;
            Rebuild();
        }

        _renderer.sortingOrder = Mathf.RoundToInt(-centre.y * sortingPrecision) + sortingOffset;
    }

    public void SetVisible(bool visible) => _visible = visible;

    public void Flash() => _flashRemaining = flashDuration;

    private void LateUpdate()
    {
        var target = _visible ? 1f : 0f;
        _alpha = fadeSpeed > 0f ? Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime) : target;

        if (_flashRemaining > 0f)
            _flashRemaining = Mathf.Max(0f, _flashRemaining - Time.deltaTime);

        var showing = (_alpha > 0.001f || _flashRemaining > 0f) && _builtSegments > 0;
        _renderer.enabled = showing;

        if (!showing)
            return;

        var fill = fillColor;
        var ring = ringColor;
        var alpha = _alpha;

        if (_flashRemaining > 0f && flashDuration > 0f) {
            var t = _flashRemaining / flashDuration;
            fill = Color.Lerp(fill, flashColor, t);
            ring = Color.Lerp(ring, flashColor, t);
            alpha = Mathf.Max(alpha, t);
        }

        fill.a *= alpha;
        ring.a *= alpha;

        ApplyTint(fill, ring);
    }

    private void ApplyTint(Color fill, Color ring)
    {
        if (_colors == null || _colors.Length == 0)
            return;

        // Uploading the colour buffer is the expensive half of the draw, and the
        // tint only actually moves while it is fading in or flashing
        if (_tinted && fill == _appliedFill && ring == _appliedRing)
            return;

        _tinted = true;
        _appliedFill = fill;
        _appliedRing = ring;

        var ringStart = RingStart;

        for (var i = 0; i < _colors.Length; i++)
            _colors[i] = i < ringStart ? fill : ring;

        _mesh.colors = _colors;
    }

    private void Rebuild()
    {
        var count = Mathf.Max(8, segments);

        var vertexCount = count + 2 + (count + 1) * 2;

        if (_vertices == null || _vertices.Length != vertexCount) {
            _vertices = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
            _mesh.Clear();
            _builtSegments = 0;
            _tinted = false;
        }

        var fillRadius = _radius * _fill;
        var innerRadius = Mathf.Max(0f, _radius - ringWidth);
        var ringStart = count + 2;

        _vertices[0] = Vector3.zero;

        for (var i = 0; i <= count; i++) {
            var radians = Mathf.PI * 2f * i / count;
            var unit = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            // Built on the ground then squashed, so it lies flat like the floor
            _vertices[i + 1] = Flatten(unit * fillRadius);
            _vertices[ringStart + i * 2] = Flatten(unit * _radius);
            _vertices[ringStart + i * 2 + 1] = Flatten(unit * innerRadius);
        }

        _mesh.vertices = _vertices;

        if (_builtSegments != count) {
            var triangles = new int[count * 9];

            for (var i = 0; i < count; i++) {
                var disc = i * 3;

                triangles[disc] = 0;
                triangles[disc + 1] = i + 1;
                triangles[disc + 2] = i + 2;

                var band = count * 3 + i * 6;
                var outer = ringStart + i * 2;

                triangles[band] = outer;
                triangles[band + 1] = outer + 2;
                triangles[band + 2] = outer + 1;
                triangles[band + 3] = outer + 1;
                triangles[band + 4] = outer + 2;
                triangles[band + 5] = outer + 3;
            }

            _mesh.triangles = triangles;
            _builtSegments = count;

            ApplyTint(Color.clear, Color.clear);
        }

        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(_radius * 2f, _radius * 2f * _yScale, 0f));
    }

    private Vector3 Flatten(Vector2 ground)
    {
        var screen = Isometric.ToScreen(ground, _yScale);

        return new Vector3(screen.x, screen.y, 0f);
    }

    private void NeutralizeParentScale()
    {
        var parent = transform.parent;

        if (parent == null) {
            transform.localScale = Vector3.one;
            return;
        }

        var scale = parent.lossyScale;

        transform.localScale = new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
    }
}
