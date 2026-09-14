using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(BossPathfinder))]
public class Minotaur : MonoBehaviour, IDamagable
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 1000f;

    [Header("Phases")]
    [SerializeField]
    private BossPhase[] phases = {
        new BossPhase(1f, 1.5f, 1f)
    };

    [Header("Targeting")]
    [SerializeField]
    private Transform target;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;
    [SerializeField, Min(0f)]
    private float startDelay = 1f;

    [Header("Idle")]
    [SerializeField, Min(0f)]
    private float idleMinDistance = 2.5f;
    [SerializeField, Min(0f)]
    private float idleMaxDistance = 6f;
    [SerializeField, Range(0.1f, 3f)]
    private float idleSpeedMultiplier = 0.8f;
    [SerializeField]
    private bool idleStrafe = true;
    [SerializeField, Min(0.1f)]
    private float idleStrafeInterval = 1.6f;
    [SerializeField, Min(0.1f)]
    private float idleStrafeReach = 1.5f;

    [Header("Death")]
    [SerializeField]
    private bool destroyOnDeath = true;
    [SerializeField, Min(0f)]
    private float destroyDelay = 0f;

    [Header("Events")]
    [SerializeField]
    private UnityEvent<float> onDamaged;
    [SerializeField]
    private UnityEvent<int> onPhaseChanged;
    [SerializeField]
    private UnityEvent onDied;

    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action<int> PhaseChanged;
    public event Action Died;

    // IDamagable
    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    public int PhaseIndex { get; private set; }
    public BossPhase CurrentPhase => phases[Mathf.Clamp(PhaseIndex, 0, phases.Length - 1)];
    public BossMove CurrentMove { get; private set; }
    public bool IsTransitioning { get; private set; }
    public Transform Target => target;
    public BossPathfinder Mover { get; private set; }
    public float YScale => isometricYScale;
    public Vector2 Position => transform.position;
    public Vector2 Facing { get; private set; } = Vector2.down;

    public Vector2 TargetPosition => target != null ? (Vector2)target.position : Position;

    // Ranges are ground space, so a target above or below reads as far as one beside
    public float DistanceToTarget =>
        target != null ? Isometric.GroundDistance(Position, TargetPosition, isometricYScale) : Mathf.Infinity;

    public Vector2 DirectionToTarget =>
        target != null ? Isometric.GroundDirection(Position, TargetPosition, isometricYScale) : Vector2.zero;

    private readonly List<BossMove> _usable = new();
    private readonly List<float> _weights = new();
    private BossMove[] _moves;
    private HitFlash _hitFlash;
    private Coroutine _fightRoutine;
    private Coroutine _moveRoutine;
    private int _pendingPhase;
    private float _strafeFlipTime;
    private float _strafeSign = 1f;
    private bool _moveActive;
    private bool _dead;

    private void Awake()
    {
        if (phases == null || phases.Length == 0)
            phases = new[] { new BossPhase() };

        _moves = GetComponentsInChildren<BossMove>(true);
        _hitFlash = HitFlash.GetOrAdd(gameObject);
        Mover = GetComponent<BossPathfinder>();

        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        if (target == null) {
            var player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
                target = player.transform;
        }

        if (target == null)
            Debug.LogWarning($"No target found");

        RaiseHealthChanged();
        onPhaseChanged?.Invoke(PhaseIndex);
        PhaseChanged?.Invoke(PhaseIndex);

        _fightRoutine = StartCoroutine(RunFight());
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= applied;

        _hitFlash.Flash();

        onDamaged?.Invoke(applied);
        Damaged?.Invoke(applied);
        RaiseHealthChanged();

        var gated = ResolvePhase(Normalized);
        if (gated > _pendingPhase)
            _pendingPhase = gated;

        if (!IsAlive)
            Die();

        return applied;
    }

    public void FaceTowards(Vector2 groundDirection)
    {
        if (groundDirection.sqrMagnitude > 0.0001f)
            Facing = groundDirection.normalized;
    }

    public float Heal(float amount)
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

    // transition when gated, otherwise pick a move to run
    private IEnumerator RunFight()
    {
        yield return new WaitForSeconds(startDelay);

        while (IsAlive) {
            if (_pendingPhase != PhaseIndex) {
                yield return EnterPhase(_pendingPhase);
                continue;
            }

            var move = SelectMove();
            if (move == null) {
                // Nothing is usable yet, so shuffle around instead of standing there
                IdleStep();
                yield return null;
                continue;
            }

            yield return RunMove(move);
            yield return Idle(CurrentPhase.MoveInterval);
        }
    }

    // Holds the preferred distance between moves, walking in or backing off as the target moves
    private IEnumerator Idle(float duration)
    {
        var deadline = Time.time + Mathf.Max(0f, duration);

        while (Time.time < deadline) {
            IdleStep();
            yield return null;
        }

        if (Mover != null)
            Mover.Stop();
    }

    private void IdleStep()
    {
        if (Mover == null || target == null || IsTransitioning)
            return;

        var direction = DirectionToTarget;
        if (direction == Vector2.zero) {
            Mover.Stop();
            return;
        }

        FaceTowards(direction);

        var distance = DistanceToTarget;

        if (distance > idleMaxDistance) {
            Mover.SetDestination(TargetPosition, idleSpeedMultiplier);
            return;
        }

        if (distance < idleMinDistance) {
            // Back off to the near edge of the band, staying on the side it is already on
            var retreat = TargetPosition + Isometric.ToScreen(-direction * idleMinDistance, isometricYScale);
            Mover.SetDestination(retreat, idleSpeedMultiplier);
            return;
        }

        if (!idleStrafe) {
            Mover.Stop();
            return;
        }

        if (Time.time >= _strafeFlipTime) {
            _strafeFlipTime = Time.time + idleStrafeInterval;
            _strafeSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        var sideways = new Vector2(-direction.y, direction.x) * (_strafeSign * idleStrafeReach);
        var destination = Position + Isometric.ToScreen(sideways, isometricYScale);

        // A shuffle is not worth pathing around a wall for, so turn around instead
        if (!Mover.HasClearWalk(destination)) {
            _strafeSign = -_strafeSign;
            _strafeFlipTime = Time.time + idleStrafeInterval;
            destination = Position - Isometric.ToScreen(sideways, isometricYScale);
        }

        if (Mover.HasClearWalk(destination))
            Mover.SetDestination(destination, idleSpeedMultiplier);
        else
            Mover.Stop();
    }

    private IEnumerator RunMove(BossMove move)
    {
        // Idle walking stops here, a move steers itself from now on
        if (Mover != null)
            Mover.Stop();

        CurrentMove = move;
        _moveActive = true;
        _moveRoutine = StartCoroutine(MoveRoutine(move));

        while (_moveActive) {
            if (_pendingPhase != PhaseIndex) {
                CancelMove();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator MoveRoutine(BossMove move)
    {
        yield return move.Execute(this);

        move.BeginCooldown();
        CurrentMove = null;
        _moveRoutine = null;
        _moveActive = false;
    }

    private void CancelMove()
    {
        _moveActive = false;

        if (_moveRoutine != null) {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        if (Mover != null)
            Mover.Stop();

        if (CurrentMove != null) {
            CurrentMove.Cancel();
            CurrentMove.BeginCooldown();
            CurrentMove = null;
        }
    }

    private IEnumerator EnterPhase(int index)
    {
        if (Mover != null)
            Mover.Stop();

        PhaseIndex = Mathf.Clamp(index, 0, phases.Length - 1);
        IsTransitioning = true;

        onPhaseChanged?.Invoke(PhaseIndex);
        PhaseChanged?.Invoke(PhaseIndex);

        yield return new WaitForSeconds(CurrentPhase.TransitionDuration);

        IsTransitioning = false;
    }

    // Uses the classic weighted random selection move system
    // Could be smarter
    private BossMove SelectMove()
    {
        _usable.Clear();
        _weights.Clear();

        var distance = DistanceToTarget;
        var total = 0f;

        foreach (var move in _moves) {
            if (move == null || !move.isActiveAndEnabled)
                continue;

            var weight = move.GetSelectionWeight(this, PhaseIndex, distance);
            if (weight <= 0f || !move.CanUse(this, distance))
                continue;

            _usable.Add(move);
            _weights.Add(weight);
            total += weight;
        }

        if (total <= 0f)
            return null;

        var roll = UnityEngine.Random.value * total;
        for (var i = 0; i < _usable.Count; i++) {
            roll -= _weights[i];
            if (roll <= 0f)
                return _usable[i];
        }

        return _usable[_usable.Count - 1];
    }

    private int ResolvePhase(float normalized)
    {
        var index = 0;

        for (var i = 0; i < phases.Length; i++) {
            if (normalized <= phases[i].HealthThreshold)
                index = i;
        }

        return index;
    }

    private void Die()
    {
        if (_dead)
            return;

        _dead = true;

        CancelMove();

        if (_fightRoutine != null) {
            StopCoroutine(_fightRoutine);
            _fightRoutine = null;
        }

        onDied?.Invoke();
        Died?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private void RaiseHealthChanged() => HealthChanged?.Invoke(CurrentHealth, maxHealth);

    private void OnValidate()
    {
        if (Application.isPlaying)
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
    }
}
