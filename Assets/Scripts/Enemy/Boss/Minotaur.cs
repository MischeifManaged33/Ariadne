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

    [Header("Line of Vision")]
    [SerializeField, Min(0f)]
    private float sightRange = 9f;
    [SerializeField]
    private LayerMask sightBlockers;
    [SerializeField, Min(0.02f)]
    private float scanInterval = 0.15f;

    [Header("Wander")]
    [SerializeField]
    private Transform homeAnchor;
    [SerializeField, Min(0f)]
    private float wanderRadius = 5f;
    [SerializeField, Min(0.1f)]
    private float wanderStep = 3f;
    [SerializeField, Min(0f)]
    private float wanderPauseMin = 0.4f;
    [SerializeField, Min(0f)]
    private float wanderPauseMax = 1.6f;
    [SerializeField, Range(0.1f, 3f)]
    private float wanderSpeedMultiplier = 0.6f;
    [SerializeField, Min(0.1f)]
    private float wanderLegTimeout = 4f;
    [SerializeField, Min(0.05f)]
    private float wanderArriveDistance = 0.35f;
    [SerializeField, Range(1, 24)]
    private int wanderAttempts = 8;

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
    private UnityEvent onAggro;
    [SerializeField]
    private UnityEvent onDied;

    public event Action Aggroed;
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

    // Latches on the first sighting and never clears, so the fight cannot be walked away from
    public bool IsAggro { get; private set; }
    public bool CanSeeTarget { get; private set; }
    public Vector2 Home => homeAnchor != null ? (Vector2)homeAnchor.position : _home;

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

    private Vector2 _home;
    private Vector2 _wanderPoint;
    private float _wanderPauseUntil;
    private float _wanderLegDeadline;
    private bool _hasWanderPoint;
    private float _nextScanTime;

    private void Reset()
    {
        sightBlockers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        if (phases == null || phases.Length == 0)
            phases = new[] { new BossPhase() };

        _moves = GetComponentsInChildren<BossMove>(true);
        _hitFlash = HitFlash.GetOrAdd(gameObject);
        Mover = GetComponent<BossPathfinder>();

        if (sightBlockers.value == 0)
            sightBlockers = LayerMask.GetMask("Walls");

        _home = transform.position;

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

    private void Update()
    {
        if (IsAlive)
            Scan();
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= applied;

        Aggro();

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
        while (IsAlive && !IsAggro) {
            WanderStep();
            yield return null;
        }

        if (!IsAlive)
            yield break;

        _hasWanderPoint = false;

        if (Mover != null)
            Mover.Stop();

        yield return new WaitForSeconds(startDelay);

        while (IsAlive) {
            if (_pendingPhase != PhaseIndex) {
                yield return EnterPhase(_pendingPhase);
                continue;
            }

            var move = SelectMove();
            if (move == null) {
                IdleStep();
                yield return null;
                continue;
            }

            yield return RunMove(move);
            yield return Idle(CurrentPhase.MoveInterval);
        }
    }

    // One way aggro
    public void Aggro()
    {
        if (IsAggro || !IsAlive)
            return;

        IsAggro = true;

        _hasWanderPoint = false;

        if (Mover != null)
            Mover.Stop();

        onAggro?.Invoke();
        Aggroed?.Invoke();
    }

    private void Scan()
    {
        if (Time.time < _nextScanTime)
            return;

        _nextScanTime = Time.time + scanInterval;

        CanSeeTarget = LookForTarget();

        if (CanSeeTarget)
            Aggro();
    }

    private bool LookForTarget()
    {
        if (target == null)
            return false;

        if (DistanceToTarget > sightRange)
            return false;

        if (sightBlockers.value == 0)
            return true;

        return Physics2D.Linecast(Position, TargetPosition, sightBlockers).collider == null;
    }

    private void WanderStep()
    {
        if (Mover == null)
            return;

        if (Time.time < _wanderPauseUntil) {
            Mover.Stop();
            return;
        }

        if (_hasWanderPoint) {
            var arrived = Mover.GroundDistanceTo(_wanderPoint) <= wanderArriveDistance;

            if (arrived || Time.time >= _wanderLegDeadline) {
                _hasWanderPoint = false;
                Mover.Stop();
                _wanderPauseUntil = Time.time + UnityEngine.Random.Range(wanderPauseMin, wanderPauseMax);
                return;
            }

            Mover.SetDestination(_wanderPoint, wanderSpeedMultiplier);
            return;
        }

        if (!PickWanderPoint(out _wanderPoint)) {
            _wanderPauseUntil = Time.time + Mathf.Max(0.1f, wanderPauseMin);
            Mover.Stop();
            return;
        }

        _hasWanderPoint = true;
        _wanderLegDeadline = Time.time + wanderLegTimeout;
    }

    private bool PickWanderPoint(out Vector2 point)
    {
        var position = Position;
        var home = Home;
        var fromHome = Isometric.GroundDistance(home, position, isometricYScale);

        for (var attempt = 0; attempt < wanderAttempts; attempt++) {
            var direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            var ground = direction.normalized * UnityEngine.Random.Range(wanderStep * 0.35f, wanderStep);
            var candidate = position + Isometric.ToScreen(ground, isometricYScale);
            var candidateFromHome = Isometric.GroundDistance(home, candidate, isometricYScale);

            // Allowed to leave the leash only while heading back towards it
            if (candidateFromHome > wanderRadius && candidateFromHome >= fromHome)
                continue;
            if (!Mover.HasClearWalk(candidate))
                continue;

            point = candidate;
            return true;
        }

        point = position;
        return false;
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

        if (wanderPauseMax < wanderPauseMin)
            wanderPauseMax = wanderPauseMin;
    }

    private void OnDrawGizmosSelected()
    {
        var position = Application.isPlaying ? Position : (Vector2)transform.position;
        var home = Application.isPlaying ? Home
            : homeAnchor != null ? (Vector2)homeAnchor.position : (Vector2)transform.position;

        DrawGroundCircle(position, sightRange, new Color(1f, 0.85f, 0.3f, 0.8f));
        DrawGroundCircle(home, wanderRadius, new Color(0.3f, 0.8f, 1f, 0.5f));
    }

    private void DrawGroundCircle(Vector2 centre, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        Gizmos.color = color;

        var previous = centre + new Vector2(radius, 0f);

        for (var i = 1; i <= 32; i++) {
            var radians = Mathf.PI * 2f * i / 32f;
            var point = centre + new Vector2(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius * isometricYScale);

            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
