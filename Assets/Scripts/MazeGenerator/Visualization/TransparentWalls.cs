using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TransparentWalls : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera mainCamera;

    // Add every wall Tilemap to this array.
    [SerializeField] private Tilemap[] wallTilemaps;

    [Header("Fade Settings")]
    [SerializeField, Range(0f, 1f)]
    private float fadeAlpha = 0f;

    [Header("Visibility Buffer")]
    [SerializeField, Min(0f)]
    private float foregroundBufferPixels = 100f;
    [SerializeField, Min(0f)]
    private float foregroundDepthPixels = 180f;

    /*
     * Each Tilemap needs its own collection because two Tilemaps
     * can have tiles at the same cell position.
     */
    private Dictionary<Tilemap, HashSet<Vector3Int>>
        fadedCells = new();

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {

        if (player == null || wallTilemaps == null)
        {
            return;
        }

        UpdateWallVisibility();
    }

    private void UpdateWallVisibility()
    {
        Vector3 playerScreenPosition = mainCamera.WorldToScreenPoint(player.position);

        Dictionary<Tilemap, HashSet<Vector3Int>> wallsToKeepFaded = new Dictionary<Tilemap, HashSet<Vector3Int>>();

        foreach(Tilemap tilemap in wallTilemaps)
        {
            if (tilemap == null)
                continue;

            foreach (Vector3Int wallCell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(wallCell))
                    continue;

                Vector3 wallWorldPosition = tilemap.GetCellCenterWorld(wallCell);
                Vector3 wallScreenPosition = mainCamera.WorldToScreenPoint(wallWorldPosition);
                float distanceInFront = playerScreenPosition.y - wallScreenPosition.y;
                bool wasAlreadyFaded = IsWallFaded(tilemap, wallCell);

                bool shouldStartFading = distanceInFront >= 0f && distanceInFront <= foregroundDepthPixels;

                bool shouldRemainFaded = wasAlreadyFaded && distanceInFront >= -foregroundBufferPixels && distanceInFront <= foregroundBufferPixels;

                if (shouldStartFading || shouldRemainFaded)
                {
                    ApplyFade(tilemap, wallCell);
                    AddWall(wallsToKeepFaded, tilemap, wallCell);
                }
            }
        }

        RestoreWallsNotIn(wallsToKeepFaded);
        fadedCells = wallsToKeepFaded;
    }

    private bool IsWallFaded(Tilemap tilemap, Vector3Int cell)
    {
        return fadedCells.TryGetValue(tilemap, out HashSet<Vector3Int> cells) && cells.Contains(cell);
    }

    private void AddWall (Dictionary<Tilemap, HashSet<Vector3Int>> collection, Tilemap tilemap, Vector3Int cell)
    {
        if (!collection.ContainsKey(tilemap))
        {
            collection[tilemap] = new HashSet<Vector3Int>();
        }

        collection[tilemap].Add(cell);
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
                continue;

            foreach (Vector3Int cell in entry.Value)
            {
                bool shouldStayFaded = wallsToKeepFaded.TryGetValue(tilemap, out HashSet<Vector3Int> cells) && cells.Contains(cell);

                if (shouldStayFaded || !tilemap.HasTile(cell))
                    continue;
                Color color = tilemap.GetColor(cell);
                color.a = 1f;

                tilemap.SetColor(cell, color);
            }
        }
    }

    private void FadeWall(Tilemap tilemap, Vector3Int cell)
    {
        TileFlags flags = tilemap.GetTileFlags(cell);

        // Unlock the tile's color.
        tilemap.SetTileFlags(
            cell,
            flags & ~TileFlags.LockColor
        );

        Color fadedColor = tilemap.GetColor(cell);
        fadedColor.a = fadeAlpha;

        tilemap.SetColor(cell, fadedColor);

        if (!fadedCells.ContainsKey(tilemap))
        {
            fadedCells[tilemap] = new HashSet<Vector3Int>();
        }

        fadedCells[tilemap].Add(cell);
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
    }

    private void OnDisable()
    {
        RestoreWalls();
    }
}