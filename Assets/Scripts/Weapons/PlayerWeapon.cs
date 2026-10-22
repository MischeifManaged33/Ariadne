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
    // Used instead of the fan when a bow is equipped
    [SerializeField]
    private RectIndicator rectIndicator;
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
    // Bow shots; direction and charge from 0 to 1
    public event Action<Vector2, float> Fired;

    public bool IsAiming { get; private set; }

    public bool IsCharging => _charging;

    public float Charge => _charging && Bow != null ? Bow.Charge(Time.time - _chargeStartTime) : 0f;

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private InputAction _pointAction;
    private InputAction _fireAction;
    private Camera _camera;
    private VirtualJoystick _boundJoystick;
    private float _nextAttackTime;

    private bool _charging;
    private bool _chargedByStick;
    private bool _stickAimed;
    private bool _pressBlockedByUI;
    private bool _fullChargeFlashed;
    private float _chargeStartTime;

    private readonly HashSet<IDamagable> _struck = new();

    public Vector2 Origin => weaponPivot != null ? (Vector2)weaponPivot.position : (Vector2)transform.position;

    public WeaponData EquippedWeapon => equippedWeapon;

    private BowData Bow => equippedWeapon as BowData;

    private bool StickHeld => _boundJoystick != null && _boundJoystick.IsPressed;

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
        CancelCharge();
    }

    private void OnDestroy()
    {
        _pointAction?.Dispose();
        _fireAction?.Dispose();
    }

    private void Update()
    {
        if (PauseMenu.IsPaused) {
            // Drop the draw, and make the click that unpauses not start a new one
            CancelCharge();
            _pressBlockedByUI = true;
            return;
        }

        if (_boundJoystick == null)
            BindJoystick(aimJoystick != null ? aimJoystick : VirtualJoystick.Aim);

        if (StickHeld)
            UpdateStickAim();
        else
            UpdatePointerAim();

        UpdateBow();
        UpdateIndicator();
    }

    public void Equip(WeaponData newWeapon)
    {
        CancelCharge();
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

        if (tilt.magnitude >= tapThreshold)
            _stickAimed = true;
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

        if (_fireAction.WasPressedThisFrame()) {
            _pressBlockedByUI = IsPointerOverUI();

            if (!_pressBlockedByUI && Bow == null)
                Attack(AimDirection);
        }
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void OnStickReleased(Vector2 tilt)
    {
        IsAiming = false;

        if (Bow != null) {
            if (!_charging || !_chargedByStick)
                return;

            // A quick flick still fires where the stick last pointed
            var aimed = tilt.magnitude >= tapThreshold ? ToGround(tilt)
                : _stickAimed ? AimDirection
                : ToGround(player != null ? player.Facing : Vector2.down);

            AimDirection = aimed;
            FireArrow(aimed);
            return;
        }

        var direction = tilt.magnitude >= tapThreshold ? ToGround(tilt)
            : ToGround(player != null ? player.Facing : Vector2.down);

        AimDirection = direction;
        Attack(direction);
    }

    private void Attack(Vector2 groundDirection)
    {
        // Stick releases arrive as UI events, so Update's pause check doesn't cover them
        if (!CanAttack || PauseMenu.IsPaused)
            return;

        _nextAttackTime = Time.time + equippedWeapon.attackCooldown;

        var origin = Origin;
        var halfAngle = equippedWeapon.attackAngle * 0.5f;

        equippedWeapon.sounds.PlaySwing(origin);

        var candidates = Physics2D.OverlapCircleAll(origin, equippedWeapon.attackRange, enemyLayers);

        _struck.Clear();

        var impactPoint = Vector2.zero;
        var connected = false;

        foreach (var candidate in candidates) {
            var target = candidate.GetComponentInParent<IDamagable>();

            if (target == null || !target.IsAlive || !_struck.Add(target))
                continue;
            if (!InsideFan(origin, groundDirection, halfAngle, candidate))
                continue;

            target.TakeDamage(equippedWeapon.damage);

            if (!connected) {
                impactPoint = candidate.ClosestPoint(origin);
                connected = true;
            }

            // Knockback logic
            if (target is IKnockbackable knockable)
                knockable.ApplyKnockback(groundDirection);
        }

        if (connected)
            equippedWeapon.sounds.PlayImpact(impactPoint);

        if (indicator != null)
            indicator.Flash();

        Attacked?.Invoke(groundDirection);
    }

    // Charging starts whenever the button or stick is held and the bow is ready,
    // so holding through the cooldown starts the next draw straight away
    private void UpdateBow()
    {
        var bow = Bow;
        if (bow == null) {
            CancelCharge();
            return;
        }

        if (!_charging) {
            if (!CanAttack)
                return;

            if (StickHeld)
                BeginCharge(true);
            else if (_fireAction.IsPressed() && !_pressBlockedByUI)
                BeginCharge(false);

            return;
        }

        if (_chargedByStick) {
            // Release normally arrives through OnStickReleased; this covers losing the stick mid draw
            if (!StickHeld)
                CancelCharge();
        }
        else if (!_fireAction.IsPressed()) {
            FireArrow(AimDirection);
            return;
        }

        if (!_fullChargeFlashed && Charge >= 1f) {
            _fullChargeFlashed = true;

            if (rectIndicator != null)
                rectIndicator.Flash();
        }
    }

    private void BeginCharge(bool byStick)
    {
        _charging = true;
        _chargedByStick = byStick;
        _stickAimed = false;
        _fullChargeFlashed = false;
        _chargeStartTime = Time.time;

        Bow.sounds.PlayWindup(Origin);
    }

    private void CancelCharge()
    {
        _charging = false;
        _chargedByStick = false;
        _stickAimed = false;
        _fullChargeFlashed = false;
    }

    private void FireArrow(Vector2 groundDirection)
    {
        var bow = Bow;
        var charge = Charge;

        CancelCharge();

        if (bow == null || !CanAttack || PauseMenu.IsPaused)
            return;

        _nextAttackTime = Time.time + bow.attackCooldown;

        var spawn = Origin + Isometric.ToScreen(groundDirection * bow.spawnOffset, isometricYScale);

        bow.sounds.PlaySwing(spawn);

        if (bow.arrowPrefab != null) {
            var arrow = Instantiate(bow.arrowPrefab, new Vector3(spawn.x, spawn.y, transform.position.z),
                Quaternion.identity);

            // The indicator measures from the origin, so take off the part covered by the spawn offset
            arrow.Launch(groundDirection, isometricYScale, bow.DamageAt(charge), bow.SpeedAt(charge),
                Mathf.Max(0f, bow.DistanceAt(charge) - bow.spawnOffset), bow.KnockbackAt(charge),
                enemyLayers, bow.sounds);
        }
        else {
            Debug.LogWarning($"{bow.name} has no arrow prefab assigned.", bow);
        }

        if (rectIndicator != null)
            rectIndicator.Flash();

        Fired?.Invoke(groundDirection, charge);
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
        var bow = Bow;

        if (rectIndicator != null) {
            if (bow != null) {
                var fill = _charging ? bow.DistanceAt(Charge) / bow.maxDistance : 0f;

                rectIndicator.Aim(Origin, AimDirection, bow.maxDistance, bow.indicatorWidth, fill, isometricYScale);
                rectIndicator.SetVisible(_charging || (IsAiming && CanAttack));
            }
            else {
                rectIndicator.SetVisible(false);
            }
        }

        if (indicator == null)
            return;

        if (equippedWeapon == null || bow != null) {
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

        if (equippedWeapon is BowData bow) {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + Isometric.ToScreen(AimDirection * bow.maxDistance, isometricYScale));
            return;
        }

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
