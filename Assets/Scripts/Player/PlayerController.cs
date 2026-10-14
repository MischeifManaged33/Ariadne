using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private Animator animator;

    [Header("Floor Bounds")]
    [SerializeField]
    private DesignedDungeonGenerator dungeonGenerator;

    [Tooltip(
        "A child marker at the player's ground contact point. " +
        "Leave empty if the root is at the feet."
    )]
    [SerializeField] private Transform feet;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float acceleration = 60f;

    [SerializeField, Range(0.1f, 1f)]
    private float isometricYScale = 0.5f;

    private Rigidbody2D _rigidbody;
    private InputAction _moveAction;
    private Vector2 _velocity;
    private Vector2 lastMoveDirection = Vector2.down;

    public Vector2 CurrentInput { get; private set; }
    public Vector2 CurrentVelocity => _velocity;

    private void Reset()
    {
        player = GetComponent<Player>();
        ConfigureBody(GetComponent<Rigidbody2D>());
    }

    private void UpdateAnimation(Vector2 movement)
    {
        if (animator == null)
            return;

        animator.SetFloat("MoveX", movement.x);
        animator.SetFloat("MoveY", movement.y);
        animator.SetFloat("Speed", movement.sqrMagnitude);

        if (movement.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = movement.normalized;

            animator.SetFloat(
                "LastMoveX",
                lastMoveDirection.x
            );

            animator.SetFloat(
                "LastMoveY",
                lastMoveDirection.y
            );
        }
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        ConfigureBody(_rigidbody);

        if (player == null)
            player = GetComponent<Player>();

        if (dungeonGenerator == null)
        {
            Debug.LogError(
                "Assign Dungeon Generator on PlayerController " +
                "to enable movement.",
                this
            );
        }

        _moveAction = new InputAction(
            "Move",
            InputActionType.Value,
            expectedControlType: "Vector2"
        );

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

    private void OnEnable() => _moveAction?.Enable();

    private void OnDisable() => _moveAction?.Disable();

    private void OnDestroy() => _moveAction?.Dispose();

    private void FixedUpdate()
    {
        Vector2 input = ReadInput();
        CurrentInput = input;

        float speed = player != null
            ? player.MoveSpeed
            : 5f;

        Vector2 target = new Vector2(
            input.x,
            input.y * isometricYScale
        ) * speed;

        _velocity = acceleration > 0f
            ? Vector2.MoveTowards(
                _velocity,
                target,
                acceleration * Time.fixedDeltaTime
            )
            : target;

        Vector2 requestedMove =
            _velocity * Time.fixedDeltaTime;

        Vector2 allowedMove = Vector2.zero;

        if (dungeonGenerator != null &&
            dungeonGenerator.WalkableFloorReady)
        {
            // Use the physics position rather than the
            // interpolated Transform position.
            Vector3 feetOffset = feet != null
                ? feet.position - transform.position
                : Vector3.zero;

            Vector3 start = new Vector3(
                _rigidbody.position.x,
                _rigidbody.position.y,
                transform.position.z
            ) + feetOffset;

            float fraction =
                dungeonGenerator.GetWalkableMoveFraction(
                    start,
                    start + (Vector3)requestedMove
                );

            allowedMove = requestedMove * fraction;

            // Try sliding along one world axis when blocked.
            if (fraction < 1f)
            {
                Vector2 xMove = new Vector2(
                    requestedMove.x,
                    0f
                );

                Vector2 yMove = new Vector2(
                    0f,
                    requestedMove.y
                );

                xMove *=
                    dungeonGenerator.GetWalkableMoveFraction(
                        start,
                        start + (Vector3)xMove
                    );

                yMove *=
                    dungeonGenerator.GetWalkableMoveFraction(
                        start,
                        start + (Vector3)yMove
                    );

                if (xMove.sqrMagnitude >
                    allowedMove.sqrMagnitude)
                {
                    allowedMove = xMove;
                }

                if (yMove.sqrMagnitude >
                    allowedMove.sqrMagnitude)
                {
                    allowedMove = yMove;
                }
            }
        }

        _velocity = allowedMove / Time.fixedDeltaTime;
        _rigidbody.linearVelocity = _velocity;

        UpdateAnimation(new Vector2(
            _velocity.x,
            _velocity.y / isometricYScale
        ));

        if (player != null)
            player.OnMovementUpdated(_velocity);
    }

    private Vector2 ReadInput()
    {
        Vector2 input =
            _moveAction.ReadValue<Vector2>();

        VirtualJoystick stick = joystick != null
            ? joystick
            : VirtualJoystick.Move;

        if (stick != null)
            input += stick.Direction;

        return Vector2.ClampMagnitude(input, 1f);
    }

    private static void ConfigureBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        body.interpolation =
            RigidbodyInterpolation2D.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
    }
}