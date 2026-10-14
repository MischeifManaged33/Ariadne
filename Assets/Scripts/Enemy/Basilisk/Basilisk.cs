using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(EnemyMover))]
public class Basilisk : Enemy
{
    public enum State
    {
        Patrol,
        Petrifying,
        Recovering,
        Returning,
        TailSwipe,
        TurnAround
    }

    public enum RouteMode
    {
        Loop,
        PingPong
    }

    [Header("Patrol")]
    [SerializeField]
    private Transform[] waypoints;
    [SerializeField]
    private RouteMode routeMode = RouteMode.Loop;
    [SerializeField, Range(0.1f, 3f)]
    private float patrolSpeedMultiplier = 0.6f;
    [SerializeField, Min(0f)]
    private float waypointPause = 0.6f;
    [SerializeField, Min(0.05f)]
    private float arriveDistance = 0.2f;
    [SerializeField, Min(0f)]
    private float stuckGrace = 3f;
    [SerializeField, Min(0f)]
    private float turnSpeed = 270f;
    [SerializeField, Range(0f, 180f)]
    private float turnInPlaceAngle = 45f;
    [SerializeField, Min(0f)]
    private float sleepDistance = 25f;

    [Header("Gaze")]
    [SerializeField]
    private BasiliskGaze gaze;
    [SerializeField]
    private Transform eye;
    [SerializeField, Min(0f)]
    private float exposureTime = 0.25f;

    [Header("Peck")]
    [SerializeField, Min(0f)]
    private float peckDamage = 20f;
    [SerializeField, Min(0.1f)]
    private float peckReach = 0.9f;
    [SerializeField, Range(0.1f, 3f)]
    private float approachSpeedMultiplier = 1.2f;
    [SerializeField, Min(0f)]
    private float approachTimeout = 4f;
    [SerializeField, Min(0f)]
    private float peckWindup = 0.5f;
    [SerializeField, Min(0f)]
    private float recovery = 2f;
    [SerializeField, Min(0.5f)]
    private float maxPetrifyTime = 6f;
    [SerializeField]
    private AttackSounds peckSounds = new AttackSounds();

    [Header("When Struck")]
    [SerializeField, Min(0f)]
    private float tailSwipeWeight = 1f;
    [SerializeField, Min(0f)]
    private float turnAroundWeight = 1f;
    [SerializeField, Min(0f)]
    private float reactionCooldown = 1.5f;

    [Header("Tail Swipe")]
    [SerializeField, Min(0f)]
    private float tailDamage = 12f;
    [SerializeField, Min(0.1f)]
    private float tailReach = 2.2f;
    [SerializeField, Range(10f, 360f)]
    private float tailArc = 150f;
    [SerializeField, Min(0f)]
    private float tailWindup = 0.6f;
    [SerializeField, Min(0f)]
    private float tailRecovery = 0.4f;
    [SerializeField]
    private AttackIndicator tailIndicator;
    [SerializeField]
    private string indicatorSortingLayer = "Enemy";
    [SerializeField]
    private AttackSounds tailSounds = new AttackSounds();

    [Header("Turn Around")]
    [SerializeField, Min(0f)]
    private float turnDelay = 1f;
    [SerializeField, Min(1f)]
    private float turnAroundSpeed = 180f;

    [Header("Targets")]
    [SerializeField]
    private LayerMask targetLayers;

    [Header("Visual")]
    [SerializeField]
    private SpriteRenderer visual;
    [SerializeField]
    private bool artFacesLeft = true;

    public event Action<State> StateChanged;

    public State CurrentState { get; private set; } = State.Patrol;
    public Vector2 Facing { get; private set; } = Vector2.left;

    private Vector2 Position => Body != null ? Body.position : (Vector2)transform.position;
    private Vector2 EyePosition => eye != null ? (Vector2)eye.position : (Vector2)transform.position;
    private float YScale => _mover.YScale;

    private bool Gazing => CurrentState is State.Patrol or State.Returning or State.TailSwipe or State.TurnAround;

    private EnemyMover _mover;
    private Coroutine _routine;

    private Vector2[] _route = Array.Empty<Vector2>();
    private int _index;
    private int _step = 1;
    private float _pauseUntil;
    private float _legDeadline = -1f;

    private Vector2 _desiredFacing = Vector2.left;
    private Vector2 _returnPoint;
    private bool _hasReturnPoint;
    private float _returnDeadline = -1f;
    private float _eyeX;
    private bool _artFlipped;
    private float _nextReactionTime;
    private float _exposure;

    private PlayerHealth _target;
    private Collider2D _targetCollider;
    private Petrification _stone;
    private float _nextSearchTime;

    protected override void Reset()
    {
        base.Reset();

        targetLayers = LayerMask.GetMask("Player");
    }

    protected override void Awake()
    {
        base.Awake();

        _mover = GetComponent<EnemyMover>();

        if (targetLayers.value == 0)
            targetLayers = LayerMask.GetMask("Player");

        foreach (var ai in GetComponents<EnemyAI>()) {
            ai.enabled = false;
            Destroy(ai);
        }

        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null)
            _artFlipped = visual.flipX;
        if (eye != null)
            _eyeX = eye.localPosition.x;

        Facing = artFacesLeft ? Vector2.left : Vector2.right;
        _desiredFacing = Facing;

        ResolveGaze();
    }

    protected override void Start()
    {
        base.Start();

        CacheRoute();
    }

    private void OnEnable()
    {
        Damaged += OnStruck;
        _mover.FacingChanged += OnMoverFacing;
    }

    private void OnDisable()
    {
        Damaged -= OnStruck;
        _mover.FacingChanged -= OnMoverFacing;

        StopRoutine();
        ReleaseTarget();
    }

    private void Update()
    {
        if (!IsAlive) {
            _mover.Stop();
            gaze.Active = false;
            return;
        }

        FindTarget();

        if (IsDormant()) {
            _mover.Stop();
            gaze.Active = false;
            return;
        }

        switch (CurrentState) {
            case State.Patrol:
                PatrolStep();
                break;

            case State.Returning:
                ReturnStep();
                break;
        }

        TurnTowardsDesired();
        UpdateGaze();
        UpdateVisual();
    }

    // Patrol

    private void CacheRoute()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        var points = new List<Vector2>(waypoints.Length);
        foreach (var waypoint in waypoints) {
            if (waypoint != null)
                points.Add(waypoint.position);
        }

        _route = points.ToArray();
        _index = 0;
        _legDeadline = -1f;

        if (_route.Length > 0)
            LookAt(_route[_index]);
    }

    private void PatrolStep()
    {
        if (_route.Length == 0 || Time.time < _pauseUntil) {
            _mover.Stop();
            return;
        }

        var point = _route[_index];

        if (_legDeadline < 0f)
            _legDeadline = Deadline(point);

        if (_mover.GroundDistanceTo(point) <= arriveDistance || Time.time >= _legDeadline) {
            _mover.Stop();
            Advance();

            _pauseUntil = Time.time + waypointPause;
            _legDeadline = -1f;

            LookAt(_route[_index]);
            return;
        }

        WalkTo(point);
    }

    private void ReturnStep()
    {
        if (_hasReturnPoint && _returnDeadline < 0f)
            _returnDeadline = Deadline(_returnPoint);

        if (!_hasReturnPoint || _mover.GroundDistanceTo(_returnPoint) <= arriveDistance ||
            Time.time >= _returnDeadline) {
            _hasReturnPoint = false;
            _mover.Stop();
            _legDeadline = -1f;

            SetState(State.Patrol);
            return;
        }

        WalkTo(_returnPoint);
    }

    private float Deadline(Vector2 point)
    {
        var speed = Mathf.Max(0.01f, _mover.Speed * patrolSpeedMultiplier);

        return Time.time + Vector2.Distance(Position, point) / speed * 1.5f + stuckGrace;
    }

    private void WalkTo(Vector2 point)
    {
        if (!_mover.IsMoving) {
            var heading = Isometric.GroundDirection(Position, point, YScale);

            if (heading != Vector2.zero && Vector2.Angle(Facing, heading) > turnInPlaceAngle) {
                _desiredFacing = heading;
                return;
            }
        }

        _mover.SetDestination(point, patrolSpeedMultiplier);
    }

    private void Advance()
    {
        var count = _route.Length;
        if (count < 2)
            return;

        if (routeMode == RouteMode.Loop) {
            _index = ((_index + _step) % count + count) % count;
            return;
        }

        var next = _index + _step;
        if (next < 0 || next >= count) {
            _step = -_step;
            next = _index + _step;
        }

        _index = next;
    }

    private void ReverseRoute()
    {
        _step = -_step;
        Advance();

        _legDeadline = -1f;
    }

    // Facing

    private void OnMoverFacing(Vector2 groundDirection)
    {
        if (CurrentState != State.TurnAround)
            _desiredFacing = groundDirection;
    }

    private void LookAt(Vector2 point)
    {
        var direction = Isometric.GroundDirection(Position, point, YScale);
        if (direction != Vector2.zero)
            _desiredFacing = direction;
    }

    private void TurnTowardsDesired()
    {
        if (turnSpeed <= 0f) {
            Facing = _desiredFacing;
            return;
        }

        var angle = Vector2.SignedAngle(Facing, _desiredFacing);
        var step = Mathf.Clamp(angle, -turnSpeed * Time.deltaTime, turnSpeed * Time.deltaTime);

        Facing = Rotate(Facing, step);
    }

    private void UpdateVisual()
    {
        if (visual == null || Mathf.Abs(Facing.x) < 0.1f)
            return;

        var turned = artFacesLeft ? Facing.x > 0f : Facing.x < 0f;
        visual.flipX = turned != _artFlipped;

        if (eye != null && eye.parent != null) {
            var local = eye.localPosition;
            local.x = turned ? -_eyeX : _eyeX;
            eye.localPosition = local;
        }
    }

    // Gaze

    private void UpdateGaze()
    {
        gaze.Aim(EyePosition, Facing);

        if (CurrentState == State.Petrifying) {
            gaze.Active = true;
            gaze.Charge = 1f;
            return;
        }

        gaze.Active = Gazing;

        var exposed = Gazing && _target != null && !_stone.IsPetrified && TargetInGaze();

        _exposure = exposed
            ? _exposure + Time.deltaTime
            : Mathf.Max(0f, _exposure - Time.deltaTime * 2f);

        gaze.Charge = exposureTime > 0f ? _exposure / exposureTime : (exposed ? 1f : 0f);

        if (exposed && _exposure >= exposureTime)
            BeginPetrify();
    }

    private bool TargetInGaze()
    {
        var point = (Vector2)_target.transform.position;
        if (gaze.Contains(point))
            return true;

        return _targetCollider != null && gaze.Contains(_targetCollider.ClosestPoint(gaze.Origin));
    }

    private void BeginPetrify()
    {
        _exposure = 0f;

        if (!_stone.TryPetrify(this, maxPetrifyTime))
            return;

        if (!_hasReturnPoint) {
            _returnPoint = Position;
            _hasReturnPoint = true;
        }

        Run(PeckRoutine());
    }

    private IEnumerator PeckRoutine()
    {
        SetState(State.Petrifying);

        var deadline = Time.time + approachTimeout;

        while (Time.time < deadline && _target != null) {
            var point = TargetPoint();

            LookAt(point);

            if (_mover.GroundDistanceTo(point) <= peckReach * 0.8f)
                break;

            _mover.SetDestination(point, approachSpeedMultiplier);
            yield return null;
        }

        _mover.Stop();

        peckSounds.PlayWindup(Position);

        for (var elapsed = 0f; elapsed < peckWindup; elapsed += Time.deltaTime) {
            if (_target != null)
                LookAt(TargetPoint());

            yield return null;
        }

        Peck();
        ReleaseTarget();

        SetState(State.Recovering);
        yield return new WaitForSeconds(recovery);

        _returnDeadline = -1f;
        SetState(State.Returning);

        _routine = null;
    }

    private void Peck()
    {
        peckSounds.PlaySwing(Position);

        if (_target == null)
            return;

        var point = TargetPoint();
        if (_mover.GroundDistanceTo(point) > peckReach)
            return;

        _target.TakeDamage(peckDamage);
        peckSounds.PlayImpact(point);
    }

    // Reactions

    private void OnStruck(float amount)
    {
        if (!IsAlive || Time.time < _nextReactionTime)
            return;
        if (CurrentState is State.Petrifying or State.TailSwipe or State.TurnAround)
            return;

        var total = tailSwipeWeight + turnAroundWeight;
        if (total <= 0f)
            return;

        _nextReactionTime = Time.time + reactionCooldown;

        if (UnityEngine.Random.value * total < tailSwipeWeight)
            Run(TailSwipeRoutine());
        else
            Run(TurnAroundRoutine());
    }

    private IEnumerator TailSwipeRoutine()
    {
        SetState(State.TailSwipe);
        _mover.Stop();

        var indicator = ResolveIndicator();
        var direction = -Facing;

        tailSounds.PlayWindup(Position);

        for (var elapsed = 0f; elapsed < tailWindup; elapsed += Time.deltaTime) {
            indicator.Aim(transform.position, direction, tailReach, tailArc, YScale);
            indicator.SetReady(true);
            indicator.SetVisible(true);

            yield return null;
        }

        TailStrike(direction);

        indicator.Aim(transform.position, direction, tailReach, tailArc, YScale);
        indicator.Flash();
        indicator.SetVisible(false);

        yield return new WaitForSeconds(tailRecovery);

        Resume();
    }

    private void TailStrike(Vector2 groundDirection)
    {
        var origin = Position;
        var half = tailArc * 0.5f;

        tailSounds.PlaySwing(origin);

        var candidates = Physics2D.OverlapCircleAll(origin, tailReach, targetLayers);

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            var contact = candidate.ClosestPoint(origin);
            var relative = Isometric.ToGround(contact - origin, YScale);

            if (relative.sqrMagnitude > tailReach * tailReach)
                continue;
            if (relative.sqrMagnitude > 0.0001f && Vector2.Angle(relative, groundDirection) > half)
                continue;

            health.TakeDamage(tailDamage);
            tailSounds.PlayImpact(contact);

            return;
        }
    }

    private IEnumerator TurnAroundRoutine()
    {
        SetState(State.TurnAround);
        _mover.Stop();

        // Gives the player a moment to back off before the cone comes round
        yield return new WaitForSeconds(turnDelay);

        ReverseRoute();

        var start = Facing;
        var side = 1f;
        if (_target != null)
            side = Vector2.SignedAngle(start, Isometric.GroundDirection(Position, TargetPoint(), YScale)) >= 0f
                ? 1f : -1f;

        for (var turned = 0f; turned < 180f; turned += turnAroundSpeed * Time.deltaTime) {
            Facing = Rotate(start, side * turned);
            _desiredFacing = Facing;

            yield return null;
        }

        Facing = -start;
        _desiredFacing = Facing;

        Resume();
    }

    private void Resume()
    {
        _routine = null;
        _legDeadline = -1f;

        if (_hasReturnPoint) {
            _returnDeadline = -1f;
            SetState(State.Returning);
        }
        else {
            SetState(State.Patrol);
        }
    }

    // Plumbing

    private void Run(IEnumerator routine)
    {
        StopRoutine();
        _routine = StartCoroutine(routine);
    }

    private void StopRoutine()
    {
        if (_routine != null) {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (tailIndicator != null)
            tailIndicator.SetVisible(false);

        _mover.Stop();
    }

    private void ReleaseTarget()
    {
        if (_stone != null)
            _stone.Release(this);
    }

    private void SetState(State next)
    {
        if (CurrentState == next)
            return;

        CurrentState = next;
        StateChanged?.Invoke(next);
    }

    private bool IsDormant()
    {
        if (sleepDistance <= 0f || _target == null || CurrentState != State.Patrol)
            return false;

        return Isometric.GroundDistance(Position, _target.transform.position, YScale) > sleepDistance;
    }

    private void FindTarget()
    {
        if (_target != null || Time.time < _nextSearchTime)
            return;

        _nextSearchTime = Time.time + 0.5f;

        _target = PlayerHealth.Current != null ? PlayerHealth.Current : FindAnyObjectByType<PlayerHealth>();
        if (_target == null)
            return;

        _stone = Petrification.GetOrAdd(_target.gameObject);
        _targetCollider = null;

        foreach (var candidate in _target.GetComponentsInChildren<Collider2D>()) {
            if (candidate.isTrigger)
                continue;

            _targetCollider = candidate;
            break;
        }
    }

    private Vector2 TargetPoint() => _target != null ? (Vector2)_target.transform.position : Position;

    protected override void Die()
    {
        StopRoutine();
        ReleaseTarget();

        if (gaze != null)
            gaze.Active = false;

        base.Die();
    }

    private void ResolveGaze()
    {
        if (gaze != null)
            return;

        var host = new GameObject("Gaze");
        host.transform.SetParent(transform, false);

        gaze = host.AddComponent<BasiliskGaze>();
    }

    private AttackIndicator ResolveIndicator()
    {
        if (tailIndicator != null)
            return tailIndicator;

        var host = new GameObject("Tail Swipe Indicator");
        host.transform.SetParent(transform, false);

        tailIndicator = host.AddComponent<AttackIndicator>();
        host.GetComponent<MeshRenderer>().sortingLayerName = indicatorSortingLayer;

        return tailIndicator;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);

        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        var yScale = Isometric.DefaultYScale;

        // Route
        if (waypoints != null && waypoints.Length > 0) {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.8f);

            for (var i = 0; i < waypoints.Length; i++) {
                if (waypoints[i] == null)
                    continue;

                Gizmos.DrawWireSphere(waypoints[i].position, 0.15f);

                var last = i == waypoints.Length - 1;
                if (last && routeMode == RouteMode.PingPong)
                    continue;

                var next = waypoints[(i + 1) % waypoints.Length];
                if (next != null)
                    Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }

        // Tail swipe reach, behind the current facing
        var origin = (Vector2)transform.position;
        var facing = Application.isPlaying ? Facing : (artFacesLeft ? Vector2.left : Vector2.right);

        DrawGroundFan(origin, -facing, tailReach, tailArc, yScale, new Color(1f, 0.4f, 0.2f, 0.6f));

        if (gaze != null)
            DrawGroundFan(eye != null ? (Vector2)eye.position : origin, facing, gaze.Range, gaze.Angle,
                gaze.YScale, new Color(0.6f, 1f, 0.4f, 0.8f));
    }

    private static void DrawGroundFan(Vector2 origin, Vector2 groundDirection, float range, float arc,
        float yScale, Color color)
    {
        Gizmos.color = color;

        var heading = Mathf.Atan2(groundDirection.y, groundDirection.x) * Mathf.Rad2Deg;
        var previous = origin;

        for (var i = 0; i <= 24; i++) {
            var radians = (heading - arc * 0.5f + arc * i / 24f) * Mathf.Deg2Rad;
            var point = origin + new Vector2(Mathf.Cos(radians) * range, Mathf.Sin(radians) * range * yScale);

            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        Gizmos.DrawLine(previous, origin);
    }
}
