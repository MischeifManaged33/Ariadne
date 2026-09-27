using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(300)]
public class Darkness : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform target;
    [SerializeField]
    private bool findPlayerAutomatically = true;
    [SerializeField]
    private Vector2 originOffset = Vector2.zero;

    [Header("Aura")]
    [SerializeField, Min(0.1f)]
    private float radius = 6f;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;
    [SerializeField, Range(0f, 1f)]
    private float coreSize = 0.35f;
    [SerializeField, Range(0f, 1f)]
    private float darkness = 1f;
    [SerializeField, Range(0.2f, 4f)]
    private float falloffPower = 1.6f;
    [SerializeField, Min(0f)]
    private float radiusLerpSpeed = 6f;

    [Header("Occlusion")]
    [SerializeField, Range(16, 256)]
    private int rays = 96;
    [SerializeField]
    private LayerMask sightBlockers;
    [SerializeField, Min(0f)]
    private float wallOvershoot = 0.5f;
    [SerializeField, Min(0.01f)]
    private float minDistance = 0.25f;
    // A shadow edge lands between two rays, so it would snap a whole ray apart as the
    // player walks. These close in on the corner that threw it instead
    [SerializeField, Range(0, 10)]
    private int edgeRefinement = 6;
    [SerializeField, Min(0.01f)]
    private float edgeThreshold = 0.35f;
    [SerializeField, Range(0, 128)]
    private int maxEdges = 48;

    [Header("Flicker")]
    [SerializeField, Range(0f, 0.5f)]
    private float flickerAmount = 0.04f;
    [SerializeField, Min(0f)]
    private float flickerSpeed = 1.5f;

    [Header("Look")]
    [SerializeField]
    private Color shade = Color.black;
    [SerializeField]
    private Material material;
    [SerializeField]
    private string sortingLayer = "TopDecor";
    [SerializeField]
    private int sortingOrder = 32000;
    [SerializeField, Min(0f)]
    private float screenMargin = 2f;

    public Transform Target => target;

    public float Radius {
        get => radius;
        set => radius = Mathf.Max(0.1f, value);
    }

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private Camera _camera;

    private readonly List<Probe> _probes = new();
    private readonly List<Probe> _fan = new();
    private readonly List<Vector3> _vertices = new();
    private readonly List<Color> _colors = new();
    private readonly List<int> _triangles = new();

    private float _displayRadius;
    private float _flickerSeed;
    private float _reach;
    private float _nextSearchTime;

    // One ray of the fan: where it was aimed, and how far it got before a wall stopped it
    private struct Probe
    {
        public float Radians;
        public Vector2 Direction;
        public float Distance;
        public float Length;
    }

    private void Reset()
    {
        sightBlockers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "Darkness" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        _renderer.sharedMaterial = material != null ? material : IndicatorMaterial.CreateDefault();
        _renderer.sortingLayerName = sortingLayer;
        _renderer.sortingOrder = sortingOrder;
        _renderer.enabled = false;

        if (sightBlockers.value == 0)
            sightBlockers = LayerMask.GetMask("Walls");

        _displayRadius = radius;
        _flickerSeed = Random.value * 100f;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    private void LateUpdate()
    {
        if (target == null)
            FindTarget();

        if (target == null) {
            _renderer.enabled = false;
            return;
        }

        var origin = (Vector2)target.position + originOffset;

        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        _displayRadius = radiusLerpSpeed > 0f
            ? Mathf.Lerp(_displayRadius, radius, 1f - Mathf.Exp(-radiusLerpSpeed * Time.deltaTime))
            : radius;

        Rebuild(origin);

        _renderer.enabled = true;
    }

    public void SetTarget(Transform value) => target = value;

    public void SetRadius(float value, bool immediate = false)
    {
        Radius = value;

        if (immediate)
            _displayRadius = radius;
    }

    private void FindTarget()
    {
        if (!findPlayerAutomatically || Time.time < _nextSearchTime)
            return;

        _nextSearchTime = Time.time + 0.5f;

        var player = PlayerHealth.Current != null ? PlayerHealth.Current : FindAnyObjectByType<PlayerHealth>();
        if (player != null)
            target = player.transform;
    }

    private void Rebuild(Vector2 origin)
    {
        var count = Mathf.Max(16, rays);

        _reach = _displayRadius * Flicker();

        var far = FarRadius(origin, count);
        var step = Mathf.PI * 2f / count;

        _probes.Clear();
        for (var i = 0; i < count; i++)
            _probes.Add(Trace(origin, step * i));

        BuildFan(origin, count, step);
        BuildMesh(far);
    }

    private void BuildFan(Vector2 origin, int count, float step)
    {
        _fan.Clear();

        var budget = maxEdges;

        for (var i = 0; i < count; i++) {
            var near = _probes[i];
            var far = _probes[(i + 1) % count];

            _fan.Add(near);

            if (budget <= 0 || edgeRefinement <= 0)
                continue;
            if (Mathf.Abs(near.Distance - far.Distance) <= edgeThreshold)
                continue;

            budget--;
            Refine(origin, near, far, step * (i + 1));
        }
    }

    private void Refine(Vector2 origin, Probe near, Probe far, float farRadians)
    {
        var nearRadians = near.Radians;

        for (var i = 0; i < edgeRefinement; i++) {
            var middle = (nearRadians + farRadians) * 0.5f;
            var probe = Trace(origin, middle);

            if (Mathf.Abs(probe.Distance - near.Distance) <= Mathf.Abs(probe.Distance - far.Distance)) {
                near = probe;
                nearRadians = middle;
            }
            else {
                far = probe;
                farRadians = middle;
            }
        }

        _fan.Add(near);
        _fan.Add(far);
    }

    private Probe Trace(Vector2 origin, float radians)
    {
        var ground = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

        var screen = Isometric.ToScreen(ground * _reach, isometricYScale);
        var length = screen.magnitude;

        if (length <= Mathf.Epsilon)
            return new Probe { Radians = radians, Direction = Vector2.right };

        var direction = screen / length;

        return new Probe {
            Radians = radians,
            Direction = direction,
            Distance = CastDistance(origin, direction, length),
            Length = length
        };
    }

    private void BuildMesh(float far)
    {
        var edge = shade;
        edge.a = darkness;

        var lit = shade;
        lit.a = 0f;

        _vertices.Clear();
        _colors.Clear();
        _triangles.Clear();

        _vertices.Add(Vector3.zero);
        _colors.Add(lit);

        foreach (var probe in _fan) {
            var reached = probe.Length > 0f ? probe.Distance / probe.Length : 0f;
            var core = Mathf.Min(probe.Distance, probe.Length * coreSize);

            _vertices.Add(probe.Direction * core);
            _vertices.Add(probe.Direction * probe.Distance);
            _vertices.Add(probe.Direction * probe.Distance);
            _vertices.Add(probe.Direction * far);

            var faded = edge;
            faded.a = darkness * Falloff(reached);

            _colors.Add(lit);
            _colors.Add(faded);
            _colors.Add(edge);
            _colors.Add(edge);
        }

        var count = _fan.Count;

        for (var i = 0; i < count; i++) {
            var here = i * 4 + 1;
            var next = (i + 1) % count * 4 + 1;

            _triangles.Add(0);
            _triangles.Add(here);
            _triangles.Add(next);

            _triangles.Add(here);
            _triangles.Add(here + 1);
            _triangles.Add(next + 1);
            _triangles.Add(here);
            _triangles.Add(next + 1);
            _triangles.Add(next);

            _triangles.Add(here + 2);
            _triangles.Add(here + 3);
            _triangles.Add(next + 3);
            _triangles.Add(here + 2);
            _triangles.Add(next + 3);
            _triangles.Add(next + 2);
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0, false);

        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(far * 2f, far * 2f, 0.1f));
    }

    private float CastDistance(Vector2 origin, Vector2 direction, float length)
    {
        if (sightBlockers.value == 0)
            return length;

        var hit = Physics2D.Raycast(origin, direction, length, sightBlockers);
        if (hit.collider == null)
            return length;

        return Mathf.Clamp(hit.distance + wallOvershoot, Mathf.Min(minDistance, length), length);
    }

    private float Falloff(float reached)
    {
        if (reached <= coreSize)
            return 0f;
        if (coreSize >= 1f)
            return 1f;

        return Mathf.Pow(Mathf.Clamp01((reached - coreSize) / (1f - coreSize)), falloffPower);
    }

    private float Flicker()
    {
        if (flickerAmount <= 0f || flickerSpeed <= 0f)
            return 1f;

        var noise = Mathf.PerlinNoise(_flickerSeed + Time.time * flickerSpeed, 0f);

        return 1f + (noise - 0.5f) * 2f * flickerAmount;
    }
    private float FarRadius(Vector2 origin, int count)
    {
        if (_camera == null)
            _camera = Camera.main;

        var reach = _displayRadius * 4f + screenMargin;

        if (_camera != null && _camera.orthographic) {
            var height = _camera.orthographicSize;
            var corner = new Vector2(height * _camera.aspect, height).magnitude;

            reach = Vector2.Distance(origin, _camera.transform.position) + corner + screenMargin;
        }

    
        return reach / Mathf.Cos(Mathf.PI / count);
    }

    private void OnValidate()
    {
        if (_renderer == null)
            return;

        _renderer.sortingLayerName = sortingLayer;
        _renderer.sortingOrder = sortingOrder;
    }

    private void OnDrawGizmosSelected()
    {
        var origin = target != null ? (Vector2)target.position + originOffset : (Vector2)transform.position;

        DrawGroundCircle(origin, radius, new Color(1f, 0.9f, 0.5f, 0.8f));
        DrawGroundCircle(origin, radius * coreSize, new Color(1f, 0.9f, 0.5f, 0.35f));
    }

    private void DrawGroundCircle(Vector2 centre, float groundRadius, Color color)
    {
        if (groundRadius <= 0f)
            return;

        Gizmos.color = color;

        var previous = centre + new Vector2(groundRadius, 0f);

        for (var i = 1; i <= 32; i++) {
            var radians = Mathf.PI * 2f * i / 32f;
            var point = centre + new Vector2(
                Mathf.Cos(radians) * groundRadius,
                Mathf.Sin(radians) * groundRadius * isometricYScale);

            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
