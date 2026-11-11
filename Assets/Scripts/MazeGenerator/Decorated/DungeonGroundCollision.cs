using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Rebuilt with the dungeon, independently of raised/faded wall artwork.
[DisallowMultipleComponent]
public class DungeonGroundCollision : MonoBehaviour
{
    [Tooltip("Use a physics layer that collides with the player. Default matches the painter's back-wall bases.")]
    [SerializeField] private string collisionLayerName = "Default";

    [SerializeField, HideInInspector] private Tilemap generatedCollisionMap;
    [SerializeField, HideInInspector] private Tile collisionTile;

    public int CollisionCellCount { get; private set; }

    private static readonly Vector2Int[] neighbors =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    public void Rebuild(
        Tilemap dungeonFloor,
        HashSet<Vector2Int> corridorFloor,
        List<RoomDefinition> rooms)
    {
        if (dungeonFloor == null || dungeonFloor.layoutGrid == null ||
            corridorFloor == null || rooms == null)
            throw new ArgumentException("Ground collision requires the dungeon floor, corridors and placed rooms.");

        int physicsLayer = LayerMask.NameToLayer(collisionLayerName);
        if (physicsLayer < 0)
            throw new InvalidOperationException("Collision physics layer is missing: " + collisionLayerName);

        var walkable = new HashSet<Vector2Int>(corridorFloor);
        var roomBlockers = new HashSet<Vector2Int>();
        foreach (RoomDefinition room in rooms)
        {
            if (room == null || room.FloorTilemap == null)
                throw new InvalidOperationException("A generated room has no floor reference.");

            // Use the same logical cell mapping and obstacle definition as
            // entrance validation. Wall sprite elevation is irrelevant here.
            BoundsInt bounds = room.GetLocalBounds();
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    var local = new Vector3Int(x, y, 0);
                    bool blocked = room.IsBlockedCell(local);
                    bool hasFloor = room.FloorTilemap.HasTile(local);
                    if (!blocked && !hasFloor) continue;
                    Vector3Int mapped = room.ToDungeonCell(local, dungeonFloor);
                    var cell = new Vector2Int(mapped.x, mapped.y);
                    if (blocked) roomBlockers.Add(cell);
                    else walkable.Add(cell);
                }
            }
        }

        walkable.ExceptWith(roomBlockers);
        var collisionCells = new HashSet<Vector2Int>(roomBlockers);
        foreach (Vector2Int cell in corridorFloor)
        {
            foreach (Vector2Int offset in neighbors)
            {
                Vector2Int next = cell + offset;
                if (!walkable.Contains(next)) collisionCells.Add(next);
            }
        }

        // A real entrance must remain clear. Abort before replacing collision
        // if another room's blocker conflicts with its validated passage.
        foreach (RoomDefinition room in rooms)
        {
            foreach (RoomEntrance entrance in room.Entrances)
            {
                var passage = entrance.GetOutwardConnectionCells();
                passage.Add(entrance.GetRoomCell());
                foreach (Vector3Int local in passage)
                {
                    Vector3Int mapped = room.ToDungeonCell(local, dungeonFloor);
                    if (collisionCells.Contains(new Vector2Int(mapped.x, mapped.y)))
                        throw new InvalidOperationException(
                            room.name + "/" + entrance.name + ": a solid collision cell conflicts with its entrance.");
                }
            }
        }

        EnsureCollisionMap(dungeonFloor, physicsLayer);
        if (collisionTile == null)
        {
            collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.name = "Generated Ground Collision Tile";
            collisionTile.hideFlags = HideFlags.HideInHierarchy;
            collisionTile.colliderType = Tile.ColliderType.Grid;
            collisionTile.sprite = null;
        }

        var cells = new Vector3Int[collisionCells.Count];
        var tiles = new TileBase[collisionCells.Count];
        int index = 0;
        foreach (Vector2Int cell in collisionCells)
        {
            cells[index] = new Vector3Int(cell.x, cell.y, 0);
            tiles[index] = collisionTile;
            index++;
        }

        generatedCollisionMap.ClearAllTiles();
        generatedCollisionMap.SetTiles(cells, tiles);
        TilemapCollider2D collider = generatedCollisionMap.GetComponent<TilemapCollider2D>();
        collider.enabled = true;
        collider.isTrigger = false;
        collider.ProcessTilemapChanges();
        CollisionCellCount = collisionCells.Count;
        Physics2D.SyncTransforms();
    }

    private void EnsureCollisionMap(Tilemap floor, int physicsLayer)
    {
        if (generatedCollisionMap == null)
        {
            var created = new GameObject(
                "Generated Ground Collision", typeof(Tilemap), typeof(TilemapCollider2D));
            generatedCollisionMap = created.GetComponent<Tilemap>();
        }

        Transform collisionTransform = generatedCollisionMap.transform;
        collisionTransform.SetParent(floor.transform.parent, false);
        collisionTransform.localPosition = floor.transform.localPosition;
        collisionTransform.localRotation = floor.transform.localRotation;
        collisionTransform.localScale = floor.transform.localScale;
        generatedCollisionMap.tileAnchor = floor.tileAnchor;
        generatedCollisionMap.orientation = floor.orientation;
        generatedCollisionMap.orientationMatrix = floor.orientationMatrix;
        generatedCollisionMap.gameObject.layer = physicsLayer;
        generatedCollisionMap.gameObject.SetActive(isActiveAndEnabled);
        // No TilemapRenderer and no wall-fading script are added.
    }

    private void OnEnable()
    {
        if (generatedCollisionMap != null) generatedCollisionMap.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (generatedCollisionMap != null) generatedCollisionMap.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (generatedCollisionMap != null)
        {
            generatedCollisionMap.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(generatedCollisionMap.gameObject);
            else DestroyImmediate(generatedCollisionMap.gameObject);
        }
        if (collisionTile != null)
        {
            if (Application.isPlaying) Destroy(collisionTile);
            else DestroyImmediate(collisionTile);
        }
    }
}
