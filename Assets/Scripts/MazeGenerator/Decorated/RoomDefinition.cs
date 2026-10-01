using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomDefinition : MonoBehaviour
{
    [Header("Room References")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Transform placementAnchor;

    public Tilemap FloorTilemap => floorTilemap;
    public Transform PlacementAnchor => placementAnchor;

    public RoomEntrance[] Entrances =>
        GetComponentsInChildren<RoomEntrance>(true);

    public Vector3Int GetAnchorCell()
    {
        return floorTilemap.WorldToCell(
            placementAnchor.position
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

        Vector3 movement =
            destinationWorld - placementAnchor.position;

        transform.position += movement;
    }

    [ContextMenu("Snap Placement Anchor To Floor Cell")]
    private void SnapPlacementAnchor()
    {
        if (floorTilemap == null || placementAnchor == null)
            return;

        Vector3Int cell =
            floorTilemap.WorldToCell(
                placementAnchor.position
            );

        placementAnchor.position =
            floorTilemap.GetCellCenterWorld(cell);
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
}