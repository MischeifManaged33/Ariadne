using UnityEngine;

// The axe of the minotaur
public class ThrownAxe : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField, Min(0.1f)]
    private float speed = 11f;
    [SerializeField, Min(0.1f)]
    private float maxDistance = 14f;
    [SerializeField, Min(0.01f)]
    private float radius = 0.2f;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;

    [Header("Damage")]
    [SerializeField, Min(0f)]
    private float damage = 20f;
    [SerializeField]
    private LayerMask wallLayers;
    [SerializeField]
    private LayerMask targetLayers;

    [Header("Sprite")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private float spinSpeed = 720f;
    [SerializeField]
    private float stuckTilt = 35f;
    [SerializeField]
    private bool sortByDepth = true;
    [SerializeField, Min(1f)]
    private float sortingPrecision = 16f;

    public float Speed => speed;
    public bool IsFlying { get; private set; }
    public bool IsStuck { get; private set; }
    public Vector2 Position => transform.position;

    private Vector2 _velocity;
    private float _travelled;

    private void Reset()
    {
        wallLayers = LayerMask.GetMask("Walls");
        targetLayers = PlayerMask();

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Walls");

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Launch(Vector2 groundDirection, float yScale)
    {
        isometricYScale = yScale;

        var screen = Isometric.ToScreen(groundDirection.normalized, yScale);
        if (screen.sqrMagnitude <= 0.0001f)
            screen = Vector2.down;

        _velocity = screen.normalized * speed;
        _travelled = 0f;

        IsFlying = true;
        IsStuck = false;

        transform.rotation = Quaternion.identity;
    }

    public void Retrieve()
    {
        IsFlying = false;
        IsStuck = false;

        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (!IsFlying)
            return;

        var step = _velocity * Time.fixedDeltaTime;
        var distance = step.magnitude;

        if (distance <= 0.0001f)
            return;

        var direction = step / distance;
        var position = Position;

        if (HitSomething(position, direction, distance))
            return;

        transform.position = new Vector3(position.x + step.x, position.y + step.y, transform.position.z);

        _travelled += Isometric.ToGround(step, isometricYScale).magnitude;

        if (_travelled >= maxDistance)
            Stick(Position);
    }

    private bool HitSomething(Vector2 position, Vector2 direction, float distance)
    {
        var player = Physics2D.CircleCast(position, radius, direction, distance, ResolveMask(targetLayers));

        if (player.collider != null) {
            var health = player.collider.GetComponentInParent<PlayerHealth>();

            if (health != null) {
                health.TakeDamage(damage);
                Stick(player.point - direction * radius);
                return true;
            }
        }

        if (wallLayers.value != 0) {
            var wall = Physics2D.CircleCast(position, radius, direction, distance, wallLayers);

            if (wall.collider != null) {
                Stick(wall.point - direction * radius);
                return true;
            }
        }

        return false;
    }

    private void Stick(Vector2 point)
    {
        transform.position = new Vector3(point.x, point.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, stuckTilt);

        _velocity = Vector2.zero;
        IsFlying = false;
        IsStuck = true;
    }

    private void LateUpdate()
    {
        if (IsFlying && spinSpeed != 0f)
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        if (sortByDepth && spriteRenderer != null)
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * sortingPrecision);
    }

    private static int ResolveMask(LayerMask mask)
    {
        return mask.value != 0 ? mask.value : Physics2D.AllLayers;
    }


    private static LayerMask PlayerMask()
    {
        var mask = LayerMask.GetMask("Player");

        return mask != 0 ? mask : LayerMask.GetMask("Default");
    }
}
