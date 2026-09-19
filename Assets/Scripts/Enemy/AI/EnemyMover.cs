using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// General movement for enemies, pathfinding via A* and also moving straight.
[DisallowMultipleComponent]
public class EnemyMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float speed = 2.5f;
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;

    [Header("Pathing")]
    [SerializeField]
    private bool usePathfinding = true;
    [SerializeField]
    private LayerMask wallLayers;
    [SerializeField, Min(0.05f)]
    private float bodyRadius = 0.3f;
    [SerializeField, Min(0.05f)]
    private float nodeReachRadius = 0.4f;
    [SerializeField, Min(0.1f)]
    private float repathInterval = 0.5f;
    [SerializeField, Min(0.1f)]
    private float repathDistance = 1.5f;

    public event Action<Vector2> FacingChanged;

    public bool IsMoving { get; private set; }
    public Vector2 Facing { get; private set; } = Vector2.down;
    public float Speed => speed;
    public float YScale => isometricYScale;

    public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;

    private readonly List<Node> _path = new();
    private Rigidbody2D _body;
    private Vector2 _destination;
    private Vector2 _pathDestination;
    private float _speedMultiplier = 1f;
    private float _nextRepathTime;
    private bool _hasDestination;

    private void Reset()
    {
        wallLayers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();

        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Walls");
    }

    public float GroundDistanceTo(Vector2 point) => Isometric.GroundDistance(Position, point, isometricYScale);
    
    public bool HasClearWalk(Vector2 point) => HasLineOfSight(Position, point);

    public void SetDestination(Vector2 point, float speedMultiplier = 1f)
    {
        _destination = point;
        _speedMultiplier = Mathf.Max(0f, speedMultiplier);
        _hasDestination = true;
        IsMoving = true;
    }

    public void Stop()
    {
        _hasDestination = false;
        IsMoving = false;
        _path.Clear();
    }

    public IEnumerator Follow(Func<Vector2> destination, float stopDistance, float timeout, float speedMultiplier = 1f)
    {
        var deadline = Time.time + Mathf.Max(0f, timeout);

        while (Time.time < deadline) {
            var point = destination();

            if (GroundDistanceTo(point) <= stopDistance)
                break;

            SetDestination(point, speedMultiplier);
            yield return null;
        }

        Stop();
    }

    private void FixedUpdate()
    {
        if (!_hasDestination)
            return;

        var position = Position;
        var step = speed * _speedMultiplier * Time.fixedDeltaTime;

        if (step <= 0f)
            return;

        var waypoint = NextWaypoint(position, _destination);
        var delta = waypoint - position;

        if (delta.sqrMagnitude <= 0.000001f)
            return;

        var direction = delta.normalized;
        MoveTo(position + direction * Mathf.Min(step, delta.magnitude));

        SetFacing(Isometric.ToGround(direction, isometricYScale).normalized);
    }

    private Vector2 NextWaypoint(Vector2 position, Vector2 destination)
    {
        if (!usePathfinding || HasLineOfSight(position, destination)) {
            _path.Clear();
            return destination;
        }

        UpdatePath(position, destination);

        while (_path.Count > 0 &&
               (_path[0] == null || Vector2.Distance(position, _path[0].transform.position) <= nodeReachRadius))
            _path.RemoveAt(0);

        return _path.Count > 0 ? (Vector2)_path[0].transform.position : destination;
    }

    private void UpdatePath(Vector2 position, Vector2 destination)
    {
        if (AStarManager.instance == null)
            return;

        var drifted = (destination - _pathDestination).sqrMagnitude > repathDistance * repathDistance;
        if (!drifted && _path.Count > 0 && Time.time < _nextRepathTime)
            return;

        _nextRepathTime = Time.time + repathInterval;
        _pathDestination = destination;

        var start = AStarManager.instance.FindNearestNode(position);
        var end = AStarManager.instance.FindNearestNode(destination);

        if (start == null || end == null)
            return;

        var path = AStarManager.instance.GeneratePath(start, end);

        _path.Clear();
        if (path != null)
            _path.AddRange(path);
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        if (wallLayers.value == 0)
            return true;

        var delta = to - from;
        var distance = delta.magnitude;

        if (distance <= 0.0001f)
            return true;

        return !Physics2D.CircleCast(from, bodyRadius, delta / distance, distance, wallLayers);
    }

    private void SetFacing(Vector2 groundDirection)
    {
        if (groundDirection.sqrMagnitude <= 0.0001f)
            return;

        Facing = groundDirection;
        FacingChanged?.Invoke(Facing);
    }

    private void MoveTo(Vector2 position)
    {
        if (_body != null && _body.bodyType != RigidbodyType2D.Static)
            _body.MovePosition(position);
        else
            transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (_path == null || _path.Count == 0)
            return;

        Gizmos.color = Color.cyan;
        var previous = transform.position;

        foreach (var node in _path) {
            if (node == null)
                continue;

            Gizmos.DrawLine(previous, node.transform.position);
            previous = node.transform.position;
        }
    }
}
