using System;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

// Dashing
public class PlayerDash : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerController controller;
    [SerializeField]
    private PlayerHealth health;

    [Header("Dash")]
    [SerializeField, Min(0f)]
    private float distance = 3f;
    [SerializeField, Min(0.01f)]
    private float duration = 0.15f;
    [SerializeField, Min(0f)]
    private float cooldown = 1f;
    [SerializeField]
    private EventReference dashSound;

    public static PlayerDash Active { get; private set; }

    public event Action<Vector2> Dashed;

    // Properties
    public bool IsDashing => Time.time < _dashEndsAt;
    public float Speed => distance / duration;
    public Vector2 Direction { get; private set; }
    public bool CanDash => !IsDashing && Time.time >= _nextDashTime;
    public float Cooldown => cooldown;
    public float CooldownRemaining => Mathf.Max(0f, _nextDashTime - Time.time);
    public float CooldownNormalized => cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / cooldown) : 0f;

    private InputAction _dashAction;
    private float _dashEndsAt;
    private float _nextDashTime;

    private void Reset()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<PlayerController>();
        if (health == null)
            health = GetComponent<PlayerHealth>();

        _dashAction = new InputAction("Dash", InputActionType.Button);
        _dashAction.AddBinding("<Keyboard>/space");
    }

    private void OnEnable()
    {
        _dashAction.Enable();
        Active = this;
    }

    private void OnDisable()
    {
        _dashAction.Disable();
        _dashEndsAt = 0f;

        if (Active == this)
            Active = null;
    }

    private void OnDestroy() => _dashAction?.Dispose();

    private void Update()
    {
        if (_dashAction.WasPressedThisFrame())
            TryDash();
    }

    public bool TryDash()
    {
        if (!isActiveAndEnabled || PauseMenu.IsPaused || !CanDash)
            return false;

        var direction = controller != null ? controller.CurrentInput : Vector2.zero;
        if (direction.sqrMagnitude < 0.01f)
            direction = controller != null ? controller.LastMoveDirection : Vector2.down;

        Direction = direction.normalized;
        _dashEndsAt = Time.time + duration;
        _nextDashTime = _dashEndsAt + cooldown;

        if (health != null)
            health.InvulnerbilityWindow(duration);

        Sfx.PlayOneShot(dashSound, transform.position);
        Dashed?.Invoke(Direction);

        return true;
    }
}
