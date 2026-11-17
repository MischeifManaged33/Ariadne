using System.Collections.Generic;
using UnityEngine;

// Puts an enemy to sleep while it is off screen and away from the player, so idle enemies
// left around the maze stop wandering, pathing, animating and simulating physics.
// EnemyAI adds this automatically; add it by hand only to tune the distances.
[DisallowMultipleComponent]
public class EnemyActivation : MonoBehaviour
{
    [Header("Range")]
    // Always awake within this distance of the player, even off screen
    [SerializeField, Min(0f)]
    private float wakeDistance = 10f;
    // Extra room past wakeDistance before falling asleep again, so it doesn't flicker at the edge
    [SerializeField, Min(0f)]
    private float sleepMargin = 4f;
    [SerializeField, Min(0f)]
    private float screenMargin = 0.25f;

    [Header("Timing")]
    [SerializeField, Min(0.05f)]
    private float checkInterval = 0.25f;

    public bool IsAsleep { get; private set; }

    // How long it has been asleep, or 0 while awake
    public float SleepingFor => IsAsleep ? Time.time - _sleptAt : 0f;

    private static Camera _camera;
    private static Transform _player;

    private readonly List<Behaviour> _paused = new();
    private EnemyAI _ai;
    private IDamagable _health;
    private Rigidbody2D _body;
    private bool _bodyWasSimulated;
    private float _nextCheckTime;
    private float _sleptAt;

    private void Awake()
    {
        _ai = GetComponent<EnemyAI>();
        _health = GetComponent<IDamagable>();
        _body = GetComponent<Rigidbody2D>();

        // Spread the checks out so a wave of spawns doesn't all check on the same frame
        _nextCheckTime = Time.time + Random.Range(0f, checkInterval);
    }

    private void OnDisable()
    {
        Wake();
    }

    private void Update()
    {
        if (Time.time < _nextCheckTime)
            return;

        _nextCheckTime = Time.time + checkInterval;

        if (IsAsleep) {
            if (ShouldWake())
                Wake();
        }
        else if (CanSleep()) {
            Sleep();
        }
    }

    private bool ShouldWake()
    {
        return NearPlayer(wakeDistance) || OnScreen();
    }

    private bool CanSleep()
    {
        if (_health != null && !_health.IsAlive)
            return false;
        if (_ai != null && (_ai.IsAlerted || _ai.IsBusy))
            return false;

        // With no player or camera to measure against, stay awake rather than guess
        if (FindPlayer() == null || FindCamera() == null)
            return false;

        return !NearPlayer(wakeDistance + sleepMargin) && !OnScreen();
    }

    private void Sleep()
    {
        IsAsleep = true;
        _sleptAt = Time.time;

        if (_ai != null && _ai.Mover != null)
            _ai.Mover.Stop();

        Pause(GetComponentsInChildren<EnemyAI>());
        Pause(GetComponentsInChildren<EnemyMover>());
        Pause(GetComponentsInChildren<EnemyAbility>());
        Pause(GetComponentsInChildren<Animator>());

        if (_body != null) {
            _bodyWasSimulated = _body.simulated;
            _body.simulated = false;
        }
    }

    private void Wake()
    {
        if (!IsAsleep)
            return;

        IsAsleep = false;

        // Only turn back on what this put to sleep, so anything switched off on purpose stays off
        foreach (var behaviour in _paused) {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        _paused.Clear();

        if (_body != null)
            _body.simulated = _bodyWasSimulated;
    }

    private void Pause<T>(T[] behaviours) where T : Behaviour
    {
        foreach (var behaviour in behaviours) {
            if (behaviour == null || !behaviour.enabled)
                continue;

            behaviour.enabled = false;
            _paused.Add(behaviour);
        }
    }

    private bool NearPlayer(float distance)
    {
        var player = FindPlayer();
        if (player == null)
            return true;

        return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude <= distance * distance;
    }

    private bool OnScreen()
    {
        var camera = FindCamera();
        if (camera == null)
            return true;

        var viewport = camera.WorldToViewportPoint(transform.position);

        return viewport.x >= -screenMargin && viewport.x <= 1f + screenMargin
            && viewport.y >= -screenMargin && viewport.y <= 1f + screenMargin;
    }

    private static Camera FindCamera()
    {
        if (_camera == null || !_camera.isActiveAndEnabled)
            _camera = Camera.main;

        return _camera;
    }

    private static Transform FindPlayer()
    {
        if (_player == null) {
            var health = PlayerHealth.Current != null ? PlayerHealth.Current : FindAnyObjectByType<PlayerHealth>();
            _player = health != null ? health.transform : null;
        }

        return _player;
    }

    public static EnemyActivation GetOrAdd(GameObject target)
    {
        var activation = target.GetComponent<EnemyActivation>();
        return activation != null ? activation : target.AddComponent<EnemyActivation>();
    }
}
