using UnityEngine;

// Bow firing
public class Arrow : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField, Min(0.01f)]
    private float radius = 0.1f;
    [SerializeField]
    private LayerMask wallLayers;

    [Header("Landing")]
    [SerializeField, Min(0f)]
    private float lingerTime = 1.5f;
    [SerializeField, Min(0f)]
    private float fadeTime = 0.4f;

    [Header("Sprite")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private bool sortByDepth = true;
    [SerializeField, Min(1f)]
    private float sortingPrecision = 16f;

    public bool IsFlying { get; private set; }

    private AttackSounds _sounds;
    private LayerMask _targetLayers;
    private Vector2 _groundDirection;
    private Vector2 _velocity;
    private float _yScale = Isometric.DefaultYScale;
    private float _damage;
    private float _knockback;
    private float _maxDistance;
    private float _travelled;
    private float _landedAt;
    private Color _baseColor = Color.white;

    private Vector2 Position => transform.position;

    private void Reset()
    {
        wallLayers = LayerMask.GetMask("Walls", "PushBlock");
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Walls", "PushBlock");

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            _baseColor = spriteRenderer.color;
    }

    public void Launch(Vector2 groundDirection, float yScale, float damage, float speed, float maxDistance,
        float knockback, LayerMask targetLayers, AttackSounds sounds)
    {
        _groundDirection = groundDirection.sqrMagnitude > 0.0001f ? groundDirection.normalized : Vector2.right;
        _yScale = yScale;
        _damage = damage;
        _knockback = knockback;
        _maxDistance = maxDistance;
        _targetLayers = targetLayers.value != 0 ? targetLayers : (LayerMask)LayerMask.GetMask("Enemy");
        _sounds = sounds;
        _travelled = 0f;

        _velocity = Isometric.ToScreen(_groundDirection, yScale) * speed;

        var screen = _velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.right;
        var degrees = Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, degrees);

        IsFlying = true;
    }

    private void FixedUpdate()
    {
        if (!IsFlying)
            return;

        var step = _velocity * Time.fixedDeltaTime;
        var groundStep = Isometric.ToGround(step, _yScale).magnitude;

        if (_travelled + groundStep > _maxDistance && groundStep > 0f) {
            step *= (_maxDistance - _travelled) / groundStep;
            groundStep = _maxDistance - _travelled;
        }

        var distance = step.magnitude;
        if (distance <= 0.0001f) {
            Land(Position);
            return;
        }

        var direction = step / distance;
        var position = Position;

        if (HitSomething(position, direction, distance))
            return;

        transform.position = new Vector3(position.x + step.x, position.y + step.y, transform.position.z);
        _travelled += groundStep;

        if (_travelled >= _maxDistance - 0.0001f)
            Land(Position);
    }

    private bool HitSomething(Vector2 position, Vector2 direction, float distance)
    {
        var enemyHit = Physics2D.CircleCast(position, radius, direction, distance, _targetLayers);
        var wallHit = wallLayers.value != 0
            ? Physics2D.CircleCast(position, radius, direction, distance, wallLayers)
            : default;

        var enemyFirst = enemyHit.collider != null
            && (wallHit.collider == null || enemyHit.distance <= wallHit.distance);

        if (enemyFirst) {
            var target = enemyHit.collider.GetComponentInParent<IDamagable>();

            if (target != null && target.IsAlive) {
                target.TakeDamage(_damage);

                if (target is IKnockbackable knockable)
                    knockable.ApplyKnockback(_groundDirection, _knockback);

                _sounds?.PlayImpact(enemyHit.point);
                Destroy(gameObject);
                return true;
            }
        }

        if (wallHit.collider != null) {
            _sounds?.PlayImpact(wallHit.point);
            Land(wallHit.centroid);
            return true;
        }

        return false;
    }

    private void Land(Vector2 point)
    {
        transform.position = new Vector3(point.x, point.y, transform.position.z);

        _velocity = Vector2.zero;
        IsFlying = false;
        _landedAt = Time.time;

        Destroy(gameObject, lingerTime + fadeTime);
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
            return;

        if (sortByDepth)
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * sortingPrecision);

        if (IsFlying || fadeTime <= 0f)
            return;

        var fade = Mathf.Clamp01((Time.time - _landedAt - lingerTime) / fadeTime);
        var color = _baseColor;
        color.a *= 1f - fade;
        spriteRenderer.color = color;
    }
}
