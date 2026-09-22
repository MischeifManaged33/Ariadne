using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PuzzleBlock : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float pushCooldown = 0.15f;

    public Vector3Int Cell { get; private set; }
    public bool IsMoving { get; private set; }

    private PuzzleManager puzzleManager;
    private Rigidbody2D rb;
    private float nextPushTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsMoving || Time.time < nextPushTime)
            return;

        PlayerController player =
            collision.gameObject.GetComponentInParent<PlayerController>();

        if (player == null)
            return;

        Vector2 input = player.CurrentInput;

        if (input.sqrMagnitude < 0.01f)
            return;

        Vector2 playerToBlock =
            (Vector2)transform.position -
            (Vector2)player.transform.position;

        float movingTowardBlock = Vector2.Dot(
            player.CurrentVelocity.normalized,
            playerToBlock.normalized
        );

        // Prevent moving the block while walking away
        // or merely brushing against its side.
        if (movingTowardBlock < 0.2f)
            return;

        Vector3Int direction =
            puzzleManager.InputToGridDirection(input);

        if (direction == Vector3Int.zero)
            return;

        if (puzzleManager.TryPush(this, direction))
            nextPushTime = Time.time + pushCooldown;
    }

    public void Initialize(
        PuzzleManager manager,
        Vector3Int startingCell)
    {
        puzzleManager = manager;
        Cell = startingCell;
    }

    public void SetCell(Vector3Int cell)
    {
        Cell = cell;
    }

    public void SetMoving(bool moving)
    {
        IsMoving = moving;
    }
}