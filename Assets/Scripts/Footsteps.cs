using FMODUnity;
using UnityEngine;

// Footstep player
public class Footsteps : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField]
    private EventReference footstep;

    [Header("Gait")]
    private float strideLength = 2.5f;
    private float minInterval = 0.15f;
    private bool stepOnFirstMove = true;

    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = Isometric.DefaultYScale;

    private Vector2 _lastPosition;
    private float _travelled;
    private float _nextStepTime;
    private bool _isMoving;

    private void OnEnable()
    {
        _lastPosition = transform.position;
        _travelled = stepOnFirstMove ? strideLength : 0f;
        _isMoving = false;
    }

    private void Update()
    {
        var position = (Vector2)transform.position;
        var moved = Isometric.ToGround(position - _lastPosition, isometricYScale).magnitude;

        _lastPosition = position;

        if (moved <= 0.0001f)
        {
            _isMoving = false;
            return;
        }

        if (!_isMoving)
        {
            _isMoving = true;

            if (stepOnFirstMove)
                _travelled = strideLength;
        }

        _travelled += moved;

        if (_travelled < strideLength || Time.time < _nextStepTime)
            return;

        Sfx.PlayOneShot(footstep, position);

        _travelled -= strideLength;

        if (_travelled > strideLength)
            _travelled = 0f;

        _nextStepTime = Time.time + minInterval;
    }
}
