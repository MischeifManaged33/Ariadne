using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GridPushBlock : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField]
    private Grid grid;

    [Header("Collision")]
    [Tooltip("Include the Walls and PushBlock layers")]
    [SerializeField]
    private LayerMask blockingLayers;

    [Header("Movement")]
    [SerializeField]
    private float moveDuration = 0.15f;
    [SerializeField]
    private float pushCooldown = 0.1f;

    private Rigidbody2D rb;
    private Collider2D blockCollider;
    private bool isMoving;
    private float nextPushTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        blockCollider = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (grid == null)
            grid = GetComponentInParent<Grid>();

        SnapToGrid();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();

        if (playerController == null || isMoving)
            return;
        if (Time.time < nextPushTime)
            return;

        Vector2 input = playerController.CurrentInput;

        if (input.sqrMagnitude < 0.1f)
            return;

        Vector3Int gridDirection = GetGridDirection(input);

        if (gridDirection == Vector3Int.zero)
            return;

        TryPush(gridDirection);
    }

    private void TryPush(Vector3Int direction)
    {
        Vector3Int currentCell = grid.WorldToCell(transform.position);
        Vector3Int targetCell = currentCell + direction;
        Vector3 targetPosition = grid.GetCellCenterWorld(targetCell);

        if (IsCellBlocked(targetPosition))
            return;

        nextPushTime = Time.time + pushCooldown;
        StartCoroutine(MoveToCell(targetPosition));
    }

    private bool IsCellBlocked(Vector3 targetPosition)
    {
        Vector2 checkSize = blockCollider.bounds.size * 0.8f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, checkSize, 0f, blockingLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit != blockCollider)
                return true;
        }

        return false;
    }

    private IEnumerator MoveToCell(Vector3 targetPosition)
    {
        isMoving = true;

        Vector2 start = rb.position;
        Vector2 target = targetPosition;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float amount = Mathf.Clamp01(elapsed / moveDuration);
            rb.MovePosition(Vector3.Lerp(start, target, amount));
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(target);
        isMoving = false;
    }

    private Vector3Int GetGridDirection(Vector2 input)
    {
        Vector3 right = GetWorldDirection(Vector3Int.right).normalized;
        Vector3 left = -right;

        Vector3 up = GetWorldDirection(Vector3Int.up).normalized;
        Vector3 down = -up;

        float bestDot = 0.5f;
        Vector3Int bestDirection = Vector3Int.zero;

        CheckDirection(input, right, Vector3Int.right, ref  bestDot, ref bestDirection);
        CheckDirection(input, left, Vector3Int.left, ref bestDot, ref bestDirection);
        CheckDirection(input, up, Vector3Int.up, ref bestDot, ref bestDirection);
        CheckDirection(input, down, Vector3Int.down, ref bestDot, ref bestDirection);

        return bestDirection;
    }

    private static void CheckDirection(Vector2 input,
        Vector2 worldDirection,
        Vector3Int gridDirection,
        ref float bestDot,
        ref Vector3Int bestDirection)
    {
        float dot = Vector2.Dot(input.normalized, worldDirection.normalized);

        if(dot > bestDot)
        {
            bestDot = dot;
            bestDirection = gridDirection;
        }
    }

    private Vector3 GetWorldDirection(Vector3Int gridDirection)
    {
        Vector3 center = grid.GetCellCenterWorld(Vector3Int.zero);
        Vector3 neighbor = grid.GetCellCenterWorld(gridDirection);

        return neighbor - center;
    }

    private void SnapToGrid()
    {
        if(grid == null)
        {
            Debug.LogError($"{name} could not find a Grid component");
            return;
        }

        Vector3Int cell = grid.WorldToCell(transform.position);
        transform.position = grid.GetCellCenterWorld(cell);
    }
}
