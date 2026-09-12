using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField]
    private WeaponData equippedWeapon;

    [Header("References")]
    [SerializeField]
    private Transform weaponPivot;
    [SerializeField]
    private SpriteRenderer weaponRenderer;
    [SerializeField]
    private AttackIndicator indicator;
    [SerializeField]
    private Player player;
    [SerializeField]
    private VirtualJoystick aimJoystick;

    [Header("Targets")]
    [SerializeField]
    private LayerMask enemyLayers;

    [Header("Aiming")]
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = 0.5f;
    [SerializeField]
    private bool alwaysShowPointerAim = true;
    [SerializeField, Range(0f, 1f)]
    private float tapThreshold = 0.25f;

    public event Action<Vector2> Attacked;

    public bool IsAiming { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private InputAction _pointAction;
    private InputAction _fireAction;
    private Camera _camera;
    private VirtualJoystick _boundJoystick;
    private float _nextAttackTime;

    private readonly HashSet<IDamagable> _struck = new();

    private Vector2 Origin => weaponPivot != null ? (Vector2)weaponPivot.position : (Vector2)transform.position;

    public bool CanAttack => equippedWeapon != null && Time.time >= _nextAttackTime;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<Player>();

        _pointAction = new InputAction("Point", InputActionType.Value, "<Mouse>/position",
            expectedControlType: "Vector2");

        _fireAction = new InputAction("Fire", InputActionType.Button);
        _fireAction.AddBinding("<Mouse>/leftButton");
        _fireAction.AddBinding("<Gamepad>/rightTrigger");
    }

    private void Start()
    {
        Equip(equippedWeapon);
    }

    private void OnEnable()
    {
        _pointAction.Enable();
        _fireAction.Enable();

        BindJoystick(aimJoystick != null ? aimJoystick : VirtualJoystick.Aim);
    }

    private void OnDisable()
    {
        _pointAction.Disable();
        _fireAction.Disable();

        BindJoystick(null);
    }

    private void OnDestroy()
    {
        _pointAction?.Dispose();
        _fireAction?.Dispose();
    }

    private void Update()
    {
        if (_boundJoystick == null)
            BindJoystick(aimJoystick != null ? aimJoystick : VirtualJoystick.Aim);

        if (_boundJoystick != null && _boundJoystick.IsPressed)
            UpdateStickAim();
        else
            UpdatePointerAim();

        UpdateIndicator();
    }

    public void Equip(WeaponData newWeapon)
    {
        equippedWeapon = newWeapon;

        if (weaponRenderer != null)
            weaponRenderer.sprite = equippedWeapon != null ? equippedWeapon.sprite : null;
    }

    private void UpdateStickAim()
    {
        IsAiming = true;

        var tilt = _boundJoystick.Direction;
        if (tilt.sqrMagnitude > 0.0001f)
            AimDirection = ToGround(tilt);
    }

    private void UpdatePointerAim()
    {
        if (Mouse.current == null) {
            IsAiming = false;
            return;
        }

        if (_camera == null)
            _camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (_camera == null) {
            IsAiming = false;
            return;
        }

        var screen = _pointAction.ReadValue<Vector2>();
        var world = (Vector2)_camera.ScreenToWorldPoint(screen);
        var toPointer = world - Origin;

        if (toPointer.sqrMagnitude > 0.0001f)
            AimDirection = ToGround(toPointer);

        IsAiming = alwaysShowPointerAim || _fireAction.IsPressed();

        if (_fireAction.WasPressedThisFrame() && !IsPointerOverUI())
            Attack(AimDirection);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void OnStickReleased(Vector2 tilt)
    {
        IsAiming = false;

        var direction = tilt.magnitude >= tapThreshold ? ToGround(tilt)
            : ToGround(player != null ? player.Facing : Vector2.down);

        AimDirection = direction;
        Attack(direction);
    }

    private void Attack(Vector2 groundDirection)
    {
        if (!CanAttack)
            return;

        _nextAttackTime = Time.time + equippedWeapon.attackCooldown;

        var origin = Origin;
        var halfAngle = equippedWeapon.attackAngle * 0.5f;

        var candidates = Physics2D.OverlapCircleAll(origin, equippedWeapon.attackRange, enemyLayers);

        _struck.Clear();

        foreach (var candidate in candidates) {
            var target = candidate.GetComponentInParent<IDamagable>();

            if (target == null || !target.IsAlive || !_struck.Add(target))
                continue;
            if (!InsideFan(origin, groundDirection, halfAngle, candidate))
                continue;

            target.TakeDamage(equippedWeapon.damage);

            // Knockback logic
            if (target is IKnockbackable knockable)
                knockable.ApplyKnockback(groundDirection);
        }

        if (indicator != null)
            indicator.Flash();

        Attacked?.Invoke(groundDirection);
    }

    private bool InsideFan(Vector2 origin, Vector2 groundDirection, float halfAngle, Collider2D candidate)
    {
        var relative = ToGroundSpace(candidate.ClosestPoint(origin) - origin);
        var distance = relative.sqrMagnitude;

        if (distance > equippedWeapon.attackRange * equippedWeapon.attackRange)
            return false;
        if (distance <= equippedWeapon.hitRadius * equippedWeapon.hitRadius)
            return true;

        return Vector2.Angle(relative, groundDirection) <= halfAngle;
    }

    private void UpdateIndicator()
    {
        if (indicator == null)
            return;

        if (equippedWeapon == null) {
            indicator.SetVisible(false);
            return;
        }

        indicator.Aim(Origin, AimDirection, equippedWeapon.attackRange, equippedWeapon.attackAngle, isometricYScale);
        indicator.SetReady(CanAttack);
        indicator.SetVisible(IsAiming);
    }

    private void BindJoystick(VirtualJoystick stick)
    {
        if (_boundJoystick == stick)
            return;

        if (_boundJoystick != null)
            _boundJoystick.Released -= OnStickReleased;

        _boundJoystick = stick;

        if (_boundJoystick != null)
            _boundJoystick.Released += OnStickReleased;
    }

    private Vector2 ToGroundSpace(Vector2 screenSpace)
    {
        return new Vector2(screenSpace.x, screenSpace.y / isometricYScale);
    }

    private Vector2 ToGround(Vector2 screenSpace)
    {
        var ground = ToGroundSpace(screenSpace);
        return ground.sqrMagnitude > 0.0001f ? ground.normalized : AimDirection;
    }

    private void OnDrawGizmosSelected()
    {
        if (equippedWeapon == null)
            return;

        var origin = Origin;
        var half = equippedWeapon.attackAngle * 0.5f;
        var heading = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;

        Gizmos.color = Color.red;

        Vector3 previous = default;
        for (var i = 0; i <= 24; i++) {
            var radians = (heading - half + equippedWeapon.attackAngle * i / 24f) * Mathf.Deg2Rad;
            var point = origin + new Vector2(Mathf.Cos(radians) * equippedWeapon.attackRange,
                Mathf.Sin(radians) * equippedWeapon.attackRange * isometricYScale);

            if (i == 0 || i == 24)
                Gizmos.DrawLine(origin, point);
            else
                Gizmos.DrawLine(previous, point);

            previous = point;
        }
    }
}
