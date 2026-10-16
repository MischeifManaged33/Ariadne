using System;
using System.Collections.Generic;
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
    [SerializeField]
    private EntranceDirection outwardDirection;

    [Tooltip("Doorway width in tile cells.")]
    [SerializeField, Min(1)]
    private int entranceWidth = 1;

    [Header("Saved Placement — Floor Cell Coordinates")]
    [SerializeField]
    private Vector3Int savedEntranceCell;

    [SerializeField, HideInInspector]
    private bool hasSavedEntranceCell;

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

    // Markers are authoring handles; saved cells control routing.
    public Vector3 WorldPosition =>
        roomFloor.GetCellCenterWorld(GetRoomCell());

    public Vector3Int GetRoomCell()
    {
        if (roomFloor == null || !hasSavedEntranceCell)
        {
            throw new InvalidOperationException(
                $"{name}: assign Room Floor and capture room cells " +
                "on RoomDefinition, then save the prefab."
            );
        }

        return savedEntranceCell;
    }

    private void RequireOwner(RoomDefinition room)
    {
        if (room == null ||
            roomFloor == null ||
            roomFloor != room.FloorTilemap ||
            GetComponentInParent<RoomDefinition>(true) != room)
        {
            throw new InvalidOperationException(
                $"{name}: Room Floor must match the parent " +
                "RoomDefinition's floor."
            );
        }

        if (transform == room.transform ||
            roomFloor.transform.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: an entrance must be a separate " +
                "marker that does not move the room floor."
            );
        }
    }

    public Vector3Int CellFromMarker(RoomDefinition room)
    {
        RequireOwner(room);

        return RoomDefinition.NearestFloorCell(
            roomFloor,
            transform.position
        );
    }

    public void SaveCell(Vector3Int cell)
    {
        RoomDefinition.RecordEditorObject(this);

        savedEntranceCell = cell;
        hasSavedEntranceCell = true;

        RoomDefinition.FinishEditorObject(this);
    }

    public void RestoreMarker(RoomDefinition room)
    {
        RequireOwner(room);

        Vector3 target =
            roomFloor.GetCellCenterWorld(GetRoomCell());

        RoomDefinition.RecordEditorObject(transform);
        transform.position = target;
        RoomDefinition.FinishEditorObject(transform);
    }

    public void ValidateSavedCell(RoomDefinition room)
    {
        RequireOwner(room);
        GetConnectionCellsFrom(GetRoomCell());
    }

    public List<Vector3Int> GetOutwardConnectionCells()
    {
        RequireOwner(
            GetComponentInParent<RoomDefinition>(true)
        );

        return GetConnectionCellsFrom(GetRoomCell());
    }

    private List<Vector3Int> GetConnectionCellsFrom(
        Vector3Int entranceCell)
    {
        RoomDefinition room =
            GetComponentInParent<RoomDefinition>(true);

        RequireOwner(room);

        if (roomFloor.layoutGrid == null ||
            Direction == Vector2Int.zero ||
            entranceCell.z != 0)
        {
            throw new InvalidOperationException(
                $"{name}: routing needs an active Grid, " +
                "valid direction and entrance cell Z = 0."
            );
        }

        BoundsInt bounds = room.GetLocalBounds();

        RectInt routingBoundary = new RectInt(
            bounds.xMin - 1,
            bounds.yMin - 1,
            bounds.size.x + 2,
            bounds.size.y + 2
        );

        if (!routingBoundary.Contains(new Vector2Int(
            entranceCell.x,
            entranceCell.y)))
        {
            throw new InvalidOperationException(
                $"{name}: saved entrance {entranceCell} " +
                "is outside the room bounds."
            );
        }

        Vector3Int direction = new Vector3Int(
            Direction.x,
            Direction.y,
            0
        );

        Vector3Int sideways = new Vector3Int(
            -direction.y,
            direction.x,
            0
        );

        int width = Mathf.Max(1, entranceWidth);
        int firstOffset = -(width / 2);

        List<Vector3Int> connectionCells =
            new List<Vector3Int>();

        List<Vector3Int> centerLane =
            new List<Vector3Int>();

        for (int offset = firstOffset;
             offset < firstOffset + width;
             offset++)
        {
            Vector3Int position =
                entranceCell + sideways * offset;

            Vector3Int inwardCell = position - direction;

            if (!roomFloor.HasTile(position) ||
                !roomFloor.HasTile(inwardCell) ||
                room.IsBlockedCell(position) ||
                room.IsBlockedCell(inwardCell))
            {
                throw new InvalidOperationException(
                    $"{room.name}/{name}: doorway width {width} " +
                    $"does not fit at {position}. Doorway and " +
                    "inward cells need clear floor."
                );
            }

            List<Vector3Int> laneCells =
                new List<Vector3Int>();

            bool leftRoomFloor = false;

            while (true)
            {
                position += direction;

                if (room.IsBlockedCell(position))
                {
                    throw new InvalidOperationException(
                        $"{room.name}/{name}: outward passage " +
                        $"hits a wall or obstacle at {position}."
                    );
                }

                bool hasFloor = roomFloor.HasTile(position);

                if (leftRoomFloor && hasFloor)
                {
                    throw new InvalidOperationException(
                        $"{room.name}/{name}: outward passage " +
                        "enters another part of the room " +
                        $"at {position}."
                    );
                }

                if (!hasFloor)
                {
                    leftRoomFloor = true;
                    laneCells.Add(position);
                }

                if (!routingBoundary.Contains(new Vector2Int(
                    position.x,
                    position.y)))
                {
                    break;
                }
            }

            if (offset == 0)
                centerLane.AddRange(laneCells);
            else
                connectionCells.AddRange(laneCells);
        }

        // Builder routes from the outward end of the center lane.
        connectionCells.AddRange(centerLane);

        if (centerLane.Count == 0)
        {
            throw new InvalidOperationException(
                $"{name}: entrance has no outward connection cells."
            );
        }

        return connectionCells;
    }

    [ContextMenu("Snap To Floor Cell")]
    private void SnapToFloorCell()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "Capture entrances outside Play Mode.",
                this
            );
            return;
        }

        try
        {
            RoomDefinition room =
                GetComponentInParent<RoomDefinition>(true);

            SaveCell(CellFromMarker(room));
            RestoreMarker(room);
            ValidatePlacement();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Validate Placement")]
    private void ValidatePlacement()
    {
        try
        {
            ValidateSavedCell(
                GetComponentInParent<RoomDefinition>(true)
            );

            Debug.Log(
                $"{name}: saved entrance {GetRoomCell()} " +
                "and its outward passage are valid.",
                this
            );
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Snap To Nearest Valid Entrance")]
    private void SnapToNearestValidEntrance()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "Edit entrances outside Play Mode.",
                this
            );
            return;
        }

        try
        {
            RoomDefinition room =
                GetComponentInParent<RoomDefinition>(true);

            RequireOwner(room);

            bool found = false;
            Vector3Int best = Vector3Int.zero;
            float bestDistance = float.PositiveInfinity;

            foreach (Vector3Int cell in
                     roomFloor.cellBounds.allPositionsWithin)
            {
                if (!roomFloor.HasTile(cell) || cell.z != 0)
                    continue;

                float distance = (
                    roomFloor.GetCellCenterWorld(cell) -
                    transform.position
                ).sqrMagnitude;

                if (distance >= bestDistance)
                    continue;

                try
                {
                    GetConnectionCellsFrom(cell);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                best = cell;
                bestDistance = distance;
                found = true;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"{name}: no valid entrance found " +
                    "for this direction and width."
                );
            }

            SaveCell(best);
            RestoreMarker(room);
            ValidatePlacement();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private void OnDrawGizmos()
    {
        if (roomFloor == null || roomFloor.layoutGrid == null)
            return;

        Vector3Int cell;

        try
        {
            cell = hasSavedEntranceCell
                ? savedEntranceCell
                : RoomDefinition.NearestFloorCell(
                    roomFloor,
                    transform.position
                );
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Vector3 start =
            roomFloor.GetCellCenterWorld(cell);

        Vector3 end =
            roomFloor.GetCellCenterWorld(
                cell + new Vector3Int(
                    Direction.x,
                    Direction.y,
                    0
                )
            );

        Gizmos.color = hasSavedEntranceCell
            ? Color.cyan
            : Color.yellow;

        Gizmos.DrawSphere(start, 0.06f);
        Gizmos.DrawLine(start, end);

        Vector3 arrow = (end - start) * 0.25f;
        Vector3 side = new Vector3(-arrow.y, arrow.x, 0f);

        Gizmos.DrawLine(end, end - arrow + side);
        Gizmos.DrawLine(end, end - arrow - side);
    }
}