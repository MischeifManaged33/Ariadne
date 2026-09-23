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

    private Vector3[] _vertices;
    private Color[] _colors;

    private float _displayRadius;
    private float _flickerSeed;
    private int _builtRays;
    private float _nextSearchTime;

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
        var vertexCount = count * 4 + 1;

        if (_vertices == null || _vertices.Length != vertexCount) {
            _vertices = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
            _mesh.Clear();
            _builtRays = 0;
        }

        var reach = _displayRadius * Flicker();
        var far = FarRadius(origin, count);

        var edge = shade;
        edge.a = darkness;

        var lit = shade;
        lit.a = 0f;

        _vertices[0] = Vector3.zero;
        _colors[0] = lit;

        for (var i = 0; i < count; i++) {
            var radians = Mathf.PI * 2f * i / count;
            var ground = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            var screen = Isometric.ToScreen(ground * reach, isometricYScale);
            var length = screen.magnitude;

            if (length <= Mathf.Epsilon)
                continue;

            var direction = screen / length;
            var distance = CastDistance(origin, direction, length);
            var reached = distance / length;

            var core = Mathf.Min(distance, length * coreSize);
            var index = i * 4 + 1;

            _vertices[index] = direction * core;
            _vertices[index + 1] = direction * distance;
            _vertices[index + 2] = direction * distance;
            _vertices[index + 3] = direction * far;

            var faded = edge;
            faded.a = darkness * Falloff(reached);

            _colors[index] = lit;
            _colors[index + 1] = faded;
            _colors[index + 2] = edge;
            _colors[index + 3] = edge;
        }

        _mesh.vertices = _vertices;
        _mesh.colors = _colors;

        if (_builtRays != count) {
            _mesh.triangles = BuildTriangles(count);
            _builtRays = count;
        }

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

    private static int[] BuildTriangles(int count)
    {
        var triangles = new int[count * 15];

        for (var i = 0; i < count; i++) {
            var here = i * 4 + 1;
            var next = (i + 1) % count * 4 + 1;
            var t = i * 15;

            triangles[t] = 0;
            triangles[t + 1] = here;
            triangles[t + 2] = next;

            triangles[t + 3] = here;
            triangles[t + 4] = here + 1;
            triangles[t + 5] = next + 1;
            triangles[t + 6] = here;
            triangles[t + 7] = next + 1;
            triangles[t + 8] = next;

            triangles[t + 9] = here + 2;
            triangles[t + 10] = here + 3;
            triangles[t + 11] = next + 3;
            triangles[t + 12] = here + 2;
            triangles[t + 13] = next + 3;
            triangles[t + 14] = next + 2;
        }

        return triangles;
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
