using UnityEngine;

[DefaultExecutionOrder(200)]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform target;
    [SerializeField]
    private Vector2 offset = Vector2.zero;

    [Header("Smoothing")]
    [SerializeField]
    private Vector2 deadZone = new Vector2(1.5f, 0.9f);
    [SerializeField, Min(0f)]
    private float smoothTime = 0.18f;
    [SerializeField, Min(0f)]
    private float maxSpeed = 0f;

    [Header("Look Ahead")]
    [SerializeField, Min(0f)]
    private float lookAheadDistance = 1.5f;
    [SerializeField, Min(0f)]
    private float lookAheadSmoothTime = 0.35f;
    [SerializeField, Range(0f, 1f)]
    private float idleLookAhead = 0.4f;
    [SerializeField, Range(0.1f, 1f)]
    private float lookAheadYScale = 0.5f;

    private Player _player;
    private Rigidbody2D _targetBody;
    private float _z;

    private Vector2 _anchor;
    private Vector2 _lookAhead;
    private Vector2 _position;
    private Vector2 _followVelocity;
    private Vector2 _lookAheadVelocity;

    private Vector2 _previousTargetPosition;
    private Vector2 _leadDirection;
    private const float MovingThreshold = 0.05f;

    private void Awake()
    {
        _z = transform.position.z;
    }

    private void OnEnable()
    {
        if (target == null) {
            _player = FindAnyObjectByType<Player>();
            if (_player != null)
                target = _player.transform;
        }

        if (target == null) {
            Debug.LogWarning("No target to follow.", this);
            return;
        }

        if (_player == null)
            _player = target.GetComponentInParent<Player>();
        _targetBody = target.GetComponentInParent<Rigidbody2D>();

        GoToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        var focus = (Vector2)target.position + offset;

        MoveAnchor(focus);
        UpdateLookAhead();

        var goal = _anchor + _lookAhead;
        _position = Vector2.SmoothDamp(_position, goal, ref _followVelocity, smoothTime,
            maxSpeed > 0f ? maxSpeed : Mathf.Infinity, Time.deltaTime);

        transform.position = new Vector3(_position.x, _position.y, _z);
        _previousTargetPosition = (Vector2)target.position;
    }

    private void MoveAnchor(Vector2 focus)
    {
        var delta = focus - _anchor;

        _anchor.x += Mathf.Sign(delta.x) * Mathf.Max(0f, Mathf.Abs(delta.x) - deadZone.x);
        _anchor.y += Mathf.Sign(delta.y) * Mathf.Max(0f, Mathf.Abs(delta.y) - deadZone.y);
    }

    private void UpdateLookAhead()
    {
        var goal = Vector2.zero;

        if (lookAheadDistance > 0f) {
            var velocity = CurrentTargetVelocity();
            var reference = _player != null ? _player.MoveSpeed : velocity.magnitude;

            if (velocity.magnitude > Mathf.Max(reference, 0.01f) * MovingThreshold) {
                var amount = reference > 0f ? Mathf.Clamp01(velocity.magnitude / reference) : 1f;
                var direction = velocity.normalized;

                _leadDirection = new Vector2(direction.x, direction.y * lookAheadYScale);
                goal = _leadDirection * (lookAheadDistance * amount);
            }
            else 
                goal = _leadDirection * (lookAheadDistance * idleLookAhead);
        }

        _lookAhead = Vector2.SmoothDamp(_lookAhead, goal, ref _lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, Time.deltaTime);
    }

    private Vector2 CurrentTargetVelocity()
    {
        if (_targetBody != null)
            return _targetBody.linearVelocity;

        return Time.deltaTime > 0f ? ((Vector2)target.position - _previousTargetPosition) / Time.deltaTime : Vector2.zero;
    }

    public void GoToTarget()
    {
        if (target == null)
            return;

        _anchor = (Vector2)target.position + offset;
        _position = _anchor;
        _lookAhead = Vector2.zero;
        _followVelocity = Vector2.zero;
        _lookAheadVelocity = Vector2.zero;
        _previousTargetPosition = target.position;
        _leadDirection = Vector2.zero;

        transform.position = new Vector3(_position.x, _position.y, _z);
    }

    public void SetTarget(Transform value, bool snap = true)
    {
        target = value;
        _player = value != null ? value.GetComponentInParent<Player>() : null;
        _targetBody = value != null ? value.GetComponentInParent<Rigidbody2D>() : null;

        if (snap)
            GoToTarget();
    }

}
