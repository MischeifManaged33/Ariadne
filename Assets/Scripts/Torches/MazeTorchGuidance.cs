using UnityEngine;
using UnityEngine.Tilemaps;

// One active generated dungeon. Updated by the generator's torch partial.
[DisallowMultipleComponent]
public class MazeTorchGuidance : MonoBehaviour
{
    public static MazeTorchGuidance Current { get; private set; }

    [Header("Distance Bands - Grid Cells")]
    [Tooltip("Distance from the rectangular floor footprint of the Minotaur room.")]
    [SerializeField, Min(0f)] private float closeDistance = 12f;
    [SerializeField, Min(0f)] private float nearbyDistance = 30f;

    public bool IsReady { get; private set; }
    private Vector2 origin, xStep, yStep;
    private float determinant;
    private Vector2 roomMin, roomMax;
    private bool refreshRequested;

    private void OnEnable()
    {
        Current = this;
        refreshRequested = true;
    }

    private void OnDisable()
    {
        if (Current != this) return;
        Current = null;
        MinotaurProximityTorch.RefreshAll(null);
    }

    private void Update()
    {
        if (!refreshRequested || Current != this) return;
        refreshRequested = false;
        MinotaurProximityTorch.RefreshAll(this);
    }

    // No room/torch is moved, repainted or respawned here.
    public void Configure(Tilemap dungeonFloor, RoomDefinition minotaurRoom)
    {
        IsReady = false;
        if (dungeonFloor == null || minotaurRoom == null || minotaurRoom.FloorTilemap == null)
        {
            MinotaurProximityTorch.RefreshAll(this);
            return;
        }
        BoundsInt bounds = minotaurRoom.FloorTilemap.cellBounds;
        if (bounds.size.x < 1 || bounds.size.y < 1)
        { MinotaurProximityTorch.RefreshAll(this); return; }
        origin = dungeonFloor.CellToWorld(Vector3Int.zero);
        xStep = (Vector2)dungeonFloor.CellToWorld(Vector3Int.right) - origin;
        yStep = (Vector2)dungeonFloor.CellToWorld(Vector3Int.up) - origin;
        determinant = xStep.x * yStep.y - xStep.y * yStep.x;
        if (Mathf.Abs(determinant) < 0.000001f)
        { MinotaurProximityTorch.RefreshAll(this); return; }

        Vector3Int min = minotaurRoom.ToDungeonCell(new Vector3Int(bounds.xMin, bounds.yMin, 0), dungeonFloor);
        Vector3Int max = minotaurRoom.ToDungeonCell(new Vector3Int(bounds.xMax - 1, bounds.yMax - 1, 0), dungeonFloor);
        roomMin = new Vector2(min.x, min.y);
        roomMax = new Vector2(max.x + 1f, max.y + 1f);
        IsReady = true;
        refreshRequested = false;
        if (isActiveAndEnabled && Current == this) MinotaurProximityTorch.RefreshAll(this);
    }

    // 0 = far, 1 = nearby, 2 = close. This is regional distance, not a route map.
    public int BandAt(Vector3 worldPosition)
    {
        if (!IsReady) return 0;
        Vector2 p = (Vector2)worldPosition - origin;
        Vector2 cell = new Vector2(
            (p.x * yStep.y - p.y * yStep.x) / determinant,
            (xStep.x * p.y - xStep.y * p.x) / determinant);
        float dx = Mathf.Max(Mathf.Max(roomMin.x - cell.x, 0f), cell.x - roomMax.x);
        float dy = Mathf.Max(Mathf.Max(roomMin.y - cell.y, 0f), cell.y - roomMax.y);
        float squared = dx * dx + dy * dy;
        float near = Mathf.Max(0f, closeDistance);
        float medium = Mathf.Max(near, nearbyDistance);
        if (squared <= near * near) return 2;
        if (squared <= medium * medium) return 1;
        return 0;
    }

    private void OnValidate()
    {
        closeDistance = Mathf.Max(0f, closeDistance);
        nearbyDistance = Mathf.Max(closeDistance, nearbyDistance);
        // OnValidate can run outside normal gameplay callbacks. Only request
        // the renderer refresh here; Update performs it on the main thread.
        refreshRequested = true;
    }
}
