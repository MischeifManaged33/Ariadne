using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(10000)]
public class CorridorWallPainter : MonoBehaviour
{
    [Header("Wall Tilemaps")]
    [SerializeField] private Tilemap wallsAlongX;
    [SerializeField] private Tilemap wallsAlongY;
    [SerializeField] private Tilemap corners;

    [Header("Wall Pieces — Top or Single Layer")]
    [SerializeField] private TileBase straightAlongX;
    [SerializeField] private TileBase straightAlongY;
    [SerializeField] private TileBase connectorTile;

    [Header("Wall Height")]
    [SerializeField, Min(1)]
    private int wallHeight = 3;

    [Tooltip("Vertical spacing in world units.")]
    [SerializeField, Min(0.01f)]
    private float heightPerLayer = 0.5f;

    [Tooltip(
        "Optional repeatable body piece. " +
        "Falls back to Straight Along X."
    )]
    [SerializeField] private TileBase bodyAlongX;

    [Tooltip(
        "Optional repeatable body piece. " +
        "Falls back to Straight Along Y."
    )]
    [SerializeField] private TileBase bodyAlongY;

    // Retained to preserve existing Inspector data.
    [Header("Connector Offsets — Local Units")]
    [SerializeField] private Vector3 insideJoinOffset;
    [SerializeField] private Vector3 outsideJoinOffset;

    [Header("Invisible Walls")]
    [SerializeField] private Tilemap invisibleWalls;
    [SerializeField] private TileBase invisibleWallTile;

    [Serializable]
    private class HeightLayer
    {
        public Tilemap source;
        public Tilemap visual;

        public TilemapRenderer sourceRenderer;
        public TilemapRenderer visualRenderer;

        public Vector3Int[] cells;
        public int level;
    }

    [SerializeField, HideInInspector]
    private List<HeightLayer> heightLayers =
        new List<HeightLayer>();

    public void Paint(
        HashSet<Vector2Int> corridorFloor,
        HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {
        if (corridorFloor == null ||
            allFloor == null ||
            protectedCells == null)
        {
            throw new ArgumentException(
                "Wall painting needs all three cell sets."
            );
        }

        if (wallsAlongX == null ||
            wallsAlongY == null ||
            straightAlongX == null ||
            straightAlongY == null)
        {
            throw new InvalidOperationException(
                "Assign both straight-wall Tilemaps and tiles."
            );
        }

        if (wallsAlongX == wallsAlongY)
        {
            throw new InvalidOperationException(
                "The two wall directions need separate Tilemaps."
            );
        }

        if (invisibleWalls == null ||
            invisibleWallTile == null)
        {
            throw new InvalidOperationException(
                "Assign Invisible Walls and Invisible Wall Tile."
            );
        }

        if (invisibleWalls == wallsAlongX ||
            invisibleWalls == wallsAlongY ||
            invisibleWalls == corners)
        {
            throw new InvalidOperationException(
                "Invisible Walls must use a separate Tilemap."
            );
        }

        ClearHeightLayers();

        wallsAlongX.ClearAllTiles();
        wallsAlongY.ClearAllTiles();

        if (corners != null)
            corners.ClearAllTiles();

        HashSet<Vector2Int> xEdges =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> yEdges =
            new HashSet<Vector2Int>();

        foreach (Vector2Int floor in corridorFloor)
        {
            Vector2Int upEdge = floor + Vector2Int.up;
            Vector2Int downEdge = floor + Vector2Int.down;
            Vector2Int rightEdge = floor + Vector2Int.right;
            Vector2Int leftEdge = floor + Vector2Int.left;

            if (CanPaint(upEdge, allFloor, protectedCells))
                xEdges.Add(upEdge);

            if (CanPaint(downEdge, allFloor, protectedCells))
                xEdges.Add(downEdge);

            if (CanPaint(rightEdge, allFloor, protectedCells))
                yEdges.Add(rightEdge);

            if (CanPaint(leftEdge, allFloor, protectedCells))
                yEdges.Add(leftEdge);
        }

        PaintFace(
            wallsAlongX,
            straightAlongX,
            bodyAlongX,
            xEdges
        );

        PaintFace(
            wallsAlongY,
            straightAlongY,
            bodyAlongY,
            yEdges
        );

        PaintInvisibleWalls(
            corridorFloor,
            allFloor,
            protectedCells
        );
    }

    private void PaintFace(
        Tilemap source,
        TileBase topTile,
        TileBase bodyTile,
        HashSet<Vector2Int> cells)
    {
        int count = Mathf.Max(1, wallHeight);

        TileBase body =
            bodyTile != null ? bodyTile : topTile;

        TileBase bottomTile =
            count == 1 ? topTile : body;

        List<Vector3Int> painted =
            new List<Vector3Int>();

        foreach (Vector2Int position in cells)
        {
            Vector3Int cell = new Vector3Int(
                position.x,
                position.y,
                0
            );

            source.SetTile(cell, bottomTile);
            painted.Add(cell);
        }

        if (painted.Count == 0)
            return;

        for (int level = 1; level < count; level++)
        {
            TileBase tile =
                level == count - 1 ? topTile : body;

            CreateHeightLayer(
                source,
                tile,
                painted,
                level
            );
        }
    }

    private void CreateHeightLayer(
        Tilemap source,
        TileBase tile,
        List<Vector3Int> cells,
        int level)
    {
        GameObject layerObject = new GameObject(
    source.name + " - Height " + (level + 1),
    typeof(Tilemap),
    typeof(TilemapRenderer)
);

        // Automatically register this generated wall layer.
        layerObject.AddComponent<TransparentWallTilemap>();

        layerObject.layer = source.gameObject.layer;

        Transform layerTransform = layerObject.transform;

        layerTransform.SetParent(
            source.transform.parent,
            false
        );

        layerTransform.localRotation =
            source.transform.localRotation;

        layerTransform.localScale =
            source.transform.localScale;

        // Move straight upward in world space.
        layerTransform.position =
            source.transform.position +
            Vector3.up *
            (level * Mathf.Max(0.01f, heightPerLayer));

        Tilemap visual =
            layerObject.GetComponent<Tilemap>();

        visual.tileAnchor = source.tileAnchor;
        visual.orientation = source.orientation;
        visual.orientationMatrix = source.orientationMatrix;
        visual.animationFrameRate = source.animationFrameRate;
        visual.color = source.color;

        TilemapRenderer sourceRenderer =
            source.GetComponent<TilemapRenderer>();

        TilemapRenderer visualRenderer =
            layerObject.GetComponent<TilemapRenderer>();

        if (sourceRenderer != null)
        {
            visualRenderer.sharedMaterials =
                sourceRenderer.sharedMaterials;

            visualRenderer.sortingLayerID =
                sourceRenderer.sortingLayerID;

            visualRenderer.sortingOrder =
                sourceRenderer.sortingOrder;

            visualRenderer.sortOrder =
                sourceRenderer.sortOrder;

            visualRenderer.mode = sourceRenderer.mode;

            visualRenderer.maskInteraction =
                sourceRenderer.maskInteraction;

            visualRenderer.enabled =
                sourceRenderer.enabled;
        }

        foreach (Vector3Int cell in cells)
        {
            visual.SetTile(cell, tile);

            // Allow the existing wall fade colors to be copied.
            visual.SetTileFlags(cell, TileFlags.None);

            if (source.GetTile(cell) == tile)
            {
                visual.SetTransformMatrix(
                    cell,
                    source.GetTransformMatrix(cell)
                );
            }

            visual.SetColor(
                cell,
                source.GetColor(cell)
            );
        }

        heightLayers.Add(new HeightLayer
        {
            source = source,
            visual = visual,
            sourceRenderer = sourceRenderer,
            visualRenderer = visualRenderer,
            cells = cells.ToArray(),
            level = level
        });

        // Upper layers have no physics colliders.
    }

    private void LateUpdate()
    {
        foreach (HeightLayer layer in heightLayers)
        {
            if (layer.source == null ||
                layer.visual == null)
            {
                continue;
            }

            Vector3 position =
                layer.source.transform.position +
                Vector3.up *
                (layer.level * Mathf.Max(0.01f, heightPerLayer));

            if (layer.visual.transform.position != position)
                layer.visual.transform.position = position;

            if (layer.visual.color != layer.source.color)
                layer.visual.color = layer.source.color;

            if (layer.sourceRenderer != null &&
                layer.visualRenderer != null)
            {
                layer.visualRenderer.enabled =
                    layer.source.gameObject.activeInHierarchy &&
                    layer.sourceRenderer.enabled;
            }

            // Carry tile-color fading to every height layer.
            foreach (Vector3Int cell in layer.cells)
            {
                Color color = layer.source.GetColor(cell);

                if (layer.visual.GetColor(cell) != color)
                    layer.visual.SetColor(cell, color);
            }
        }
    }

    private void PaintInvisibleWalls(
        HashSet<Vector2Int> corridorFloor,
        HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {
        invisibleWalls.ClearAllTiles();

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,

            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1)
        };

        HashSet<Vector2Int> boundary =
            new HashSet<Vector2Int>();

        foreach (Vector2Int floor in corridorFloor)
        {
            foreach (Vector2Int direction in directions)
            {
                Vector2Int candidate = floor + direction;

                if (CanPaint(
                    candidate,
                    allFloor,
                    protectedCells))
                {
                    boundary.Add(candidate);
                }
            }
        }

        foreach (Vector2Int cell in boundary)
        {
            invisibleWalls.SetTile(
                new Vector3Int(cell.x, cell.y, 0),
                invisibleWallTile
            );
        }

        Debug.Log(
            $"Painted {boundary.Count} invisible boundary tiles.",
            this
        );
    }

    private static bool CanPaint(
        Vector2Int cell,
        HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {
        return !allFloor.Contains(cell) &&
               !protectedCells.Contains(cell);
    }

    private void ClearHeightLayers()
    {
        foreach (HeightLayer layer in heightLayers)
        {
            if (layer.visual == null ||
                layer.visual == wallsAlongX ||
                layer.visual == wallsAlongY ||
                layer.visual == corners ||
                layer.visual == invisibleWalls)
            {
                continue;
            }

            GameObject target = layer.visual.gameObject;
            target.SetActive(false);

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        heightLayers.Clear();
    }

    private void OnDestroy()
    {
        ClearHeightLayers();
    }
}