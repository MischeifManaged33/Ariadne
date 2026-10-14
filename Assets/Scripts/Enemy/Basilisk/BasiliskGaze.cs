using System.Collections.Generic;
using UnityEngine;

// Petrify
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BasiliskGaze : MonoBehaviour
{
    [Header("Cone")]
    [SerializeField, Min(0.1f)]
    private float range = 5f;
    [SerializeField, Range(5f, 180f)]
    private float angle = 60f;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;

    [Header("Occlusion")]
    [SerializeField]
    private LayerMask sightBlockers;
    [SerializeField, Range(8, 128)]
    private int rays = 40;
    [SerializeField, Min(0f)]
    private float wallOvershoot = 0.1f;

    [Header("Look")]
    // Off hides the cone without changing what it petrifies
    [SerializeField]
    private bool showIndicator = true;
    [SerializeField]
    private Color calmColor = new Color(0.55f, 0.9f, 0.45f, 0.22f);
    [SerializeField]
    private Color chargedColor = new Color(0.9f, 1f, 0.55f, 0.55f);
    [SerializeField, Range(0f, 1f)]
    private float edgeFade = 0.3f;
    [SerializeField, Min(0f)]
    private float fadeSpeed = 6f;
    [SerializeField]
    private Material material;
    [SerializeField]
    private string sortingLayer = "FloorDecor";
    [SerializeField]
    private int sortingOrder = 10;

    public float Range => range;
    public float Angle => angle;
    public float YScale => isometricYScale;
    public float Charge { get; set; }
    public bool Active { get; set; }

    public bool ShowIndicator {
        get => showIndicator;
        set => showIndicator = value;
    }

    public Vector2 Origin { get; private set; }
    public Vector2 Facing { get; private set; } = Vector2.left;

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private float _alpha;

    private readonly List<Vector3> _vertices = new();
    private readonly List<Color> _colors = new();
    private readonly List<int> _triangles = new();

    private void Reset()
    {
        sightBlockers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "Basilisk Gaze" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        _renderer.sharedMaterial = material != null ? material : IndicatorMaterial.CreateDefault();
        _renderer.sortingLayerName = sortingLayer;
        _renderer.sortingOrder = sortingOrder;
        _renderer.enabled = false;

        if (sightBlockers.value == 0)
            sightBlockers = LayerMask.GetMask("Walls");
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    public void Aim(Vector2 origin, Vector2 groundFacing)
    {
        Origin = origin;

        if (groundFacing.sqrMagnitude > 0.0001f)
            Facing = groundFacing.normalized;
    }

    // Inside the cone and not behind a wall
    public bool Contains(Vector2 point)
    {
        var ground = Isometric.ToGround(point - Origin, isometricYScale);

        if (ground.sqrMagnitude > range * range)
            return false;
        if (ground.sqrMagnitude > 0.0001f && Vector2.Angle(ground, Facing) > angle * 0.5f)
            return false;

        return sightBlockers.value == 0 || Physics2D.Linecast(Origin, point, sightBlockers).collider == null;
    }

    private void LateUpdate()
    {
        var target = Active && showIndicator ? 1f : 0f;
        _alpha = fadeSpeed > 0f ? Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime) : target;

        if (_alpha <= 0.001f) {
            _renderer.enabled = false;
            return;
        }

        transform.position = new Vector3(Origin.x, Origin.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        NeutralizeParentScale();

        Rebuild();

        _renderer.enabled = true;
    }

    private void Rebuild()
    {
        var count = Mathf.Max(8, rays);
        var heading = Mathf.Atan2(Facing.y, Facing.x);
        var half = angle * 0.5f * Mathf.Deg2Rad;

        var tint = Color.Lerp(calmColor, chargedColor, Mathf.Clamp01(Charge));
        tint.a *= _alpha;

        var rim = tint;
        rim.a *= edgeFade;

        _vertices.Clear();
        _colors.Clear();
        _triangles.Clear();

        _vertices.Add(Vector3.zero);
        _colors.Add(tint);

        for (var i = 0; i <= count; i++) {
            var radians = heading - half + half * 2f * i / count;
            var ground = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            var screen = Isometric.ToScreen(ground * range, isometricYScale);
            var length = screen.magnitude;
            var direction = screen / length;

            _vertices.Add(direction * CastDistance(direction, length));
            _colors.Add(rim);

            if (i == 0)
                continue;

            _triangles.Add(0);
            _triangles.Add(i);
            _triangles.Add(i + 1);
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0, false);
        _mesh.RecalculateBounds();
    }

    private float CastDistance(Vector2 direction, float length)
    {
        if (sightBlockers.value == 0)
            return length;

        var hit = Physics2D.Raycast(Origin, direction, length, sightBlockers);
        if (hit.collider == null)
            return length;

        return Mathf.Min(hit.distance + wallOvershoot, length);
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

    private void OnValidate()
    {
        if (_renderer == null)
            return;

        _renderer.sortingLayerName = sortingLayer;
        _renderer.sortingOrder = sortingOrder;
    }
}
