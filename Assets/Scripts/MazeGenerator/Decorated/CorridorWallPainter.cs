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

    [Header("Wall Pieces - Top or Single Layer")]
    [SerializeField] private TileBase straightAlongX;
    [SerializeField] private TileBase straightAlongY;
    [SerializeField] private TileBase connectorTile;

    [Header("Artist Set - Front Straights")]
    [SerializeField] private TileBase frontStraightAlongX;
    [SerializeField] private TileBase frontStraightAlongY;

    [Header("Artist Set - Floor Neighbours of a Wall Cell")]
    [SerializeField] private TileBase floorUpAndRight;
    [SerializeField] private TileBase floorUpAndLeft;
    [SerializeField] private TileBase floorDownAndRight;
    [SerializeField] private TileBase floorDownAndLeft;
    [SerializeField] private TileBase enclosedWall;

    [Header("Artist Set - Outside Corner Posts")]
    [SerializeField] private TileBase backCornerPost;
    [SerializeField] private TileBase frontCornerPost;
    // 92 pixels / 140 pixels: measured from the supplied 280px wall sprites.
    // The post sprite's footprint is at the south tip of its cell. This moves
    // that footprint to the other three tips without changing the artwork.
    private const float PostStep = 92f / 140f;

    [SerializeField, HideInInspector]
    private List<Tilemap> generatedCornerMaps = new List<Tilemap>();

    [Flags]
    private enum FloorSides { None = 0, Up = 1, Right = 2, Down = 4, Left = 8 }

    [Header("Front Wall Tilemaps")]
    [SerializeField] private Tilemap frontWallsAlongX;
    [SerializeField] private Tilemap frontWallsAlongY;

    [Header("Front Wall Offsets - Local Units")]
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
    [Header("Connector Offsets - Local Units")]
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

    [Header("Performance Diagnostics")]
    [SerializeField] private bool logWallPaintTiming;
    public float LastWallPaintMilliseconds { get; private set; }
    private int newWallObjects;
    private int reusedWallObjects;
    private readonly HashSet<HeightLayer> usedHeightLayers = new HashSet<HeightLayer>();
    private readonly HashSet<Tilemap> usedCornerMaps = new HashSet<Tilemap>();
    private static readonly Unity.Profiling.ProfilerMarker PaintMarker =
        new Unity.Profiling.ProfilerMarker("Ariadne.CorridorWallPainter.Paint");

    public void Paint(HashSet<Vector2Int> corridorFloor, HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        newWallObjects = reusedWallObjects = 0;
        using (PaintMarker.Auto())
            PaintInternal(corridorFloor, allFloor, protectedCells);
        timer.Stop();
        LastWallPaintMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
        if (logWallPaintTiming)
            Debug.Log($"Wall repaint: {LastWallPaintMilliseconds:F1} ms; " +
                $"created {newWallObjects}, reused {reusedWallObjects} wall objects.", this);
    }

    private void PaintInternal(
        HashSet<Vector2Int> corridorFloor,
        HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {
        if (corridorFloor == null || allFloor == null || protectedCells == null)
            throw new ArgumentException("Wall painting needs all three cell sets.");

        Tilemap[] maps = { wallsAlongX, wallsAlongY, frontWallsAlongX, frontWallsAlongY };
        HashSet<Tilemap> unique = new HashSet<Tilemap>();
        foreach (Tilemap map in maps)
        {
            if (map == null || map == corners || !unique.Add(map))
                throw new InvalidOperationException("Assign four distinct wall Tilemaps and a separate optional Corners Tilemap.");
            if (map.transform.parent != wallsAlongX.transform.parent)
                throw new InvalidOperationException("All corridor wall Tilemaps must share the same Grid parent.");
        }
        if (straightAlongX == null || straightAlongY == null ||
            frontStraightAlongX == null || frontStraightAlongY == null ||
            floorUpAndRight == null || floorUpAndLeft == null ||
            floorDownAndRight == null || floorDownAndLeft == null ||
            enclosedWall == null || backCornerPost == null || frontCornerPost == null)
            throw new InvalidOperationException("On CorridorWallPainter, run Configure Supplied Artist Wall Set from the component menu before generating.");

        usedHeightLayers.Clear();
        usedCornerMaps.Clear();
        ConfigureFrontWall(wallsAlongY, wallsAlongX, Vector3.zero);
        ConfigureFrontWall(frontWallsAlongX, wallsAlongX, Vector3.zero);
        ConfigureFrontWall(frontWallsAlongY, wallsAlongX, Vector3.zero);
        foreach (Tilemap map in maps)
        {
            map.ClearAllTiles();
            TransparentWallTilemap transparent = map.GetComponent<TransparentWallTilemap>();
            if (transparent == null) transparent = map.gameObject.AddComponent<TransparentWallTilemap>();
            transparent.IgnoreSorting = true;
            ApplyWallSorting(map.GetComponent<TilemapRenderer>(), 0, false);
        }
        if (corners != null) corners.ClearAllTiles();

        var plans = new Dictionary<Vector2Int, TileBase>[4];
        for (int i = 0; i < plans.Length; i++) plans[i] = new Dictionary<Vector2Int, TileBase>();
        HashSet<Vector2Int> candidates = new HashSet<Vector2Int>();
        foreach (Vector2Int floor in corridorFloor)
        {
            candidates.Add(floor + Vector2Int.up);
            candidates.Add(floor + Vector2Int.right);
            candidates.Add(floor + Vector2Int.down);
            candidates.Add(floor + Vector2Int.left);
        }
        foreach (Vector2Int cell in candidates)
        {
            if (!CanPaint(cell, allFloor, protectedCells)) continue;
            FloorSides sides = FloorSides.None;
            if (allFloor.Contains(cell + Vector2Int.up)) sides |= FloorSides.Up;
            if (allFloor.Contains(cell + Vector2Int.right)) sides |= FloorSides.Right;
            if (allFloor.Contains(cell + Vector2Int.down)) sides |= FloorSides.Down;
            if (allFloor.Contains(cell + Vector2Int.left)) sides |= FloorSides.Left;
            TileBase tile = TileFor(sides);
            if (tile == null) continue;
            // Each cell has exactly one owner. A meeting of two edges gets a
            // corner sprite rather than deleting either edge by priority.
            int owner = (sides & FloorSides.Up) != 0 ? 2 :
                (sides & FloorSides.Right) != 0 ? 3 :
                (sides & FloorSides.Down) != 0 ? 0 : 1;
            plans[owner].Add(cell, tile);
        }
        for (int i = 0; i < maps.Length; i++)
            PaintFace(maps[i], plans[i], i >= 2);

        // A convex floor corner leaves a small diamond between the two wall
        // end caps. Fill only that diamond, not another full straight wall.
        PaintOutsidePosts(corridorFloor, allFloor, protectedCells);
        // Retire only objects not needed by this layout. Reused maps stay active,
        // avoiding collider/registration churn on every shift.
        foreach (HeightLayer layer in heightLayers)
            if (layer.visual != null && !usedHeightLayers.Contains(layer))
                layer.visual.gameObject.SetActive(false);
        foreach (Tilemap map in generatedCornerMaps)
            if (map != null && !usedCornerMaps.Contains(map))
                map.gameObject.SetActive(false);
    }

    private TileBase TileFor(FloorSides sides)
    {
        switch (sides)
        {
            case FloorSides.Down: return straightAlongX;        // 134
            case FloorSides.Left: return straightAlongY;        // 135
            case FloorSides.Up: return frontStraightAlongX;     // 157
            case FloorSides.Right: return frontStraightAlongY;  // 158
            case FloorSides.Up | FloorSides.Right: return floorUpAndRight;     // 156
            case FloorSides.Up | FloorSides.Left: return floorUpAndLeft;       // 111
            case FloorSides.Down | FloorSides.Right: return floorDownAndRight; // 110
            case FloorSides.Down | FloorSides.Left: return floorDownAndLeft;   // 109
            case FloorSides.None: return null;
            default: return enclosedWall; // Opposing edges or a one-cell solid island.
        }
    }

    private void PaintOutsidePosts(HashSet<Vector2Int> corridorFloor,
        HashSet<Vector2Int> allFloor, HashSet<Vector2Int> protectedCells)
    {
        for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
            {
                var posts = new Dictionary<Vector2Int, TileBase>();
                bool isFront = dx > 0 || dy > 0;
                TileBase tile = dx > 0 && dy > 0 ? frontCornerPost : backCornerPost;
                foreach (Vector2Int floor in corridorFloor)
                {
                    Vector2Int cell = floor - new Vector2Int(dx, dy);
                    if (!CanPaint(cell, allFloor, protectedCells) ||
                        allFloor.Contains(cell + new Vector2Int(dx, 0)) ||
                        allFloor.Contains(cell + new Vector2Int(0, dy))) continue;
                    // Never extend across the artist room's reserved footprint.
                    if (protectedCells.Contains(cell + new Vector2Int(dx, 0)) ||
                        protectedCells.Contains(cell + new Vector2Int(0, dy))) continue;
                    posts[cell] = tile;
                }
                if (posts.Count == 0) continue;
                Tilemap map = CreateCornerMap(dx, dy);
                Vector3 origin = map.CellToLocal(Vector3Int.zero);
                Vector3 shift = Vector3.zero;
                if (dx > 0) shift += (map.CellToLocal(Vector3Int.right) - origin) * PostStep;
                if (dy > 0) shift += (map.CellToLocal(Vector3Int.up) - origin) * PostStep;
                PaintFace(map, posts, isFront, Matrix4x4.Translate(shift), true);
            }
    }

    private Tilemap CreateCornerMap(int dx, int dy)
    {
        string name = "Corridor Corner Posts " + dx + "," + dy;
        Tilemap map = null;
        foreach (Tilemap cached in generatedCornerMaps)
            if (cached != null && cached.name == name) { map = cached; break; }
        if (map == null)
        {
            GameObject go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(wallsAlongX.transform.parent, false);
            map = go.GetComponent<Tilemap>();
            go.AddComponent<TransparentWallTilemap>().IgnoreSorting = true;
            generatedCornerMaps.Add(map);
            newWallObjects++;
        }
        else reusedWallObjects++;
        usedCornerMaps.Add(map);
        // Remove tiles from the previous layout before repainting this source.
        map.ClearAllTiles();
        ConfigureFrontWall(map, wallsAlongX, Vector3.zero);
        map.color = wallsAlongX.color;
        TilemapRenderer source = wallsAlongX.GetComponent<TilemapRenderer>();
        TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
        if (source != null)
        {
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.sortOrder = source.sortOrder;
            renderer.maskInteraction = source.maskInteraction;
        }
        ApplyWallSorting(renderer, 0, false);
        map.gameObject.SetActive(true);
        return map;
    }

    private void ClearCornerMaps()
    {
        foreach (Tilemap map in generatedCornerMaps)
        {
            if (map == null) continue;
            map.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(map.gameObject);
            else DestroyImmediate(map.gameObject);
        }
        generatedCornerMaps.Clear();
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

        front.tileAnchor = reference.tileAnchor;
        front.orientation = reference.orientation;
        front.orientationMatrix = reference.orientationMatrix;
    }

    private TileBase LayerTile(TileBase top, bool isTop)
    {
        if (isTop) return top;
        if ((top == straightAlongX || top == frontStraightAlongX) && bodyAlongX != null)
            return bodyAlongX;
        if ((top == straightAlongY || top == frontStraightAlongY) && bodyAlongY != null)
            return bodyAlongY;
        return top;
    }

    private void PaintFace(Tilemap source, Dictionary<Vector2Int, TileBase> plan,
        bool isFront, Matrix4x4? tileTransform = null, bool isPost = false)
    {
        int count = Mathf.Max(1, wallHeight);
        // The transformed post sprite must not receive a transformed full-cell
        // collision shape. DungeonGroundCollision owns those outside cells.
        ConfigureWallPhysics(source, !isPost, false, !isPost && (isFront || count == 1));
        SetFadeable(source, isFront);
        // This post closes the two front strips. Its translated sprite pivot
        // otherwise sorts behind their end caps, exposing a zipper at each
        // height. Draw the post after those strips within the same layer.
        if (isPost && plan.ContainsValue(frontCornerPost))
        {
            TilemapRenderer renderer = source.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.sortingOrder = wallSortingOrder + 1;
        }
        var topTiles = new Dictionary<Vector3Int, TileBase>();
        var tileCells = new Vector3Int[plan.Count];
        var tiles = new TileBase[plan.Count];
        int index = 0;
        foreach (KeyValuePair<Vector2Int, TileBase> entry in plan)
        {
            Vector3Int cell = new Vector3Int(entry.Key.x, entry.Key.y, 0);
            topTiles[cell] = entry.Value;
            tileCells[index] = cell;
            tiles[index++] = LayerTile(entry.Value, count == 1);
        }
        if (tileCells.Length > 0) source.SetTiles(tileCells, tiles);
        Matrix4x4 transform = tileTransform ?? Matrix4x4.identity;
        foreach (Vector3Int cell in tileCells)
        {
            source.SetTileFlags(cell, TileFlags.None);
            source.SetTransformMatrix(cell, transform);
        }
        if (topTiles.Count == 0) return;
        for (int level = 1; level < count; level++)
            CreateHeightLayer(source, topTiles, level,
                !isPost && !isFront && level == count - 1, isFront);
    }

    private void CreateHeightLayer(
        Tilemap source,
        Dictionary<Vector3Int, TileBase> topTiles,
        int level,
        bool blocksLight,
        bool canFade)
    {
        HeightLayer layer = null;
        foreach (HeightLayer cached in heightLayers)
            if (cached.source == source && cached.level == level && cached.visual != null)
            { layer = cached; break; }
        GameObject layerObject;
        if (layer == null)
        {
            layerObject = new GameObject(source.name + " - Height " + (level + 1),
                typeof(Tilemap), typeof(TilemapRenderer));
            layerObject.AddComponent<TransparentWallTilemap>().IgnoreSorting = true;
            layer = new HeightLayer
            {
                source = source,
                level = level,
                visual = layerObject.GetComponent<Tilemap>()
            };
            heightLayers.Add(layer);
            newWallObjects++;
        }
        else
        {
            layerObject = layer.visual.gameObject;
            reusedWallObjects++;
        }
        usedHeightLayers.Add(layer);
        layer.visual.ClearAllTiles();

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
        // Retain the front-post bias on every stacked height, while keeping
        // each height on its existing sorting layer.
        if (sourceRenderer != null)
            visualRenderer.sortingOrder = sourceRenderer.sortingOrder;

        var tileCells = new Vector3Int[topTiles.Count];
        var tiles = new TileBase[topTiles.Count];
        int index = 0;
        foreach (KeyValuePair<Vector3Int, TileBase> entry in topTiles)
        {
            tileCells[index] = entry.Key;
            tiles[index++] = LayerTile(entry.Value, level == Mathf.Max(1, wallHeight) - 1);
        }
        visual.SetTiles(tileCells, tiles);
        foreach (Vector3Int cell in tileCells)
        {
            visual.SetTileFlags(cell, TileFlags.None);
            visual.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            visual.SetColor(cell, source.GetColor(cell));
        }
        layer.sourceRenderer = sourceRenderer;
        layer.visualRenderer = visualRenderer;
        layer.cells = new List<Vector3Int>(topTiles.Keys).ToArray();

        ConfigureWallPhysics(
            visual,
            hasCollider: blocksLight,
            isTrigger: true,
            blocksLight: blocksLight
        );

        SetFadeable(visual, canFade);
        layerObject.SetActive(true);
    }

    private void LateUpdate()
    {
        foreach (HeightLayer layer in heightLayers)
        {
            if (layer.source == null ||
                layer.visual == null || !layer.visual.gameObject.activeSelf)
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
        ClearCornerMaps();
    }

#if UNITY_EDITOR
    [ContextMenu("Configure Supplied Artist Wall Set")]
    private void ConfigureSuppliedArtistWallSet()
    {
        // Resolve everything before changing any serialized field.
        TileBase tile134 = LoadArtistTile("bbf149c167b275942b29ec2b7d9ae6d2", 134);
        TileBase tile135 = LoadArtistTile("891243b500c5336459f6c2c219b2f8ff", 135);
        TileBase tile157 = LoadArtistTile("76713a165965d2c4480c9f5c88becbdd", 157);
        TileBase tile158 = LoadArtistTile("e05e9d1907ca19c4a8c90fb1ba07bea5", 158);
        TileBase tile156 = LoadArtistTile("b08cd236b62fbbc4bb0bf7367e54e18d", 156);
        TileBase tile111 = LoadArtistTile("b603f87821b3bde42b668f18813bb04e", 111);
        TileBase tile110 = LoadArtistTile("b3f75c68915a6074bb1f546ef771f97f", 110);
        TileBase tile109 = LoadArtistTile("ebc20b565298c29459c30b00fe79707c", 109);
        TileBase tile174 = LoadArtistTile("b2f8b3d30beb2154b9a1f6ebfd585bc2", 174);
        TileBase tile108 = LoadArtistTile("2df2accd272ed9143b7dba4ea57403c7", 108);
        TileBase tile144 = LoadArtistTile("398128f51c9c5524798d028e976b03c1", 144);
        UnityEditor.Undo.RecordObject(this, "Configure Artist Wall Set");
        straightAlongX = tile134;
        straightAlongY = tile135;
        frontStraightAlongX = tile157;
        frontStraightAlongY = tile158;
        floorUpAndRight = tile156;
        floorUpAndLeft = tile111;
        floorDownAndRight = tile110;
        floorDownAndLeft = tile109;
        enclosedWall = tile174;
        backCornerPost = tile108;
        frontCornerPost = tile144;
        bodyAlongX = null;
        bodyAlongY = null;
        frontAlongXOffset = Vector3.zero;
        frontAlongYOffset = Vector3.zero;
        insideJoinOffset = Vector3.zero;
        outsideJoinOffset = Vector3.zero;
        wallHeight = 1;
        heightPerLayer = 1.4f;
        if (wallsAlongX != null)
        {
            Tilemap[] maps = { wallsAlongX, wallsAlongY, frontWallsAlongX, frontWallsAlongY, corners };
            foreach (Tilemap map in maps)
            {
                if (map == null) continue;
                UnityEditor.Undo.RecordObject(map, "Align Artist Wall Anchors");
                map.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
                UnityEditor.EditorUtility.SetDirty(map);
            }
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("Artist walls configured. Height is 1 for the first check. Existing floor and room assets were not changed.", this);
    }

    private static TileBase LoadArtistTile(string guid, int number)
    {
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        TileBase tile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (tile == null)
            throw new InvalidOperationException("Cannot find TileMapGrid_png_test3_" + number +
                ". Keep the original artist Tile assets and their .meta files in the project.");
        return tile;
    }
#endif
}
