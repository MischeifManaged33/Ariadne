using System;
using FMODUnity;
using UnityEngine;

// Breakable objects by hit
[RequireComponent(typeof(Collider2D))]
public class Breakable : MonoBehaviour, IDamagable, IKnockbackable
{
    [SerializeField, Min(1)]
    private int hitsToBreak = 1;
    [SerializeField]
    private EventReference breakSound;

    [Header("Shards")]
    [SerializeField]
    private Sprite[] shardSprites;
    [SerializeField, Min(0)]
    private int minShards = 6;
    [SerializeField, Min(0)]
    private int maxShards = 10;
    [SerializeField, Range(0f, 180f)]
    private float spreadAngle = 70f;
    [SerializeField, Min(0f)]
    private float minSpeed = 2f;
    [SerializeField, Min(0f)]
    private float maxSpeed = 4.5f;
    [SerializeField, Min(0f)]
    private float hopSpeed = 4f;
    [SerializeField, Min(0f)]
    private float maxSpin = 540f;
    [SerializeField, Min(0.01f)]
    private float shardLifetime = 1.2f;
    [SerializeField, Min(0f)]
    private float shardFadeDuration = 0.4f;
    [SerializeField]
    private float isometricYScale = 0.5f;

    public event Action Broken;

    // IDamagable
    public float MaxHealth => hitsToBreak;
    public float CurrentHealth { get; private set; }
    public float Normalized => hitsToBreak > 0 ? CurrentHealth / hitsToBreak : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    private HitFlash _hitFlash;
    private SpriteRenderer _spriteRenderer;
    private bool _broken;
    private bool _shardsSpawned;

    private void Awake()
    {
        _hitFlash = HitFlash.GetOrAdd(gameObject);
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        CurrentHealth = hitsToBreak;
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        CurrentHealth--;

        if (IsAlive) {
            _hitFlash.Flash();
            return 1f;
        }

        Break();
        return 1f;
    }

    public float Heal(float amount) => 0f;

    public void ApplyKnockback(Vector2 direction, float strength = 1f)
    {
        if (_broken && !_shardsSpawned)
            SpawnShards(direction);
    }

    private void Break()
    {
        _broken = true;
        Broken?.Invoke();
        Sfx.PlayOneShot(breakSound, transform.position);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_broken && !_shardsSpawned)
            SpawnShards(UnityEngine.Random.insideUnitCircle);
    }

    private void SpawnShards(Vector2 groundDirection)
    {
        _shardsSpawned = true;

        if (shardSprites == null || shardSprites.Length == 0)
            return;

        if (groundDirection.sqrMagnitude <= 0.0001f)
            groundDirection = UnityEngine.Random.insideUnitCircle;
        groundDirection.Normalize();

        var origin = (Vector2)(_spriteRenderer != null ? _spriteRenderer.bounds.center : transform.position);
        var count = Mathf.Min(UnityEngine.Random.Range(minShards, Mathf.Max(minShards, maxShards) + 1), shardSprites.Length);

        var pool = (Sprite[])shardSprites.Clone();
        for (var i = 0; i < count; i++) {
            var pick = UnityEngine.Random.Range(i, pool.Length);
            (pool[i], pool[pick]) = (pool[pick], pool[i]);

            if (pool[i] == null)
                continue;

            var angle = UnityEngine.Random.Range(-spreadAngle, spreadAngle) * 0.5f;
            var ground = (Vector2)(Quaternion.Euler(0f, 0f, angle) * groundDirection);
            var screen = Isometric.ToScreen(ground, isometricYScale).normalized;
            var velocity = screen * UnityEngine.Random.Range(minSpeed, maxSpeed);
            var hop = hopSpeed * UnityEngine.Random.Range(0.6f, 1.2f);
            var spin = UnityEngine.Random.Range(-maxSpin, maxSpin);

            Shard.Spawn(pool[i], origin, velocity, hop, spin, shardLifetime, shardFadeDuration, _spriteRenderer);
        }
    }
}
