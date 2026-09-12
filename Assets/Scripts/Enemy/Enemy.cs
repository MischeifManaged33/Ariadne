using System;
using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, IDamagable, IKnockbackable
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 50f;


    [Header("Death")]
    [SerializeField]
    private bool destroyOnDeath = true;
    [SerializeField, Min(0f)]
    private float destroyDelay = 0f;


    [Header("Knockback")]
    [SerializeField, Min(0f)]
    private float knockbackSpeed = 6f;
    [SerializeField, Min(0f)]
    private float knockbackDuration = 0.15f;
    [SerializeField]
    private LayerMask wallLayers;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = 0.5f;
    [SerializeField, Min(0f)]
    private float wallSkin = 0.02f;


    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action Died;

    // IDamagable
    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    public bool IsKnockedBack => _knockback.sqrMagnitude > 0f;

    protected Rigidbody2D Body { get; private set; }

    private bool _dead;
    private HitFlash _hitFlash;
    private Collider2D _collider;
    private Vector2 _knockback;
    private ContactFilter2D _wallFilter;
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];

    private Vector2 Position => Body != null ? Body.position : (Vector2)transform.position;

    protected virtual void Reset()
    {
        ConfigureBody(GetComponent<Rigidbody2D>());

        wallLayers = LayerMask.GetMask("Walls");
    }

    protected virtual void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        ConfigureBody(Body);

        _collider = GetComponent<Collider2D>();
        _hitFlash = HitFlash.GetOrAdd(gameObject);

        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Walls");

        _wallFilter = new ContactFilter2D {
            useLayerMask = true,
            layerMask = wallLayers,
            useTriggers = false
        };

        CurrentHealth = maxHealth;
    }

    protected virtual void Start()
    {
        RaiseHealthChanged();
    }

    protected virtual void FixedUpdate()
    {
        StepKnockback(Time.fixedDeltaTime);
    }

    public virtual float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= applied;

        _hitFlash.Flash();

        Damaged?.Invoke(applied);
        RaiseHealthChanged();

        if (!IsAlive)
            Die();

        return applied;
    }

    public virtual float Heal(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, maxHealth - CurrentHealth);
        if (applied <= 0f)
            return 0f;

        CurrentHealth += applied;

        Healed?.Invoke(applied);
        RaiseHealthChanged();

        return applied;
    }

    public virtual void ApplyKnockback(Vector2 direction, float strength = 1f)
    {
        if (!IsAlive || strength <= 0f || knockbackSpeed <= 0f || knockbackDuration <= 0f)
            return;

        var screen = new Vector2(direction.x, direction.y * isometricYScale);
        if (screen.sqrMagnitude <= 0.0001f)
            return;

        _knockback = screen.normalized * (knockbackSpeed * strength);
    }

    public void CancelKnockback() => _knockback = Vector2.zero;

    public virtual void ResetHealth()
    {
        CurrentHealth = maxHealth;
        _dead = false;
        _knockback = Vector2.zero;
        RaiseHealthChanged();
    }

    protected virtual void Die()
    {
        if (_dead)
            return;

        _dead = true;
        _knockback = Vector2.zero;

        Died?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private void StepKnockback(float deltaTime)
    {
        if (_knockback.sqrMagnitude <= 0.0001f) {
            _knockback = Vector2.zero;
            return;
        }

        var step = _knockback * deltaTime;
        var distance = step.magnitude;
        var direction = step / distance;
        var travel = Mathf.Min(distance, FreeDistance(direction, distance));

        if (travel > 0f)
            MoveTo(Position + direction * travel);

        if (travel < distance) {
            _knockback = Vector2.zero;
            return;
        }

        var deceleration = knockbackSpeed / knockbackDuration;
        _knockback = Vector2.MoveTowards(_knockback, Vector2.zero, deceleration * deltaTime);
    }
    private float FreeDistance(Vector2 direction, float distance)
    {
        if (wallLayers.value == 0)
            return distance;

        var probe = distance + wallSkin;

        if (_collider == null) {
            var ray = Physics2D.Raycast(Position, direction, probe, wallLayers);
            return ray.collider != null ? Mathf.Max(0f, ray.distance - wallSkin) : distance;
        }

        var count = _collider.Cast(direction, _wallFilter, _castHits, probe);

        var nearest = distance;
        for (var i = 0; i < count; i++) {
            var hit = _castHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            nearest = Mathf.Min(nearest, Mathf.Max(0f, hit.distance - wallSkin));
        }

        return nearest;
    }

    private void MoveTo(Vector2 position)
    {
        if (Body != null && Body.bodyType != RigidbodyType2D.Static)
            Body.MovePosition(position);
        else
            transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void RaiseHealthChanged() => HealthChanged?.Invoke(CurrentHealth, maxHealth);

    private static void ConfigureBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        if (body.bodyType == RigidbodyType2D.Static)
            body.bodyType = RigidbodyType2D.Kinematic;

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    protected virtual void OnValidate()
    {
        if (Application.isPlaying)
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
    }
}
