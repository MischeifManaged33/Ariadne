using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Player player;
    [SerializeField]
    private VirtualJoystick joystick;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float acceleration = 60f;
    // Currently, moving up/down will be slightly faster than left/right, be probably need to tweak it a bit
    // after an exact scale for the grid is determined. I'm just using 2:1 ratio
    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = 0.5f;

    private Rigidbody2D _rigidbody;
    private InputAction _moveAction;
    private Vector2 _velocity;

    private void Reset()
    {
        player = GetComponent<Player>();
        ConfigureBody(GetComponent<Rigidbody2D>());
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        ConfigureBody(_rigidbody);

        if (player == null)
            player = GetComponent<Player>();

        _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        _moveAction.AddBinding("<Gamepad>/leftStick");
        _moveAction.AddBinding("<Joystick>/stick");
    }

    private void OnEnable() => _moveAction.Enable();

    private void OnDisable() => _moveAction.Disable();

    private void OnDestroy() => _moveAction?.Dispose();

    private void FixedUpdate()
    {
        var input = ReadInput();

        var speed = player != null ? player.MoveSpeed : 5f;
        var target = new Vector2(input.x, input.y * isometricYScale) * speed;

        _velocity = acceleration > 0f
            ? Vector2.MoveTowards(_velocity, target, acceleration * Time.fixedDeltaTime) : target;

        _rigidbody.linearVelocity = _velocity;

        if (player != null)
            player.OnMovementUpdated(_velocity);
    }

    // Merges keyboard or joystick input into a single clamped vector
    private Vector2 ReadInput()
    {
        var input = _moveAction.ReadValue<Vector2>();

        var stick = joystick != null ? joystick : VirtualJoystick.Active;
        if (stick != null)
            input += stick.Direction;

        return Vector2.ClampMagnitude(input, 1f);
    }

    // Helper to configure the Rigidbody2D for top down
    private static void ConfigureBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }
}
