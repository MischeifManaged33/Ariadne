using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RectIndicator : MonoBehaviour
{
    [SerializeField]
    private Material material;

    [Header("UI")]
    [SerializeField]
    private Color areaColor = new Color(1f, 0.35f, 0.25f, 0.2f);
    [SerializeField]
    private Color fillColor = new Color(1f, 0.45f, 0.3f, 0.45f);
    [SerializeField]
    private Color flashColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField, Min(0f)]
    private float fadeSpeed = 14f;
    [SerializeField, Min(0f)]
    private float flashDuration = 0.12f;

    [Header("Sorting")]
    [SerializeField]
    private string sortingLayer = "Enemy";
    [SerializeField, Min(1f)]
    private float sortingPrecision = 16f;
    [SerializeField]
    private int sortingOffset = -1;

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;

    private readonly Vector3[] _vertices = new Vector3[8];
    private readonly Color[] _colors = new Color[8];
    private static readonly int[] Triangles = { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };

    private bool _visible;
    private float _alpha;
    private float _flashRemaining;

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "Attack Rect" };
        _mesh.MarkDynamic();
        _mesh.vertices = _vertices;
        _mesh.colors = _colors;
        _mesh.triangles = Triangles;
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

    public void Aim(Vector2 origin, Vector2 groundDirection, float length, float width, float fill, float yScale)
    {
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        NeutralizeParentScale();

        var along = groundDirection.sqrMagnitude > 0.0001f ? groundDirection.normalized : Vector2.right;
        var side = new Vector2(-along.y, along.x) * (width * 0.5f);

        Quad(0, along, side, length, yScale);
        Quad(4, along, side, length * Mathf.Clamp01(fill), yScale);

        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();

        _renderer.sortingOrder = Mathf.RoundToInt(-origin.y * sortingPrecision) + sortingOffset;
    }

    public void SetVisible(bool visible) => _visible = visible;

    public void Flash() => _flashRemaining = flashDuration;

    private void Quad(int start, Vector2 along, Vector2 side, float length, float yScale)
    {
        _vertices[start] = Isometric.ToScreen(side, yScale);
        _vertices[start + 1] = Isometric.ToScreen(-side, yScale);
        _vertices[start + 2] = Isometric.ToScreen(along * length + side, yScale);
        _vertices[start + 3] = Isometric.ToScreen(along * length - side, yScale);
    }

    private void LateUpdate()
    {
        var target = _visible ? 1f : 0f;
        _alpha = fadeSpeed > 0f ? Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime) : target;

        if (_flashRemaining > 0f)
            _flashRemaining = Mathf.Max(0f, _flashRemaining - Time.deltaTime);

        var showing = _alpha > 0.001f || _flashRemaining > 0f;
        _renderer.enabled = showing;

        if (!showing)
            return;

        var area = areaColor;
        var fill = fillColor;
        var alpha = _alpha;

        if (_flashRemaining > 0f && flashDuration > 0f) {
            var t = _flashRemaining / flashDuration;
            area = Color.Lerp(area, flashColor, t);
            fill = Color.Lerp(fill, flashColor, t);
            alpha = Mathf.Max(alpha, t);
        }

        area.a *= alpha;
        fill.a *= alpha;

        for (var i = 0; i < 4; i++) {
            _colors[i] = area;
            _colors[i + 4] = fill;
        }

        _mesh.colors = _colors;
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
