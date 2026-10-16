using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomDefinition : MonoBehaviour
{
    [Header("Room References")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Transform placementAnchor;

    // Keep existing serialized field names.
    [SerializeField, HideInInspector]
    private Vector3Int savedAnchorCell;

    [SerializeField, HideInInspector]
    private bool hasSavedAnchorCell;

    [Header("Corridor Obstacles")]
    [SerializeField]
    private Tilemap[] blockingTilemaps = new Tilemap[0];

    [Header("Player Spawn")]
    [SerializeField] private Transform playerSpawnPoint;

    public Tilemap FloorTilemap => floorTilemap;
    public Transform PlacementAnchor => placementAnchor;
    public Transform PlayerSpawnPoint => playerSpawnPoint;

    public RoomEntrance[] Entrances =>
        GetComponentsInChildren<RoomEntrance>(true);

    private Tilemap placedDungeonFloor;
    private Vector3Int placedDungeonAnchor;
    private bool isPlaced;

    private const float Tolerance = 0.001f;

    public Vector3Int GetAnchorCell()
    {
        if (!hasSavedAnchorCell)
        {
            throw new InvalidOperationException(
                $"{name}: open the prefab and run " +
                "Capture Room Cells From Markers, then save it."
            );
        }

        return savedAnchorCell;
    }

    public bool IsBlockedCell(Vector3Int cell)
    {
        if (blockingTilemaps == null)
            return false;

        foreach (Tilemap tilemap in blockingTilemaps)
        {
            if (tilemap != null && tilemap.HasTile(cell))
                return true;
        }

        return false;
    }

    public BoundsInt GetLocalBounds()
    {
        bool found = false;

        Vector3Int minimum = Vector3Int.zero;
        Vector3Int maximum = Vector3Int.zero;

        foreach (Tilemap tilemap in
                 GetComponentsInChildren<Tilemap>(true))
        {
            foreach (Vector3Int cell in
                     tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;

                if (!found)
                {
                    minimum = maximum = cell;
                    found = true;
                }
                else
                {
                    minimum = Vector3Int.Min(minimum, cell);
                    maximum = Vector3Int.Max(maximum, cell);
                }
            }
        }

        if (!found)
        {
            throw new InvalidOperationException(
                $"{name}: the room contains no painted tiles."
            );
        }

        return new BoundsInt(
            minimum,
            maximum - minimum + Vector3Int.one
        );
    }

    public void PlaceAt(
        Tilemap dungeonFloor,
        Vector2Int destinationCell)
    {
        isPlaced = false;

        ValidateFloorCompatibility(dungeonFloor);

        Vector3Int anchorCell = GetAnchorCell();

        foreach (RoomEntrance entrance in Entrances)
            entrance.ValidateSavedCell(this);

        Vector3Int destination = new Vector3Int(
            destinationCell.x,
            destinationCell.y,
            0
        );

        // Align the saved floor cell instead of a marker Transform.
        transform.position +=
            dungeonFloor.GetCellCenterWorld(destination) -
            floorTilemap.GetCellCenterWorld(anchorCell);

        placedDungeonFloor = dungeonFloor;
        placedDungeonAnchor = destination;
        isPlaced = true;

        RestoreMarkers();
    }

    public Vector3Int ToDungeonCell(
        Vector3Int roomCell,
        Tilemap dungeonFloor)
    {
        if (!isPlaced || placedDungeonFloor != dungeonFloor)
        {
            throw new InvalidOperationException(
                $"{name}: call PlaceAt before mapping room cells."
            );
        }

        if (roomCell.z != 0)
        {
            throw new InvalidOperationException(
                $"{name}: routing requires room floor cells on Z = 0."
            );
        }

        Vector3Int result =
            placedDungeonAnchor + roomCell - GetAnchorCell();

        Vector3 actual =
            floorTilemap.GetCellCenterWorld(roomCell);

        Vector3 expected =
            dungeonFloor.GetCellCenterWorld(result);

        if ((actual - expected).sqrMagnitude >
            Tolerance * Tolerance)
        {
            throw new InvalidOperationException(
                $"{name}: the floor grid moved after placement. " +
                $"Room cell {roomCell}, dungeon cell {result}, " +
                $"world difference {(actual - expected).ToString("F6")}."
            );
        }

        return result;
    }

    public void ValidateFloorCompatibility(Tilemap dungeonFloor)
    {
        if (floorTilemap == null ||
            dungeonFloor == null ||
            placementAnchor == null)
        {
            throw new InvalidOperationException(
                $"{name}: assign Floor Tilemap, " +
                "Placement Anchor and Dungeon Floor."
            );
        }

        if (!floorTilemap.transform.IsChildOf(transform) ||
            placementAnchor == transform ||
            !placementAnchor.IsChildOf(transform) ||
            floorTilemap.transform.IsChildOf(placementAnchor))
        {
            throw new InvalidOperationException(
                $"{name}: floor and placement anchor must belong " +
                "to this room; the anchor must be a separate " +
                "marker that does not move the floor."
            );
        }

        Grid roomGrid = floorTilemap.layoutGrid;
        Grid dungeonGrid = dungeonFloor.layoutGrid;

        if (roomGrid == null || dungeonGrid == null)
        {
            throw new InvalidOperationException(
                $"{name}: both floor Tilemaps need a linked Grid."
            );
        }

        if (roomGrid.cellLayout != dungeonGrid.cellLayout)
        {
            throw new InvalidOperationException(
                $"{name}: Grid Cell Layout differs. " +
                $"Room: {roomGrid.cellLayout}; " +
                $"dungeon: {dungeonGrid.cellLayout}."
            );
        }

        if (roomGrid.cellSwizzle != dungeonGrid.cellSwizzle)
        {
            throw new InvalidOperationException(
                $"{name}: Grid Cell Swizzle differs. " +
                $"Room: {roomGrid.cellSwizzle}; " +
                $"dungeon: {dungeonGrid.cellSwizzle}."
            );
        }

        RequireSame(
            "Grid Cell Size",
            roomGrid.cellSize,
            dungeonGrid.cellSize
        );

        RequireSame(
            "Grid Cell Gap",
            roomGrid.cellGap,
            dungeonGrid.cellGap
        );

        RequireSame(
            "Floor Tile Anchor",
            floorTilemap.tileAnchor,
            dungeonFloor.tileAnchor
        );

        Vector3 roomOrigin =
            floorTilemap.GetCellCenterWorld(Vector3Int.zero);

        Vector3 dungeonOrigin =
            dungeonFloor.GetCellCenterWorld(Vector3Int.zero);

        RequireSame(
            "world X cell step (check Grid/parent scale and rotation)",
            floorTilemap.GetCellCenterWorld(Vector3Int.right)
                - roomOrigin,
            dungeonFloor.GetCellCenterWorld(Vector3Int.right)
                - dungeonOrigin,
            Tolerance
        );

        RequireSame(
            "world Y cell step (check Grid/parent scale and rotation)",
            floorTilemap.GetCellCenterWorld(Vector3Int.up)
                - roomOrigin,
            dungeonFloor.GetCellCenterWorld(Vector3Int.up)
                - dungeonOrigin,
            Tolerance
        );

        if (GetAnchorCell().z != 0)
        {
            throw new InvalidOperationException(
                $"{name}: save the placement anchor " +
                "on floor layer Z = 0."
            );
        }
    }

    private void RequireSame(
        string setting,
        Vector3 roomValue,
        Vector3 dungeonValue,
        float tolerance = 0.000001f)
    {
        if ((roomValue - dungeonValue).sqrMagnitude >
            tolerance * tolerance)
        {
            throw new InvalidOperationException(
                $"{name}: {setting} differs. " +
                $"Room: {roomValue.ToString("F6")}; " +
                $"dungeon: {dungeonValue.ToString("F6")}."
            );
        }
    }

    // Find the nearest logical cell center in a planar floor grid.
    public static Vector3Int NearestFloorCell(
        Tilemap floor,
        Vector3 world)
    {
        if (floor == null || floor.layoutGrid == null)
        {
            throw new InvalidOperationException(
                "Open the prefab with an active Grid " +
                "and assign its floor."
            );
        }

        Vector3 origin =
            floor.GetCellCenterWorld(Vector3Int.zero);

        Vector3 x =
            floor.GetCellCenterWorld(Vector3Int.right) - origin;

        Vector3 y =
            floor.GetCellCenterWorld(Vector3Int.up) - origin;

        Vector3 delta = world - origin;

        float xx = Vector3.Dot(x, x);
        float xy = Vector3.Dot(x, y);
        float yy = Vector3.Dot(y, y);

        float determinant = xx * yy - xy * xy;

        if (determinant <= Mathf.Epsilon * xx * yy)
        {
            throw new InvalidOperationException(
                "Floor grid has degenerate cell axes; check its scale."
            );
        }

        float dx = Vector3.Dot(delta, x);
        float dy = Vector3.Dot(delta, y);

        int cx = Mathf.RoundToInt(
            (dx * yy - dy * xy) / determinant
        );

        int cy = Mathf.RoundToInt(
            (dy * xx - dx * xy) / determinant
        );

        Vector3Int best = new Vector3Int(cx, cy, 0);
        float bestDistance = float.PositiveInfinity;

        for (int a = cx - 1; a <= cx + 1; a++)
        {
            for (int b = cy - 1; b <= cy + 1; b++)
            {
                Vector3Int candidate = new Vector3Int(a, b, 0);

                float distance = (
                    floor.GetCellCenterWorld(candidate) - world
                ).sqrMagnitude;

                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    [ContextMenu("Capture Room Cells From Markers")]
    private void CaptureRoomCells()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "Capture room cells outside Play Mode.",
                this
            );
            return;
        }

        try
        {
            if (floorTilemap == null ||
                placementAnchor == null ||
                placementAnchor == transform ||
                floorTilemap.transform.IsChildOf(placementAnchor) ||
                !placementAnchor.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    "Assign a room floor and a separate " +
                    "child placement marker."
                );
            }

            Vector3Int anchor = NearestFloorCell(
                floorTilemap,
                placementAnchor.position
            );

            RoomEntrance[] entrances = Entrances;
            Vector3Int[] cells =
                new Vector3Int[entrances.Length];

            // Resolve all marker cells before changing saved data.
            for (int i = 0; i < entrances.Length; i++)
                cells[i] = entrances[i].CellFromMarker(this);

            RecordEditorObject(this);

            savedAnchorCell = anchor;
            hasSavedAnchorCell = true;

            for (int i = 0; i < entrances.Length; i++)
                entrances[i].SaveCell(cells[i]);

            RestoreMarkers();
            FinishEditorObject(this);

            Debug.Log(
                $"{name}: saved anchor {anchor} and " +
                $"{entrances.Length} entrance cells. Save the prefab.",
                this
            );
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Restore Markers From Saved Cells")]
    private void RestoreMarkersFromSavedCells()
    {
        try
        {
            RestoreMarkers();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private void RestoreMarkers()
    {
        if (floorTilemap == null || placementAnchor == null)
        {
            throw new InvalidOperationException(
                $"{name}: assign floor and placement marker."
            );
        }

        RecordEditorObject(placementAnchor);

        placementAnchor.position =
            floorTilemap.GetCellCenterWorld(GetAnchorCell());

        FinishEditorObject(placementAnchor);

        foreach (RoomEntrance entrance in Entrances)
            entrance.RestoreMarker(this);
    }

    // Retain the old authoring menu entries.
    [ContextMenu("Save Placement Anchor Cell")]
    private void SavePlacementAnchorCell() =>
        CaptureRoomCells();

    [ContextMenu("Snap Placement Anchor To Floor Cell")]
    private void SnapPlacementAnchor() =>
        CaptureRoomCells();

    [ContextMenu("Print Room Information")]
    private void PrintRoomInformation()
    {
        try
        {
            Debug.Log(
                $"{name}: bounds {GetLocalBounds()}, " +
                $"saved anchor {GetAnchorCell()}, " +
                $"entrances {Entrances.Length}.",
                this
            );
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Print Grid Diagnostics")]
    private void PrintGridDiagnostics()
    {
        if (floorTilemap == null ||
            floorTilemap.layoutGrid == null)
        {
            return;
        }

        Grid grid = floorTilemap.layoutGrid;

        Vector3 origin =
            floorTilemap.GetCellCenterWorld(Vector3Int.zero);

        Vector3 xStep =
            floorTilemap.GetCellCenterWorld(Vector3Int.right)
            - origin;

        Vector3 yStep =
            floorTilemap.GetCellCenterWorld(Vector3Int.up)
            - origin;

        Debug.Log(
            $"{name}: layout {grid.cellLayout}, " +
            $"swizzle {grid.cellSwizzle}\n" +
            $"Cell Size {grid.cellSize}, Gap {grid.cellGap}, " +
            $"Anchor {floorTilemap.tileAnchor}\n" +
            $"X step {xStep.ToString("F6")}\n" +
            $"Y step {yStep.ToString("F6")}",
            this
        );
    }

    public static void RecordEditorObject(UnityEngine.Object value)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(
                value,
                "Edit Room Cells"
            );
        }
#endif
    }

    public static void FinishEditorObject(UnityEngine.Object value)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(value);

            UnityEditor.PrefabUtility
                .RecordPrefabInstancePropertyModifications(value);
        }
#endif
    }
}