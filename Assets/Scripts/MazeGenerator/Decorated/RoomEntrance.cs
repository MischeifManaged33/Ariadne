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

    [Tooltip("Doorway width in tile cells.")]
    [SerializeField, Min(1)] private int entranceWidth = 1;

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
        if (roomFloor == null || roomFloor.layoutGrid == null)
        {
            Debug.LogError(
                $"{name}: assign Room Floor and open the prefab " +
                "with its Grid active before snapping.",
                this
            );
            return;
        }

        Vector3 before = transform.position;
        Vector3Int cell = roomFloor.WorldToCell(before);

#if UNITY_EDITOR
    UnityEditor.Undo.RecordObject(
        transform,
        "Snap Room Entrance"
    );
#endif

        transform.position = roomFloor.GetCellCenterWorld(cell);

#if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(transform);

    UnityEditor.PrefabUtility
        .RecordPrefabInstancePropertyModifications(transform);
#endif

        Vector3Int resultingCell =
            roomFloor.WorldToCell(transform.position);

        Vector3 offset = transform.position -
            roomFloor.GetCellCenterWorld(resultingCell);

        Debug.Log(
            $"{name}: ENTRANCE SNAP\n" +
            $"Before: {before.ToString("F6")}\n" +
            $"After: {transform.position.ToString("F6")}\n" +
            $"Cell: {resultingCell}\n" +
            $"Remaining offset: {offset.ToString("F6")}",
            this
        );
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

    public System.Collections.Generic.List<Vector3Int>
    GetOutwardConnectionCells()
    {
        RoomDefinition room =
            GetComponentInParent<RoomDefinition>(true);

        if (room == null ||
            roomFloor == null ||
            room.FloorTilemap != roomFloor)
        {
            throw new System.InvalidOperationException(
                $"{name}: assign the same room floor as RoomDefinition."
            );
        }

        if (roomFloor.layoutGrid == null)
        {
            throw new System.InvalidOperationException(
                $"{name}: the room Grid must be active."
            );
        }

        if (Direction == Vector2Int.zero)
        {
            throw new System.InvalidOperationException(
                $"{name}: choose a valid outward direction."
            );
        }

        BoundsInt bounds = room.GetLocalBounds();

        RectInt routingBoundary = new RectInt(
            bounds.xMin - 1,
            bounds.yMin - 1,
            bounds.size.x + 2,
            bounds.size.y + 2
        );

        Vector3Int direction =
            new Vector3Int(Direction.x, Direction.y, 0);

        // Across the doorway, perpendicular to its outward direction.
        Vector3Int sideways =
            new Vector3Int(-direction.y, direction.x, 0);

        Vector3Int entranceCell =
            roomFloor.WorldToCell(transform.position);

        int width = Mathf.Max(1, entranceWidth);
        int firstOffset = -(width / 2);

        var connectionCells =
            new System.Collections.Generic.List<Vector3Int>();

        var centerLane =
            new System.Collections.Generic.List<Vector3Int>();

        for (int offset = firstOffset;
             offset < firstOffset + width;
             offset++)
        {
            Vector3Int position =
                entranceCell + sideways * offset;

            Vector3Int inwardCell = position - direction;

            // The full doorway width must already be authored.
            if (!roomFloor.HasTile(position) ||
                !roomFloor.HasTile(inwardCell) ||
                room.IsBlockedCell(position) ||
                room.IsBlockedCell(inwardCell))
            {
                throw new System.InvalidOperationException(
                    $"{name}: Entrance Width {width} does not fit " +
                    $"the doorway at {position}. Each doorway cell " +
                    "and its inward cell must contain clear floor."
                );
            }

            var laneCells =
                new System.Collections.Generic.List<Vector3Int>();

            bool leftRoomFloor = false;

            while (true)
            {
                position += direction;

                if (room.IsBlockedCell(position))
                {
                    throw new System.InvalidOperationException(
                        $"{name}: the entrance passage hits a wall " +
                        $"or obstacle at {position}. Reduce Entrance " +
                        "Width or widen the opening in the prefab."
                    );
                }

                bool hasFloor = roomFloor.HasTile(position);

                if (leftRoomFloor && hasFloor)
                {
                    throw new System.InvalidOperationException(
                        $"{name}: the outward passage enters another " +
                        $"part of the room at {position}."
                    );
                }

                if (!hasFloor)
                {
                    leftRoomFloor = true;
                    laneCells.Add(position);
                }

                Vector2Int cell2D =
                    new Vector2Int(position.x, position.y);

                if (!routingBoundary.Contains(cell2D))
                    break;
            }

            if (offset == 0)
                centerLane.AddRange(laneCells);
            else
                connectionCells.AddRange(laneCells);
        }

        // RoomCorridorBuilder uses the last cell as its routing start.
        // Keep that cell at the outward end of the center lane.
        connectionCells.AddRange(centerLane);

        return connectionCells;
    }

    [ContextMenu("Validate Placement")]
    private void ValidatePlacement()
    {
        if (roomFloor == null)
        {
            Debug.LogError(
                $"{name}: assign Room Floor.",
                this
            );
            return;
        }

        RoomDefinition room =
            GetComponentInParent<RoomDefinition>(true);

        if (room == null)
        {
            Debug.LogError(
                $"{name}: the entrance must be a child " +
                "of a GameObject with RoomDefinition.",
                this
            );
            return;
        }

        if (room.FloorTilemap != roomFloor)
        {
            Debug.LogError(
                $"{name}: Room Floor must match the " +
                "floor assigned to RoomDefinition.",
                this
            );
            return;
        }

        Vector3Int cell =
            roomFloor.WorldToCell(transform.position);

        Vector3 center =
            roomFloor.GetCellCenterWorld(cell);

        if ((transform.position - center)
            .sqrMagnitude > 0.000001f)
        {
            Debug.LogError(
                $"{name}: use Snap To Floor Cell first.",
                this
            );
            return;
        }

        Vector3Int direction =
            new Vector3Int(Direction.x, Direction.y, 0);

        Vector3Int inwardCell = cell - direction;
        Vector3Int outwardCell = cell + direction;

        if (!roomFloor.HasTile(cell))
        {
            Debug.LogError(
                $"{name}: doorway cell {cell} has no floor. " +
                "Move the marker onto a painted doorway tile.",
                this
            );
            return;
        }

        if (!roomFloor.HasTile(inwardCell))
        {
            Debug.LogError(
                $"{name}: inward cell {inwardCell} has no floor. " +
                "Check the outward direction or doorway position.",
                this
            );
            return;
        }

        try
        {
            GetOutwardConnectionCells();
        }
        catch (System.InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
            return;
        }

        Debug.Log(
            $"{name}: entrance placement is valid. " +
            $"Doorway: {cell}; inward: {inwardCell}; " +
            $"outward: {outwardCell}. " +
            "Also check that the doorway has no blocking collider.",
            this
        );
    }

    [ContextMenu("Snap To Nearest Valid Entrance")]
    private void SnapToNearestValidEntrance()
    {
        RoomDefinition room =
            GetComponentInParent<RoomDefinition>(true);

        if (room == null ||
            roomFloor == null ||
            room.FloorTilemap != roomFloor)
        {
            Debug.LogError(
                $"{name}: assign the same floor as RoomDefinition.",
                this
            );
            return;
        }

        Vector3 originalPosition = transform.position;
        Vector3 bestPosition = originalPosition;

        float bestDistance = float.PositiveInfinity;
        bool found = false;

        Vector3Int direction =
            new Vector3Int(Direction.x, Direction.y, 0);

        foreach (Vector3Int cell in
                 roomFloor.cellBounds.allPositionsWithin)
        {
            Vector3Int inwardCell = cell - direction;

            if (!roomFloor.HasTile(cell) ||
                !roomFloor.HasTile(inwardCell) ||
                room.IsBlockedCell(cell) ||
                room.IsBlockedCell(inwardCell))
            {
                continue;
            }

            Vector3 center =
                roomFloor.GetCellCenterWorld(cell);

            float distance =
                (center - originalPosition).sqrMagnitude;

            if (distance >= bestDistance)
                continue;

            try
            {
                transform.position = center;

                // Reject candidates whose outward passage
                // hits a wall or another part of the room.
                GetOutwardConnectionCells();

                bestPosition = center;
                bestDistance = distance;
                found = true;
            }
            catch (System.InvalidOperationException)
            {
                // This candidate cannot connect outward.
            }
            finally
            {
                transform.position = originalPosition;
            }
        }

        if (!found)
        {
            Debug.LogError(
                $"{name}: no valid entrance found for this direction. " +
                "Check Outward Direction and the assigned wall tilemaps.",
                this
            );
            return;
        }

        transform.position = bestPosition;
        ValidatePlacement();
    }
}