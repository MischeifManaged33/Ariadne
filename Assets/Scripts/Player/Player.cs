using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 5f;

    [Header("Sprite")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private Animator animator;

    [Header("Sorting")]
    [SerializeField]
    private bool sortByDepth = true;
    [SerializeField, Min(1f)]
    private float sortingPrecision = 16f;

    // Hash for animator
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // Properties
    public float MoveSpeed => moveSpeed;
    public Vector2 Facing { get; private set; } = Vector2.down;
    public bool IsMoving { get; private set; }

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void LateUpdate()
    {
        if (sortByDepth && spriteRenderer != null)
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * sortingPrecision);
    }


    public void OnMovementUpdated(Vector2 velocity)
    {
        IsMoving = velocity.sqrMagnitude > 0.0001f;
        if (IsMoving)
            Facing = velocity.normalized;

        if (animator == null)
            return;

        animator.SetFloat(MoveXHash, Facing.x);
        animator.SetFloat(MoveYHash, Facing.y);
        animator.SetFloat(SpeedHash, velocity.magnitude);
    }
}
