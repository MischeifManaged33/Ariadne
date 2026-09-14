using UnityEngine;

// A straight stripe along the ground that thins out down its length.
// Shows where a projectile is about to leave from without drawing the whole flight.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LineIndicator : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Range(2, 64)]
    private int segments = 12;
    [SerializeField, Min(0.01f)]
    private float width = 0.22f;
    [SerializeField]
    private Material material;

    [Header("Falloff")]
    [Tooltip("Fraction of the length that stays solid before it starts fading")]
    [SerializeField, Range(0f, 1f)]
    private float solidFraction = 0.25f;
    [Tooltip("Higher fades out sooner")]
    [SerializeField, Range(0.1f, 8f)]
    private float falloffCurve = 1.6f;

    [Header("UI")]
    [SerializeField]
    private Color tint = new Color(1f, 0.8f, 0.45f, 0.55f);
    [SerializeField]
    private Color flashColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Min(0f)]
    private float fadeSpeed = 16f;
    [SerializeField, Min(0f)]
    private float flashDuration = 0.12f;

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
    private float[] _falloff;

    private float _length;
    private float _width;
    private float _yScale = 1f;
    private float _heading = float.NaN;
    private int _builtSegments;

    private bool _visible;
    private float _alpha;
    private float _flashRemaining;

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "Indicator Line" };
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

    public void Aim(Vector2 origin, Vector2 groundDirection, float length, float yScale)
    {
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        NeutralizeParentScale();

        var heading = Mathf.Atan2(groundDirection.y, groundDirection.x) * Mathf.Rad2Deg;

        if (!Mathf.Approximately(length, _length) || !Mathf.Approximately(width, _width) ||
            !Mathf.Approximately(yScale, _yScale) || !Mathf.Approximately(heading, _heading) ||
            _builtSegments != segments) {
            _length = length;
            _width = width;
            _yScale = yScale;
            _heading = heading;
            Rebuild();
        }

        _renderer.sortingOrder = Mathf.RoundToInt(-origin.y * sortingPrecision) + sortingOffset;
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

        var color = tint;
        var alpha = _alpha;

        if (_flashRemaining > 0f && flashDuration > 0f) {
            var t = _flashRemaining / flashDuration;
            color = Color.Lerp(color, flashColor, t);
            alpha = Mathf.Max(alpha, t);
        }

        color.a *= alpha;
        ApplyTint(color);
    }

    private void ApplyTint(Color color)
    {
        if (_colors == null || _colors.Length == 0)
            return;

        for (var i = 0; i < _colors.Length; i++) {
            var vertex = color;
            vertex.a *= _falloff != null && i < _falloff.Length ? _falloff[i] : 1f;
            _colors[i] = vertex;
        }

        _mesh.colors = _colors;
    }

    private void Rebuild()
    {
        var count = Mathf.Max(2, segments);
        var vertexCount = (count + 1) * 2;

        if (_vertices == null || _vertices.Length != vertexCount) {
            _vertices = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
            _falloff = new float[vertexCount];
            _mesh.Clear();
            _builtSegments = 0;
        }

        var radians = _heading * Mathf.Deg2Rad;
        var along = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        var side = new Vector2(-along.y, along.x) * (_width * 0.5f);

        for (var i = 0; i <= count; i++) {
            var t = (float)i / count;
            var centre = along * (t * _length);

            // Built on the ground then squashed, so it lies flat like the floor
            var left = Isometric.ToScreen(centre + side, _yScale);
            var right = Isometric.ToScreen(centre - side, _yScale);

            _vertices[i * 2] = new Vector3(left.x, left.y, 0f);
            _vertices[i * 2 + 1] = new Vector3(right.x, right.y, 0f);

            var fade = Falloff(t);
            _falloff[i * 2] = fade;
            _falloff[i * 2 + 1] = fade;
        }

        _mesh.vertices = _vertices;

        if (_builtSegments != count) {
            var triangles = new int[count * 6];

            for (var i = 0; i < count; i++) {
                var vertex = i * 2;
                var triangle = i * 6;

                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            _mesh.triangles = triangles;
            _builtSegments = count;

            ApplyTint(Color.clear);
        }

        _mesh.RecalculateBounds();
    }

    private float Falloff(float t)
    {
        if (t <= solidFraction)
            return 1f;

        var span = 1f - solidFraction;
        if (span <= 0.0001f)
            return 0f;

        return Mathf.Pow(1f - (t - solidFraction) / span, falloffCurve);
    }
    // The mesh is built in world units, so a scaled parent would stretch the
    // drawing away from the zone the attack actually tests
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
