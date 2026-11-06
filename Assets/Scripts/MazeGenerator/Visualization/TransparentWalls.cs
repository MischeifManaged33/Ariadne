using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TransparentWalls : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer playerRenderer;

    [Header("Fade Settings")]
    [SerializeField, Range(0f, 1f)]
    private float fadeAlpha = 0f;

    [Header("Overlap")]
    [SerializeField, Min(0f)]
    private float fadeMargin = 0.3f;
    [SerializeField, Min(0f)]
    private float releaseMargin = 0.6f;
    // Used if there's no player renderer
    [SerializeField]
    private Vector2 fallbackPlayerSize = new Vector2(0.6f, 1.2f);

    /*
     * Each Tilemap needs its own collection because two Tilemaps
     * can have tiles at the same cell position.
     * Two sets are kept and swapped each frame
     */
    private Dictionary<Tilemap, HashSet<Vector3Int>>
        fadedCells = new();
    private Dictionary<Tilemap, HashSet<Vector3Int>>
        nextFadedCells = new();

    private readonly List<Tilemap> staleTilemaps = new();
    private Sprite[] usedSprites = new Sprite[16];

    private void Awake()
    {
        ResolvePlayerRenderer();
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        UpdateWallVisibility();
    }

    private void UpdateWallVisibility()
    {
        foreach (HashSet<Vector3Int> cells in nextFadedCells.Values)
            cells.Clear();

        ResolvePlayerRenderer();

        Rect body = PlayerRect();
        Rect fadeZone = Grow(body, fadeMargin);
        Rect releaseZone = Grow(body, Mathf.Max(fadeMargin, releaseMargin));

        foreach (Tilemap tilemap in TransparentWallTilemap.RegisteredTilemaps)
        {
            if (tilemap == null || !SortsAbovePlayer(tilemap))
                continue;

            if (!SpriteExtent(tilemap, out Rect extent))
                continue;

            Vector3 scale = tilemap.transform.lossyScale;
            Vector2 reachMin = releaseZone.min - Vector2.Scale(extent.max, scale);
            Vector2 reachMax = releaseZone.max - Vector2.Scale(extent.min, scale);

            BoundsInt window = CellWindow(tilemap, reachMin, reachMax);

            foreach (Vector3Int wallCell in window.allPositionsWithin)
            {
                Sprite sprite = tilemap.GetSprite(wallCell);
                if (sprite == null)
                    continue;

                Rect art = TileRect(tilemap, wallCell, sprite, scale);
                bool wasAlreadyFaded = IsWallFaded(tilemap, wallCell);

                if (art.Overlaps(fadeZone) || (wasAlreadyFaded && art.Overlaps(releaseZone)))
                {
                    ApplyFade(tilemap, wallCell);
                    AddWall(nextFadedCells, tilemap, wallCell);
                }
            }
        }

        RestoreWallsNotIn(nextFadedCells);

        (fadedCells, nextFadedCells) = (nextFadedCells, fadedCells);
    }

    private Rect PlayerRect()
    {
        if (playerRenderer != null)
        {
            Bounds bounds = playerRenderer.bounds;
            return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }

        Vector2 feet = player.position;
        return new Rect(feet.x - fallbackPlayerSize.x * 0.5f, feet.y, fallbackPlayerSize.x, fallbackPlayerSize.y);
    }

    private static Rect Grow(Rect rect, float margin)
    {
        return new Rect(rect.x - margin, rect.y - margin, rect.width + margin * 2f, rect.height + margin * 2f);
    }

    private static Rect TileRect(Tilemap tilemap, Vector3Int cell, Sprite sprite, Vector3 scale)
    {
        Vector3 local = tilemap.CellToLocalInterpolated(cell + tilemap.tileAnchor);
        local += (Vector3)tilemap.GetTransformMatrix(cell).GetColumn(3);

        Vector2 pivot = tilemap.transform.TransformPoint(local);
        Bounds bounds = sprite.bounds;
        Vector2 min = pivot + Vector2.Scale(bounds.min, scale);
        Vector2 size = Vector2.Scale(bounds.size, scale);

        return new Rect(min, size);
    }

    private bool SpriteExtent(Tilemap tilemap, out Rect extent)
    {
        extent = default;

        int count = tilemap.GetUsedSpritesCount();
        if (count == 0)
            return false;

        if (usedSprites.Length < count)
            usedSprites = new Sprite[Mathf.NextPowerOfTwo(count)];

        count = tilemap.GetUsedSpritesNonAlloc(usedSprites);

        bool any = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            if (usedSprites[i] == null)
                continue;

            Bounds bounds = usedSprites[i].bounds;

            min = any ? Vector2.Min(min, bounds.min) : (Vector2)bounds.min;
            max = any ? Vector2.Max(max, bounds.max) : (Vector2)bounds.max;
            any = true;
        }

        System.Array.Clear(usedSprites, 0, count);

        extent = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return any;
    }

    private void ResolvePlayerRenderer()
    {
        if (playerRenderer == null && player != null)
        {
            playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
        }
    }

    private bool SortsAbovePlayer(Tilemap tilemap)
    {
        if (playerRenderer == null || !tilemap.TryGetComponent(out TilemapRenderer wallRenderer))
            return true;

        return SortingLayer.GetLayerValueFromID(wallRenderer.sortingLayerID) >
               SortingLayer.GetLayerValueFromID(playerRenderer.sortingLayerID);
    }

    // Only check tiles around the player to reduce load
    private static BoundsInt CellWindow(Tilemap tilemap, Vector2 worldMin, Vector2 worldMax)
    {
        Vector3Int a = tilemap.WorldToCell(new Vector3(worldMin.x, worldMin.y, 0f));
        Vector3Int b = tilemap.WorldToCell(new Vector3(worldMax.x, worldMin.y, 0f));
        Vector3Int c = tilemap.WorldToCell(new Vector3(worldMin.x, worldMax.y, 0f));
        Vector3Int d = tilemap.WorldToCell(new Vector3(worldMax.x, worldMax.y, 0f));

        BoundsInt map = tilemap.cellBounds;

        int xMin = Mathf.Max(map.xMin, Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) - 1);
        int xMax = Mathf.Min(map.xMax, Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) + 2);
        int yMin = Mathf.Max(map.yMin, Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)) - 1);
        int yMax = Mathf.Min(map.yMax, Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)) + 2);

        if (xMax <= xMin || yMax <= yMin)
            return new BoundsInt(0, 0, 0, 0, 0, 0);

        return new BoundsInt(xMin, yMin, map.zMin, xMax - xMin, yMax - yMin, map.size.z);
    }

    private bool IsWallFaded(Tilemap tilemap, Vector3Int cell)
    {
        return fadedCells.TryGetValue(tilemap, out HashSet<Vector3Int> cells) && cells.Contains(cell);
    }

    private void AddWall (Dictionary<Tilemap, HashSet<Vector3Int>> collection, Tilemap tilemap, Vector3Int cell)
    {
        if (!collection.TryGetValue(tilemap, out HashSet<Vector3Int> cells))
        {
            cells = new HashSet<Vector3Int>();
            collection[tilemap] = cells;
        }

        cells.Add(cell);
    }

    private void ApplyFade (Tilemap tilemap, Vector3Int cell)
    {
        TileFlags flags = tilemap.GetTileFlags(cell);

        tilemap.SetTileFlags(cell, flags & ~TileFlags.LockColor);

        Color color = tilemap.GetColor(cell);
        color.a = fadeAlpha;

        tilemap.SetColor(cell, color);
    }

    private void RestoreWallsNotIn(Dictionary<Tilemap, HashSet<Vector3Int>> wallsToKeepFaded)
    {
        foreach (KeyValuePair<Tilemap, HashSet<Vector3Int>> entry in fadedCells)
        {
            Tilemap tilemap = entry.Key;

            if (tilemap == null)
            {
                staleTilemaps.Add(tilemap);
                continue;
            }

            wallsToKeepFaded.TryGetValue(tilemap, out HashSet<Vector3Int> kept);

            foreach (Vector3Int cell in entry.Value)
            {
                bool shouldStayFaded = kept != null && kept.Contains(cell);

                if (shouldStayFaded || !tilemap.HasTile(cell))
                    continue;
                Color color = tilemap.GetColor(cell);
                color.a = 1f;

                tilemap.SetColor(cell, color);
            }
        }
        foreach (Tilemap tilemap in staleTilemaps)
        {
            fadedCells.Remove(tilemap);
            nextFadedCells.Remove(tilemap);
        }

        staleTilemaps.Clear();
    }

    private void RestoreWalls()
    {
        foreach (
            KeyValuePair<Tilemap, HashSet<Vector3Int>> entry
            in fadedCells
        )
        {
            Tilemap tilemap = entry.Key;

            if (tilemap == null)
            {
                continue;
            }

            foreach (Vector3Int cell in entry.Value)
            {
                // The dungeon might have regenerated.
                if (!tilemap.HasTile(cell))
                {
                    continue;
                }

                Color normalColor = tilemap.GetColor(cell);
                normalColor.a = 1f;

                tilemap.SetColor(cell, normalColor);
            }
        }

        fadedCells.Clear();
        nextFadedCells.Clear();
    }

    private void OnDisable()
    {
        RestoreWalls();
    }
}
