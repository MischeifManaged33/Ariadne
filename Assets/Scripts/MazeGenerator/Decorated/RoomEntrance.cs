using UnityEngine;
using UnityEngine.Tilemaps;

public enum EntranceDirection
{
    North,
    East,
    South,
    West
}

public class RoomEntrance : MonoBehaviour
{
    [Header("Room References")]
    [SerializeField] private Tilemap roomFloor;

    [Header("Entrance")]
    [SerializeField] private EntranceDirection outwardDirection;

    public Vector2Int Direction
    {
        get
        {
            switch (outwardDirection)
            {
                case EntranceDirection.North:
                    return Vector2Int.up;

                case EntranceDirection.East:
                    return Vector2Int.right;

                case EntranceDirection.South:
                    return Vector2Int.down;

                case EntranceDirection.West:
                    return Vector2Int.left;

                default:
                    return Vector2Int.zero;
            }
        }
    }

    public Vector3 WorldPosition => transform.position;

    public Vector3Int GetRoomCell()
    {
        if (roomFloor == null)
        {
            Debug.LogError(
                $"{name}: assign the room's floor tilemap.",
                this
            );

            return Vector3Int.zero;
        }

        return roomFloor.WorldToCell(transform.position);
    }

    [ContextMenu("Snap To Floor Cell")]
    private void SnapToFloorCell()
    {
        if (roomFloor == null)
            return;

        Vector3Int cell =
            roomFloor.WorldToCell(transform.position);

        transform.position =
            roomFloor.GetCellCenterWorld(cell);
    }

    private void OnDrawGizmos()
    {
        if (roomFloor == null)
            return;

        Vector3Int entranceCell =
            roomFloor.WorldToCell(transform.position);

        Vector3Int outsideCell = entranceCell +
            new Vector3Int(Direction.x, Direction.y, 0);

        Vector3 start =
            roomFloor.GetCellCenterWorld(entranceCell);

        Vector3 end =
            roomFloor.GetCellCenterWorld(outsideCell);

        Gizmos.color = Color.cyan;

        Gizmos.DrawSphere(start, 0.06f);
        Gizmos.DrawLine(start, end);

        Vector3 arrow = (end - start) * 0.25f;
        Vector3 side = new Vector3(-arrow.y, arrow.x, 0f);

        Gizmos.DrawLine(end, end - arrow + side);
        Gizmos.DrawLine(end, end - arrow - side);
    }
}