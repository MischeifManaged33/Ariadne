using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomDefinition : MonoBehaviour
{
    [Header("Room References")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Transform placementAnchor;

    [SerializeField, HideInInspector]
    private Vector3Int savedAnchorCell;

    [SerializeField, HideInInspector]
    private bool hasSavedAnchorCell;

    public Tilemap FloorTilemap => floorTilemap;
    public Transform PlacementAnchor => placementAnchor;

    public RoomEntrance[] Entrances =>
        GetComponentsInChildren<RoomEntrance>(true);

    [Header("Corridor Obstacles")]
    [SerializeField]
    private Tilemap[] blockingTilemaps =
    new Tilemap[0];

    public bool IsBlockedCell(Vector3Int cell)
    {
        foreach (Tilemap tilemap in blockingTilemaps)
        {
            if (tilemap != null && tilemap.HasTile(cell))
                return true;
        }

        return false;
    }

    public Vector3Int GetAnchorCell()
    {
        if (floorTilemap == null || placementAnchor == null)
        {
            throw new System.InvalidOperationException(
                $"{name}: assign Floor Tilemap and Placement Anchor."
            );
        }

        // Scene instances have a working Grid connection.
        if (gameObject.scene.IsValid() &&
            floorTilemap.layoutGrid != null)
        {
            return floorTilemap.WorldToCell(
                placementAnchor.position
            );
        }

        // Prefab assets use the cell recorded during authoring.
        if (!hasSavedAnchorCell)
        {
            throw new System.InvalidOperationException(
                $"{name}: open this room in Prefab Mode and run " +
                "'Save Placement Anchor Cell', then save the prefab."
            );
        }

        return savedAnchorCell;
    }

    [ContextMenu("Save Placement Anchor Cell")]
    private void SavePlacementAnchorCell()
    {
        if (floorTilemap == null ||
            placementAnchor == null ||
            floorTilemap.layoutGrid == null ||
            !gameObject.scene.IsValid())
        {
            Debug.LogError(
                $"{name}: open the room in Prefab Mode with its " +
                "Grid active before saving the anchor cell.",
                this
            );
            return;
        }

        Vector3Int cell =
            floorTilemap.WorldToCell(placementAnchor.position);

        Vector3 center =
            floorTilemap.GetCellCenterWorld(cell);

        if ((placementAnchor.position - center).sqrMagnitude > 0.000001f)
        {
            Debug.LogError(
                $"{name}: snap the placement anchor first.",
                this
            );
            return;
        }

#if UNITY_EDITOR
    UnityEditor.Undo.RecordObject(
        this,
        "Save Placement Anchor Cell"
    );
#endif

        savedAnchorCell = cell;
        hasSavedAnchorCell = true;

#if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(this);
    UnityEditor.PrefabUtility
        .RecordPrefabInstancePropertyModifications(this);
#endif

        Debug.Log(
            $"{name}: saved placement anchor cell {savedAnchorCell}.",
            this
        );
    }

    // Measures all painted tilemaps, including the room's walls.
    // Returned coordinates are room tile coordinates.
    public BoundsInt GetLocalBounds()
    {
        Tilemap[] tilemaps =
            GetComponentsInChildren<Tilemap>(true);

        Vector3Int minimum = Vector3Int.zero;
        Vector3Int maximum = Vector3Int.zero;

        bool foundTile = false;

        foreach (Tilemap tilemap in tilemaps)
        {
            foreach (Vector3Int cell in
                     tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;

                if (!foundTile)
                {
                    minimum = cell;
                    maximum = cell;
                    foundTile = true;
                }
                else
                {
                    minimum = Vector3Int.Min(minimum, cell);
                    maximum = Vector3Int.Max(maximum, cell);
                }
            }
        }

        if (!foundTile)
        {
            Debug.LogError(
                $"{name}: the room contains no painted tiles.",
                this
            );

            return new BoundsInt();
        }

        // BoundsInt's maximum is exclusive.
        return new BoundsInt(
            minimum,
            maximum - minimum + Vector3Int.one
        );
    }

    // Moves the entire room so its anchor matches a dungeon cell.
    public void PlaceAt(
        Tilemap dungeonFloor,
        Vector2Int destinationCell)
    {
        if (dungeonFloor == null ||
            floorTilemap == null ||
            placementAnchor == null)
        {
            Debug.LogError(
                $"{name}: missing a floor tilemap or placement anchor.",
                this
            );

            return;
        }

        Vector3 destinationWorld =
            dungeonFloor.GetCellCenterWorld(
                new Vector3Int(
                    destinationCell.x,
                    destinationCell.y,
                    0
                )
            );

        PrintGridDiagnostics();

        LogAlignment("BEFORE placement");

        Vector3 movement =
            destinationWorld - placementAnchor.position;

        transform.position += movement;

        LogAlignment("AFTER placement"); LogAlignment("AFTER placement");
    }

    [ContextMenu("Print Grid Diagnostics")]
    private void PrintGridDiagnostics()
    {
        if (floorTilemap == null)
        {
            Debug.LogError("Floor Tilemap is missing.", this);
            return;
        }

        Grid parentGrid =
            floorTilemap.GetComponentInParent<Grid>(true);

        Grid linkedGrid = floorTilemap.layoutGrid;

        Vector3 origin =
            floorTilemap.GetCellCenterWorld(Vector3Int.zero);

        Vector3 xStep =
            floorTilemap.GetCellCenterWorld(Vector3Int.right)
            - origin;

        Vector3 yStep =
            floorTilemap.GetCellCenterWorld(Vector3Int.up)
            - origin;

        string message =
            $"{name}: GRID DIAGNOSTICS\n" +
            $"Parent Grid: " +
            $"{(parentGrid != null ? parentGrid.name : "MISSING")}\n" +
            $"Linked Grid: " +
            $"{(linkedGrid != null ? linkedGrid.name : "MISSING")}\n" +
            $"World X step: {xStep.ToString("F6")}\n" +
            $"World Y step: {yStep.ToString("F6")}\n" +
            $"Floor world scale: {floorTilemap.transform.lossyScale}";

        if (parentGrid != null)
        {
            message +=
                $"\nCell Layout: {parentGrid.cellLayout}" +
                $"\nCell Size: {parentGrid.cellSize}" +
                $"\nCell Gap: {parentGrid.cellGap}" +
                $"\nCell Swizzle: {parentGrid.cellSwizzle}";
        }

        Debug.Log(message, this);
    }

    [ContextMenu("Snap Placement Anchor To Floor Cell")]
    private void SnapPlacementAnchor()
    {
        if (floorTilemap == null || placementAnchor == null)
        {
            Debug.LogError(
                $"{name}: assign Floor Tilemap and Placement Anchor.",
                this
            );
            return;
        }

        if (floorTilemap.layoutGrid == null)
        {
            Debug.LogError(
                $"{name}: snapping cannot run because the floor " +
                "has no linked Grid. Use an active room instance " +
                "in the scene, then apply the change to the prefab.",
                this
            );
            return;
        }

        if (placementAnchor == transform ||
            floorTilemap.transform.IsChildOf(placementAnchor))
        {
            Debug.LogError(
                $"{name}: Placement Anchor must be a separate " +
                "child marker. Moving it must not move the floor.",
                this
            );
            return;
        }

        Vector3 before = placementAnchor.position;
        Vector3Int cell = floorTilemap.WorldToCell(before);
        Vector3 target = floorTilemap.GetCellCenterWorld(cell);

#if UNITY_EDITOR
    UnityEditor.Undo.RecordObject(
        placementAnchor,
        "Snap Room Placement Anchor"
    );
#endif

        placementAnchor.position = target;

#if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(placementAnchor);

    UnityEditor.PrefabUtility
        .RecordPrefabInstancePropertyModifications(placementAnchor);
#endif

        Vector3Int resultingCell =
            floorTilemap.WorldToCell(placementAnchor.position);

        Vector3 offset = placementAnchor.position -
            floorTilemap.GetCellCenterWorld(resultingCell);

        Debug.Log(
            $"{name}: ANCHOR SNAP\n" +
            $"Before: {before.ToString("F6")}\n" +
            $"After: {placementAnchor.position.ToString("F6")}\n" +
            $"Cell: {resultingCell}\n" +
            $"Remaining offset: {offset.ToString("F6")}",
            this
        );
    }

    [ContextMenu("Print Room Information")]
    private void PrintRoomInformation()
    {
        if (floorTilemap == null || placementAnchor == null)
        {
            Debug.LogError(
                $"{name}: assign Floor Tilemap and Placement Anchor.",
                this
            );

            return;
        }

        BoundsInt bounds = GetLocalBounds();

        Debug.Log(
            $"{name}: " +
            $"size {bounds.size.x} × {bounds.size.y} cells, " +
            $"anchor cell {GetAnchorCell()}, " +
            $"{Entrances.Length} entrances.",
            this
        );
    }

    private void LogAlignment(string stage)
    {
        Vector3Int anchorCell = GetAnchorCell();

        Vector3 anchorOffset =
            placementAnchor.position -
            floorTilemap.GetCellCenterWorld(anchorCell);

        string message =
            $"{name} — {stage}\n" +
            $"Floor belongs to room: " +
            $"{floorTilemap.transform.IsChildOf(transform)}\n" +
            $"Anchor belongs to room: " +
            $"{placementAnchor.IsChildOf(transform)}\n" +
            $"Tile Anchor: {floorTilemap.tileAnchor}\n" +
            $"Anchor offset: {anchorOffset.ToString("F6")}";

        foreach (RoomEntrance entrance in Entrances)
        {
            Vector3Int cell =
                floorTilemap.WorldToCell(entrance.WorldPosition);

            Vector3 offset =
                entrance.WorldPosition -
                floorTilemap.GetCellCenterWorld(cell);

            message +=
                $"\n{entrance.name} offset: {offset.ToString("F6")}";
        }

        Debug.Log(message, this);
    }
}