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

    [Header("Wall Pieces � Top or Single Layer")]
    [SerializeField] private TileBase straightAlongX;
    [SerializeField] private TileBase straightAlongY;
    [SerializeField] private TileBase connectorTile;

    [Header("Front Wall Tilemaps")]
    [SerializeField] private Tilemap frontWallsAlongX;
    [SerializeField] private Tilemap frontWallsAlongY;

    [Header("Front Wall Offsets � Local Units")]
    [SerializeField] private Vector3 frontAlongXOffset;
    [SerializeField] private Vector3 frontAlongYOffset;

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

    [Header("Sorting")]
    [SerializeField] private string[] heightSortingLayers = { "Wall", "Wall2", "Wall3" };
    [SerializeField] private int wallSortingOrder = 0;

    // Retained to preserve existing Inspector data.
    [Header("Connector Offsets � Local Units")]
    [SerializeField] private Vector3 insideJoinOffset;
    [SerializeField] private Vector3 outsideJoinOffset;

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
            frontWallsAlongX == null ||
            frontWallsAlongY == null ||
            straightAlongX == null)
        {
            throw new InvalidOperationException(
                "Assign all four visual wall Tilemaps, " +
                "their straight-wall tiles, and the invisible wall references."
            );
        }

        Tilemap[] visualWalls =
        {
        wallsAlongX,
        wallsAlongY,
        frontWallsAlongX,
        frontWallsAlongY
    };

        HashSet<Tilemap> uniqueMaps = new HashSet<Tilemap>();

        foreach (Tilemap map in visualWalls)
        {
            if (!uniqueMaps.Add(map) ||
                map == corners)
            {
                throw new InvalidOperationException(
                    "Each wall direction, corners, and invisible walls " +
                    "must use separate Tilemaps."
                );
            }
        }

        // The front offsets are relative to the matching back wall.
        if (frontWallsAlongX.transform.parent != wallsAlongX.transform.parent ||
            frontWallsAlongY.transform.parent != wallsAlongY.transform.parent)
        {
            throw new InvalidOperationException(
                "Each front wall Tilemap must share a Grid parent " +
                "with its matching back wall Tilemap."
            );
        }

        ClearHeightLayers();

        ConfigureFrontWall(
            frontWallsAlongX,
            wallsAlongX,
            frontAlongXOffset
        );

        ConfigureFrontWall(
            frontWallsAlongY,
            wallsAlongY,
            frontAlongYOffset
        );

        foreach (Tilemap map in visualWalls)
        {
            map.ClearAllTiles();
            ApplyWallSorting(map.GetComponent<TilemapRenderer>(), 0, IsBottomLeftFace(map));

            // Automatically register all four base wall Tilemaps.
            if (map.GetComponent<TransparentWallTilemap>() == null)
            {
                map.gameObject.AddComponent<TransparentWallTilemap>();
            }
        }

        if (corners != null)
            corners.ClearAllTiles();

        HashSet<Vector2Int> backXEdges = new HashSet<Vector2Int>();
        HashSet<Vector2Int> backYEdges = new HashSet<Vector2Int>();
        HashSet<Vector2Int> frontXEdges = new HashSet<Vector2Int>();
        HashSet<Vector2Int> frontYEdges = new HashSet<Vector2Int>();

        foreach (Vector2Int floor in corridorFloor)
        {
            Vector2Int upEdge = floor + Vector2Int.up;
            Vector2Int rightEdge = floor + Vector2Int.right;
            Vector2Int downEdge = floor + Vector2Int.down;
            Vector2Int leftEdge = floor + Vector2Int.left;

            if (CanPaint(upEdge, allFloor, protectedCells))
                backXEdges.Add(upEdge);

            if (CanPaint(rightEdge, allFloor, protectedCells))
                backYEdges.Add(rightEdge);

            if (CanPaint(downEdge, allFloor, protectedCells))
                frontXEdges.Add(downEdge);

            if (CanPaint(leftEdge, allFloor, protectedCells))
                frontYEdges.Add(leftEdge);
        }

        List<Vector2Int> cornerExtensions = new List<Vector2Int>();

        foreach (Vector2Int floor in corridorFloor)
        {
            Vector2Int leftEdge = floor + Vector2Int.left;

            if (!frontYEdges.Contains(leftEdge))
                continue;

            Vector2Int bottomCorner = leftEdge + Vector2Int.down;

            if (frontXEdges.Contains(floor + Vector2Int.down) &&
                CanPaint(bottomCorner, allFloor, protectedCells))
            {
                cornerExtensions.Add(bottomCorner);
            }

            Vector2Int leftCorner = leftEdge + Vector2Int.up;

            if (backXEdges.Contains(floor + Vector2Int.up) &&
                CanPaint(leftCorner, allFloor, protectedCells))
            {
                cornerExtensions.Add(leftCorner);
            }
        }

        frontYEdges.UnionWith(cornerExtensions);

        cornerExtensions.Clear();

        foreach (Vector2Int floor in corridorFloor)
        {
            Vector2Int rightEdge = floor + Vector2Int.right;
            Vector2Int rightCorner = rightEdge + Vector2Int.down;

            if (backYEdges.Contains(rightEdge) &&
                frontXEdges.Contains(floor + Vector2Int.down) &&
                CanPaint(rightCorner, allFloor, protectedCells))
            {
                cornerExtensions.Add(rightCorner);
            }
        }

        backYEdges.UnionWith(cornerExtensions);

        frontXEdges.ExceptWith(frontYEdges);
        backXEdges.ExceptWith(frontYEdges);
        backYEdges.ExceptWith(frontYEdges);

        frontXEdges.ExceptWith(backXEdges);
        backYEdges.ExceptWith(backXEdges);

        backYEdges.ExceptWith(frontXEdges);

        PaintFace(
            wallsAlongX,
            straightAlongX,
            bodyAlongX,
            backXEdges,
            false
        );

        PaintFace(
            wallsAlongY,
            straightAlongY,
            bodyAlongY,
            backYEdges,
            false
        );

        PaintFace(
            frontWallsAlongX,
            straightAlongX,
            bodyAlongX,
            frontXEdges,
            true
        );

        PaintFace(
            frontWallsAlongY,
            straightAlongY,
            bodyAlongY,
            frontYEdges,
            true
        );
    }

    private void ConfigureFrontWall(
    Tilemap front,
    Tilemap reference,
    Vector3 offset)
    {
        front.transform.localPosition =
            reference.transform.localPosition + offset;

        front.transform.localRotation =
            reference.transform.localRotation;

        front.transform.localScale =
            reference.transform.localScale;

        front.orientation = reference.orientation;
        front.orientationMatrix = reference.orientationMatrix;
    }

    private void PaintFace(
        Tilemap source,
        TileBase topTile,
        TileBase bodyTile,
        HashSet<Vector2Int> cells,
        bool isFront)
    {
        int count = Mathf.Max(1, wallHeight);

        ConfigureWallPhysics(
            source,
            hasCollider: true,
            isTrigger: false,
            blocksLight: isFront || count == 1
        );

        SetFadeable(source, isFront);

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
                level,
                !isFront && level == count - 1,
                isFront
            );
        }
    }

    private void CreateHeightLayer(
        Tilemap source,
        TileBase tile,
        List<Vector3Int> cells,
        int level,
        bool blocksLight,
        bool canFade)
    {
        GameObject layerObject = new GameObject(
    source.name + " - Height " + (level + 1),
    typeof(Tilemap),
    typeof(TilemapRenderer)
);

        // Upper front layers fade, including those sorted below the player.
        layerObject.AddComponent<TransparentWallTilemap>().IgnoreSorting = true;

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


            visualRenderer.maskInteraction =
                sourceRenderer.maskInteraction;

            visualRenderer.enabled =
                sourceRenderer.enabled;
        }

        ApplyWallSorting(visualRenderer, level, IsBottomLeftFace(source));

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

        ConfigureWallPhysics(
            visual,
            hasCollider: blocksLight,
            isTrigger: true,
            blocksLight: blocksLight
        );

        SetFadeable(visual, canFade);
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

            foreach (Vector3Int cell in layer.cells)
            {
                Color current = layer.visual.GetColor(cell);
                Color color = layer.source.GetColor(cell);
                color.a = current.a;

                if (current != color)
                    layer.visual.SetColor(cell, color);
            }
        }
    }

    private void PaintInvisibleWalls(
        HashSet<Vector2Int> corridorFloor,
        HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {

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

        Debug.Log(
            $"Painted {boundary.Count} invisible boundary tiles.",
            this
        );
    }

    private static void ConfigureWallPhysics(
        Tilemap map,
        bool hasCollider,
        bool isTrigger,
        bool blocksLight)
    {
        TilemapCollider2D collider =
            map.GetComponent<TilemapCollider2D>();

        if (hasCollider)
        {
            if (collider == null)
                collider = map.gameObject.AddComponent<TilemapCollider2D>();

            collider.enabled = true;
            collider.isTrigger = isTrigger;
        }
        else if (collider != null)
        {
            collider.enabled = false;
        }

        // Light and enemy sight raycast against the Walls layer.
        if (map.TryGetComponent(out TransparentWallTilemap transparent))
            transparent.AssignWallsLayer = false;

        int wallsLayer = LayerMask.NameToLayer("Walls");

        map.gameObject.layer =
            blocksLight && wallsLayer >= 0 ? wallsLayer : 0;
    }

    private static void SetFadeable(Tilemap map, bool canFade)
    {
        if (map.TryGetComponent(out TransparentWallTilemap transparent))
            transparent.enabled = canFade;
    }

    private bool IsBottomLeftFace(Tilemap map)
    {
        return map == frontWallsAlongY;
    }

    private void ApplyWallSorting(TilemapRenderer renderer, int level, bool isBottomLeft)
    {
        if (renderer == null)
            return;

        string layerName = "Wall";

        if (heightSortingLayers != null && heightSortingLayers.Length > 0)
        {
            layerName = heightSortingLayers[
                Mathf.Clamp(level, 0, heightSortingLayers.Length - 1)
            ];
        }

        renderer.mode = TilemapRenderer.Mode.Individual;
        renderer.sortingLayerName = layerName;
        renderer.sortingOrder = wallSortingOrder;
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
                layer.visual == wallsAlongY)
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