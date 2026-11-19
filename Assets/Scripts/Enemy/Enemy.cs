using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, IDamagable, IKnockbackable
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 50f;
    [SerializeField]
    private bool showHealthBar = false;


    [Header("Death")]
    [SerializeField]
    private bool destroyOnDeath = true;
    [SerializeField, Min(0f)]
    private float destroyDelay = 0f;
    [SerializeField]
    private DropTable dropTable;


    [Header("Sounds")]
    [SerializeField]
    private EventReference hurtSound;
    [SerializeField]
    private EventReference deathSound;


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

    [Header("Attack Movement")]
    [Tooltip("While an attack moves this enemy, it passes through the player instead of shoving them.")]
    [SerializeField]
    private bool phaseThroughPlayer = true;
    [Tooltip("How fast the enemy backs off the player after an attack leaves them overlapping.")]
    [SerializeField, Min(0f)]
    private float separationSpeed = 6f;


    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action Died;

    // IDamagable
    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    public bool ShowHealthBar {
        get => showHealthBar;
        set => showHealthBar = value;
    }

    public bool IsKnockedBack => _knockback.sqrMagnitude > 0f;

    protected Rigidbody2D Body { get; private set; }

    private bool _dead;
    private HitFlash _hitFlash;
    private Collider2D _collider;
    private Vector2 _knockback;
    private ContactFilter2D _wallFilter;
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];
    private readonly Collider2D[] _overlapHits = new Collider2D[8];

    private readonly List<Collider2D> _ownColliders = new List<Collider2D>();
    private readonly List<Collider2D> _ignoredPlayer = new List<Collider2D>();
    private bool _phasing;
    private bool _separating;

    public bool IsPhasingThroughPlayer => _ignoredPlayer.Count > 0;

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

        foreach (var own in GetComponents<Collider2D>()) {
            if (!own.isTrigger)
                _ownColliders.Add(own);
        }
        _hitFlash = HitFlash.GetOrAdd(gameObject);

        Hurtbox.Find(this);

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
        StepSeparation(Time.fixedDeltaTime);
    }

    protected virtual void OnDisable() => RestorePlayerCollision();

    // Attack movement: never ends inside a wall and never shoves the player.
    // Returns where the enemy actually ended up.
    public Vector2 SetGroundPosition(Vector2 position)
    {
        BeginPhasing();

        // Transforms moved this frame aren't in the physics scene until synced
        Physics2D.SyncTransforms();
        ResolveWallOverlap();

        var from = Position;
        var delta = position - from;
        var distance = delta.magnitude;

        if (distance > 0.0001f) {
            var direction = delta / distance;
            position = from + direction * Mathf.Min(distance, FreeDistance(direction, distance));
        } else {
            position = from;
        }

        Teleport(position);
        return position;
    }

    // Called by EnemyAI once a move is over; collisions come back once clear of the player
    public void EndAttackMovement()
    {
        if (!_phasing)
            return;

        _phasing = false;
        _separating = true;
    }

    private void BeginPhasing()
    {
        _separating = false;

        if (_phasing || !phaseThroughPlayer)
            return;

        _phasing = true;

        var player = PlayerHealth.Current;
        if (player == null || _ignoredPlayer.Count > 0)
            return;

        foreach (var other in player.GetComponentsInChildren<Collider2D>()) {
            if (other.isTrigger)
                continue;

            foreach (var own in _ownColliders)
                Physics2D.IgnoreCollision(own, other, true);

            _ignoredPlayer.Add(other);
        }
    }

    // Backs off the player (not the other way round), then turns collisions back on
    private void StepSeparation(float deltaTime)
    {
        if (!_separating)
            return;

        if (_ignoredPlayer.Count == 0 || _collider == null) {
            _separating = false;
            return;
        }

        var push = Vector2.zero;
        var overlapping = false;

        foreach (var other in _ignoredPlayer) {
            if (other == null || !other.enabled)
                continue;

            var gap = _collider.Distance(other);
            if (!gap.isValid || !gap.isOverlapped)
                continue;

            overlapping = true;
            push += gap.normal * gap.distance;
        }

        if (!overlapping) {
            RestorePlayerCollision();
            return;
        }

        if (push.sqrMagnitude <= 0.000001f)
            push = Vector2.up * -0.01f;

        var step = Mathf.Min(push.magnitude + wallSkin, separationSpeed * deltaTime);
        var direction = push.normalized;
        var travel = Mathf.Min(step, FreeDistance(direction, step));

        // Pinned against a wall: stay phased so the player can simply walk off
        if (travel > 0f)
            MoveTo(Position + direction * travel);
    }

    private void RestorePlayerCollision()
    {
        _separating = false;
        _phasing = false;

        foreach (var other in _ignoredPlayer) {
            if (other == null)
                continue;

            foreach (var own in _ownColliders) {
                if (own != null)
                    Physics2D.IgnoreCollision(own, other, false);
            }
        }

        _ignoredPlayer.Clear();
    }

    // Pushes the enemy back out if it is already inside a wall (e.g. the maze shifted)
    private void ResolveWallOverlap()
    {
        if (_collider == null || wallLayers.value == 0)
            return;

        for (var pass = 0; pass < 3; pass++) {
            var count = _collider.Overlap(_wallFilter, _overlapHits);
            var push = Vector2.zero;

            for (var i = 0; i < count; i++) {
                var wall = _overlapHits[i];
                if (wall == null || wall.transform.IsChildOf(transform))
                    continue;

                var gap = _collider.Distance(wall);
                if (gap.isValid && gap.isOverlapped)
                    push += gap.normal * (gap.distance - wallSkin);
            }

            if (push.sqrMagnitude <= 0.000001f)
                return;

            Teleport(Position + push);
            Physics2D.SyncTransforms();
        }
    }

    private void Teleport(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if (Body != null && Body.bodyType != RigidbodyType2D.Static)
            Body.position = position;
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

        if (IsAlive)
            Sfx.PlayOneShot(hurtSound, Position);
        else
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

        Sfx.PlayOneShot(deathSound, Position);

        Died?.Invoke();

        if (dropTable != null)
            dropTable.Drop(Position);

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
