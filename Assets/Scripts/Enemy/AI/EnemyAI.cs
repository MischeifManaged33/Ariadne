using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Generalized AI for enemies, handles states and attack
[RequireComponent(typeof(EnemyMover))]
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    public enum State
    {
        Idle,
        Wander,
        Chase,
        Attack
    }

    public enum AlertBehaviour
    {
        Chase,
        Wander,
        Hold
    }

    [Header("Target")]
    [SerializeField]
    private Transform target;
    [SerializeField]
    private bool findPlayerOnStart = true;

    [Header("Line of Vision")]
    [SerializeField, Min(0f)]
    private float sightRange = 7f;
    [SerializeField, Min(0f)]
    private float loseSightRange = 10f;
    [SerializeField]
    private bool requireLineOfSight = true;
    [SerializeField]
    private LayerMask sightBlockers;
    [SerializeField, Min(0f)]
    private float alertMemory = 2.5f;
    [SerializeField, Min(0.02f)]
    private float scanInterval = 0.15f;
    [SerializeField, Min(0f)]
    private float reactionDelay = 0.25f;

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

    [Header("Engage")]
    [SerializeField]
    private AlertBehaviour none = AlertBehaviour.Chase;
    [SerializeField, Min(0f)]
    private float chaseStopDistance = 1.2f;
    [SerializeField, Range(0.1f, 3f)]
    private float chaseSpeedMultiplier = 1f;

    [Header("Performance")]
    [SerializeField, Min(0f)]
    private float thinkInterval = 0.1f;
    [SerializeField, Min(0f)]
    private float sleepDistance = 25f;

    public event Action<State, State> StateChanged;

    public EnemyMover Mover { get; private set; }
    public State CurrentState { get; private set; } = State.Idle;
    public EnemyAbility CurrentAbility { get; private set; }
    public bool IsBusy => _abilityRoutine != null;

    public Transform Target => target;
    public bool CanSeeTarget { get; private set; }
    public bool IsAlerted => Time.time < _alertUntil;

    public Vector2 Facing { get; private set; } = Vector2.down;
    public Vector2 Home => homeAnchor != null ? (Vector2)homeAnchor.position : _home;

    public Vector2 Position => Mover != null ? Mover.Position : (Vector2)transform.position;
    public Vector2 TargetPosition => target != null ? (Vector2)target.position : Position;
    public float YScale => Mover != null ? Mover.YScale : Isometric.DefaultYScale;

    public float DistanceToTarget =>
        target != null ? Isometric.GroundDistance(Position, TargetPosition, YScale) : Mathf.Infinity;

    public Vector2 DirectionToTarget =>
        target != null ? Isometric.GroundDirection(Position, TargetPosition, YScale) : Vector2.zero;

    private readonly List<EnemyAbility> _usable = new();
    private readonly List<float> _weights = new();
    private EnemyAbility[] _abilities;
    private IDamagable _health;
    private Enemy _enemy;
    private Coroutine _abilityRoutine;

    private Vector2 _home;
    private Vector2 _wanderPoint;
    private float _wanderPauseUntil;
    private float _wanderLegDeadline;
    private bool _hasWanderPoint;

    private float _alertUntil;
    private float _nextScanTime;
    private float _nextThinkTime;
    private float _actReadyTime;

    private void Reset()
    {
        sightBlockers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        Mover = GetComponent<EnemyMover>();
        _health = GetComponent<IDamagable>();
        _enemy = GetComponent<Enemy>();
        _abilities = GetComponentsInChildren<EnemyAbility>(true);

        if (sightBlockers.value == 0)
            sightBlockers = LayerMask.GetMask("Walls");

        _home = transform.position;
    }

    private void Start()
    {
        if (target == null && findPlayerOnStart) {
            var player = PlayerHealth.Current != null ? PlayerHealth.Current : FindAnyObjectByType<PlayerHealth>();
            if (player != null)
                target = player.transform;
        }

        _nextThinkTime = Time.time + UnityEngine.Random.Range(0f, thinkInterval);

        SetState(State.Wander);
    }

    private void OnEnable()
    {
        if (_enemy != null)
            _enemy.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (_enemy != null)
            _enemy.Damaged -= OnDamaged;

        CancelAbility();
    }

    private void OnDamaged(float amount) => Alert();

    private void Update()
    {
        if (Time.time < _nextThinkTime)
            return;

        _nextThinkTime = Time.time + thinkInterval;

        if (_health != null && !_health.IsAlive) {
            CancelAbility();
            Mover.Stop();
            return;
        }

        if (IsDormant()) {
            if (Mover.IsMoving)
                Mover.Stop();

            return;
        }

        Scan();
        TrackFacing();

        if (IsBusy)
            return;

        Think();
    }

    private bool IsDormant()
    {
        if (sleepDistance <= 0f || target == null || IsBusy || IsAlerted)
            return false;

        return DistanceToTarget > sleepDistance;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void FaceTowards(Vector2 groundDirection)
    {
        if (groundDirection.sqrMagnitude <= 0.0001f)
            return;

        Facing = groundDirection.normalized;
    }

    public void Alert()
    {
        _alertUntil = Mathf.Max(_alertUntil, Time.time + Mathf.Max(alertMemory, 0.1f));
    }

    public void CancelAbility()
    {
        if (_abilityRoutine != null) {
            StopCoroutine(_abilityRoutine);
            _abilityRoutine = null;
        }

        if (CurrentAbility != null) {
            CurrentAbility.Cancel();
            CurrentAbility.BeginCooldown();
            CurrentAbility = null;
        }
    }

    private void Think()
    {
        if (!IsAlerted) {
            SetState(State.Wander);
            WanderStep();
            return;
        }

        if (Time.time < _actReadyTime) {
            Mover.Stop();
            FaceTowards(DirectionToTarget);
            return;
        }

        if (TryStartAbility())
            return;

        switch (none) {
            case AlertBehaviour.Chase:
                SetState(State.Chase);
                ChaseStep();
                break;

            case AlertBehaviour.Wander:
                SetState(State.Wander);
                WanderStep();
                break;

            default:
                SetState(State.Idle);
                Mover.Stop();
                FaceTowards(DirectionToTarget);
                break;
        }
    }

    private void Scan()
    {
        if (Time.time < _nextScanTime)
            return;

        _nextScanTime = Time.time + scanInterval;

        var wasAlerted = IsAlerted;
        CanSeeTarget = LookForTarget();

        if (!CanSeeTarget)
            return;

        _alertUntil = Time.time + alertMemory;

        if (!wasAlerted)
            _actReadyTime = Time.time + reactionDelay;
    }

    private bool LookForTarget()
    {
        if (target == null)
            return false;

        var range = IsAlerted ? Mathf.Max(sightRange, loseSightRange) : sightRange;
        if (DistanceToTarget > range)
            return false;

        if (!requireLineOfSight || sightBlockers.value == 0)
            return true;

        return Physics2D.Linecast(Position, TargetPosition, sightBlockers).collider == null;
    }

    private void WanderStep()
    {
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
        var fromHome = Isometric.GroundDistance(home, position, YScale);

        for (var attempt = 0; attempt < wanderAttempts; attempt++) {
            var direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            var ground = direction.normalized * UnityEngine.Random.Range(wanderStep * 0.35f, wanderStep);
            var candidate = position + Isometric.ToScreen(ground, YScale);
            var candidateFromHome = Isometric.GroundDistance(home, candidate, YScale);

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

    private void ChaseStep()
    {
        if (target == null) {
            Mover.Stop();
            return;
        }

        FaceTowards(DirectionToTarget);

        if (DistanceToTarget <= chaseStopDistance) {
            Mover.Stop();
            return;
        }

        Mover.SetDestination(TargetPosition, chaseSpeedMultiplier);
    }

    private bool TryStartAbility()
    {
        var ability = SelectAbility();
        if (ability == null)
            return false;

        Mover.Stop();
        _hasWanderPoint = false;

        SetState(State.Attack);

        CurrentAbility = ability;
        _abilityRoutine = StartCoroutine(RunAbility(ability));

        return true;
    }

    private IEnumerator RunAbility(EnemyAbility ability)
    {
        yield return ability.Execute(this);

        ability.BeginCooldown();
        CurrentAbility = null;
        _abilityRoutine = null;
    }

    private EnemyAbility SelectAbility()
    {
        if (_abilities == null || _abilities.Length == 0 || target == null)
            return null;

        _usable.Clear();
        _weights.Clear();

        var distance = DistanceToTarget;
        var total = 0f;

        foreach (var ability in _abilities) {
            if (ability == null || !ability.isActiveAndEnabled)
                continue;

            var weight = ability.GetSelectionWeight(this, distance);
            if (weight <= 0f || !ability.CanUse(this, distance))
                continue;

            _usable.Add(ability);
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

    private void TrackFacing()
    {
        if (Mover != null && Mover.IsMoving)
            FaceTowards(Mover.Facing);
    }

    private void SetState(State next)
    {
        if (CurrentState == next)
            return;

        var previous = CurrentState;
        CurrentState = next;

        StateChanged?.Invoke(previous, next);
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

        var yScale = Mover != null ? Mover.YScale : Isometric.DefaultYScale;
        var previous = centre + new Vector2(radius, 0f);

        for (var i = 1; i <= 32; i++) {
            var radians = Mathf.PI * 2f * i / 32f;
            var point = centre + new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius * yScale);

            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
