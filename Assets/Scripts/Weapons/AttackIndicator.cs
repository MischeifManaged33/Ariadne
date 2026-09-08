using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AttackIndicator : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Range(3, 96)]
    private int segments = 32;
    [SerializeField]
    private Material material;

    [Header("UI")]
    [SerializeField]
    private Color readyColor = new Color(1f, 0.95f, 0.7f, 0.45f);
    [SerializeField]
    private Color cooldownColor = new Color(0.6f, 0.6f, 0.6f, 0.18f);
    [SerializeField]
    private Color flashColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField, Range(0f, 1f)]
    private float edgeFade = 0.35f;
    [SerializeField, Min(0f)]
    private float fadeSpeed = 14f;
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

    private float _range;
    private float _angle;
    private float _yScale = 1f;
    private float _direction = float.NaN;
    private int _builtSegments;

    private bool _visible;
    private bool _ready = true;
    private float _alpha;
    private float _flashRemaining;

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "Attack Fan" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        _renderer.sharedMaterial = material != null ? material : CreateDefaultMaterial();

        _renderer.sortingLayerName = sortingLayer;
        _renderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }
    public void Aim(Vector2 origin, Vector2 groundDirection, float range, float angle, float yScale)
    {
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        transform.rotation = Quaternion.identity;

        var heading = Mathf.Atan2(groundDirection.y, groundDirection.x) * Mathf.Rad2Deg;

        if (!Mathf.Approximately(range, _range) || !Mathf.Approximately(angle, _angle) ||
            !Mathf.Approximately(yScale, _yScale) || !Mathf.Approximately(heading, _direction) ||
            _builtSegments != segments) {
            _range = range;
            _angle = angle;
            _yScale = yScale;
            _direction = heading;
            Rebuild();
        }

        _renderer.sortingOrder = Mathf.RoundToInt(-origin.y * sortingPrecision) + sortingOffset;
    }

    public void SetVisible(bool visible) => _visible = visible;
    public void SetReady(bool ready) => _ready = ready;
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

        var tint = _ready ? readyColor : cooldownColor;
        var alpha = _alpha;

        if (_flashRemaining > 0f && flashDuration > 0f) {
            var t = _flashRemaining / flashDuration;
            tint = Color.Lerp(tint, flashColor, t);
            alpha = Mathf.Max(alpha, t);
        }

        tint.a *= alpha;
        ApplyTint(tint);
    }

    private void ApplyTint(Color tint)
    {
        if (_colors == null || _colors.Length == 0)
            return;

        var rim = tint;
        rim.a *= edgeFade;

        _colors[0] = tint;
        for (var i = 1; i < _colors.Length; i++)
            _colors[i] = rim;

        _mesh.colors = _colors;
    }

    private void Rebuild()
    {
        var count = Mathf.Max(3, segments);

        if (_vertices == null || _vertices.Length != count + 2) {
            _vertices = new Vector3[count + 2];
            _colors = new Color[count + 2];
            _mesh.Clear();
            _builtSegments = 0;
        }

        _vertices[0] = Vector3.zero;

        var half = _angle * 0.5f;

        for (var i = 0; i <= count; i++) {
            var degrees = _direction - half + _angle * i / count;
            var radians = degrees * Mathf.Deg2Rad;

            // Swept on the ground plane, then flattened into the isometric view.
            _vertices[i + 1] = new Vector3(
                Mathf.Cos(radians) * _range,
                Mathf.Sin(radians) * _range * _yScale,
                0f);
        }

        _mesh.vertices = _vertices;

        if (_builtSegments != count) {
            var triangles = new int[count * 3];
            for (var i = 0; i < count; i++) {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _mesh.triangles = triangles;
            _builtSegments = count;

            ApplyTint(Color.clear);
        }

        _mesh.RecalculateBounds();
    }

    private static Material CreateDefaultMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null) {
            Debug.LogError("AttackIndicator found no usable sprite shader. Assign a Material instead.");
            return null;
        }

        return new Material(shader) {
            name = "Attack Indicator (Runtime)",
            mainTexture = Texture2D.whiteTexture
        };
    }
}
