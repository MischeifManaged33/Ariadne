using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DesignedDungeonGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomCatalogue roomCatalogue;
    [SerializeField] private Tilemap dungeonFloor;

    [Header("Dungeon Area — Tile Coordinates")]
    [SerializeField] private Vector2Int dungeonOrigin;

    [SerializeField]
    private Vector2Int dungeonSize =
        new Vector2Int(80, 80);

    [Header("Rooms")]
    [SerializeField, Min(1)] private int roomCount = 4;

    [Tooltip("Empty cells between room footprints.")]
    [SerializeField, Min(1)] private int roomSpacing = 4;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool useFixedSeed = false;

    [SerializeField]
    private TileMapVisualizer tilemapVisualizer;

    [Header("Corridor Width")]
    [Tooltip("0 = 1 cell wide, 1 = 3 cells, 2 = 5 cells.")]
    [SerializeField, Range(0, 3)]
    private int corridorRadius = 1;

    [Header("Corridor Smoothing")]
    [SerializeField, Range(0, 3)]
    private int smoothingPasses = 2;

    [Header("Extra Corridors")]
    [SerializeField, Min(0)]
    private int deadEndCount = 20;

    [Header("Character Spawning")]
    [SerializeField] private Transform player;

    [Header("Maze Layout")]
    [SerializeField, Min(4)]
    private int mazeSpacing = 6;

    [SerializeField, Range(0f, 0.3f)]
    private float mazeLoopChance = 0.08f; [Header("Maze Layout")]

    [SerializeField]
    private CorridorWallPainter corridorWallPainter;

    [SerializeField]
    private CorridorEnemySpawner corridorEnemySpawner;

    [SerializeField, Min(1)]
    private int minimumBranchLength = 3;

    [SerializeField, Min(1)]
    private int maximumBranchLength = 12;

    [SerializeField, Range(0f, 1f)]
    private float straightPreference = 0.65f;

    [SerializeField, Min(1)]
    private int layoutAttempts = 30;

    [SerializeField, Min(1)]
    private int attemptsPerRoom = 200;

    [SerializeField, HideInInspector]
    private GameObject generatedRoomRoot;

    // Corridor floor plus every generated room's floor.
    private readonly List<Tilemap> walkableFloors =
        new List<Tilemap>();

    private readonly List<float> floorCrossings =
        new List<float>();

    public bool WalkableFloorReady { get; private set; }

    private class SelectedRoom
    {
        public RoomDefinition prefab;
        public BoundsInt localBounds;
        public Vector2Int localAnchor;
    }

    private class PlacedRoom
    {
        public SelectedRoom selected;
        public RectInt bounds;
        public Vector2Int destinationAnchor;
    }

    private void Start()
    {
        RefreshWalkableFloors();

        if (generateOnStart)
            GenerateDungeon();
    }

    private void RefreshWalkableFloors()
    {
        walkableFloors.Clear();

        if (dungeonFloor != null)
            walkableFloors.Add(dungeonFloor);

        if (generatedRoomRoot != null)
        {
            RoomDefinition[] rooms =
                generatedRoomRoot.GetComponentsInChildren<
                    RoomDefinition
                >(true);

            foreach (RoomDefinition room in rooms)
            {
                if (room.FloorTilemap != null &&
                    !walkableFloors.Contains(room.FloorTilemap))
                {
                    walkableFloors.Add(room.FloorTilemap);
                }
            }
        }

        WalkableFloorReady = walkableFloors.Count > 0;
    }

    public bool IsWalkablePosition(Vector3 worldPosition)
    {
        if (!WalkableFloorReady)
            return false;

        foreach (Tilemap floor in walkableFloors)
        {
            if (floor == null ||
                !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            // These are planar 2D floors on tile layer zero.
            Vector3 probe = worldPosition;

            probe.z =
                floor.CellToWorld(Vector3Int.zero).z;

            Vector3Int cell = floor.WorldToCell(probe);
            cell.z = 0;

            if (floor.HasTile(cell))
                return true;
        }

        return false;
    }

    // Check the whole movement path, including any empty
    // cells between the start and destination.
    public float GetWalkableMoveFraction(
        Vector3 start,
        Vector3 end)
    {
        if (!IsWalkablePosition(start))
            return 0f;

        float distance = Vector2.Distance(start, end);

        if (distance < 0.000001f)
            return 1f;

        floorCrossings.Clear();
        floorCrossings.Add(0f);
        floorCrossings.Add(1f);

        foreach (Tilemap floor in walkableFloors)
        {
            if (floor == null ||
                !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 origin =
                floor.CellToWorld(Vector3Int.zero);

            Vector2 xAxis =
                (Vector2)floor.CellToWorld(Vector3Int.right)
                - origin;

            Vector2 yAxis =
                (Vector2)floor.CellToWorld(Vector3Int.up)
                - origin;

            float determinant =
                xAxis.x * yAxis.y -
                xAxis.y * yAxis.x;

            if (Mathf.Abs(determinant) < 0.000001f)
                return 0f;

            Vector2 a = (Vector2)start - origin;
            Vector2 b = (Vector2)end - origin;

            AddFloorCrossings(
                (a.x * yAxis.y - a.y * yAxis.x)
                    / determinant,
                (b.x * yAxis.y - b.y * yAxis.x)
                    / determinant
            );

            AddFloorCrossings(
                (xAxis.x * a.y - xAxis.y * a.x)
                    / determinant,
                (xAxis.x * b.y - xAxis.y * b.x)
                    / determinant
            );
        }

        floorCrossings.Sort();

        for (int i = 1; i < floorCrossings.Count; i++)
        {
            float before = floorCrossings[i - 1];
            float after = floorCrossings[i];

            if (after <= before)
                continue;

            Vector3 midpoint = Vector3.Lerp(
                start,
                end,
                (before + after) * 0.5f
            );

            if (!IsWalkablePosition(midpoint))
            {
                // Stop slightly before the empty cell.
                return Mathf.Max(
                    0f,
                    before - 0.0001f / distance
                );
            }
        }

        if (!IsWalkablePosition(end))
        {
            return Mathf.Max(
                0f,
                1f - 0.0001f / distance
            );
        }

        return 1f;
    }

    private void AddFloorCrossings(float start, float end)
    {
        float delta = end - start;

        if (Mathf.Abs(delta) < 0.000001f)
            return;

        int first =
            Mathf.FloorToInt(Mathf.Min(start, end)) + 1;

        int last =
            Mathf.CeilToInt(Mathf.Max(start, end)) - 1;

        for (int boundary = first;
             boundary <= last;
             boundary++)
        {
            float fraction =
                (boundary - start) / delta;

            if (fraction > 0f && fraction < 1f)
                floorCrossings.Add(fraction);
        }
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        if (roomCatalogue == null || dungeonFloor == null)
        {
            Debug.LogError(
                "Assign Room Catalogue and Dungeon Floor.",
                this
            );
            return;
        }

        if (roomCount < 1 ||
            roomSpacing < 1 ||
            dungeonSize.x < 1 ||
            dungeonSize.y < 1 ||
            layoutAttempts < 1 ||
            attemptsPerRoom < 1)
        {
            Debug.LogError(
                "Room count, spacing, size and attempt limits " +
                "must be positive.",
                this
            );
            return;
        }

        try
        {
            int generationSeed = useFixedSeed
                ? seed
                : System.Guid.NewGuid().GetHashCode();

            System.Random random =
                new System.Random(generationSeed);

            Debug.Log(
                $"Generating dungeon with seed: {generationSeed}"
            );

            List<SelectedRoom> selectedRooms =
                SelectRooms(random);

            List<PlacedRoom> layout =
                CreateLayout(selectedRooms, random);

            if (layout == null)
            {
                Debug.LogError(
                    "Could not fit all selected rooms. " +
                    "Increase Dungeon Size or reduce Room Count. " +
                    "The previous rooms were kept.",
                    this
                );
                return;
            }

            SpawnRooms(layout, random);

            Debug.Log(
                $"Placed {layout.Count} rooms. Seed: {seed}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private HashSet<Vector2Int> SmoothCorridorFloor(
        HashSet<Vector2Int> floor,
        List<RectInt> roomBounds,
        RectInt dungeonArea)
    {
        HashSet<Vector2Int> result =
            new HashSet<Vector2Int>(floor);

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        HashSet<Vector2Int> candidates =
            new HashSet<Vector2Int>();

        foreach (Vector2Int cell in floor)
        {
            foreach (Vector2Int direction in directions)
            {
                Vector2Int candidate = cell + direction;

                if (!floor.Contains(candidate))
                    candidates.Add(candidate);
            }
        }

        foreach (Vector2Int candidate in candidates)
        {
            if (!dungeonArea.Contains(candidate))
                continue;

            bool insideRoom = false;

            foreach (RectInt room in roomBounds)
            {
                if (room.Contains(candidate))
                {
                    insideRoom = true;
                    break;
                }
            }

            if (insideRoom)
                continue;

            int floorNeighbors = 0;

            foreach (Vector2Int direction in directions)
            {
                if (floor.Contains(candidate + direction))
                    floorNeighbors++;
            }

            bool up =
                floor.Contains(candidate + Vector2Int.up);

            bool right =
                floor.Contains(candidate + Vector2Int.right);

            bool down =
                floor.Contains(candidate + Vector2Int.down);

            bool left =
                floor.Contains(candidate + Vector2Int.left);

            bool inwardCorner =
                (up && right) ||
                (right && down) ||
                (down && left) ||
                (left && up);

            if (floorNeighbors >= 3 || inwardCorner)
                result.Add(candidate);
        }

        return result;
    }

    private void PositionCharacters(
        List<RoomDefinition> rooms,
        System.Random random)
    {
        if (player == null)
        {
            Debug.LogError(
                "Assign the Player and Minotaur scene objects.",
                this
            );
            return;
        }

        int tutorialRoomIndex = -1;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].PlayerSpawnPoint == null)
                continue;

            if (tutorialRoomIndex != -1)
            {
                Debug.LogError(
                    "Multiple generated rooms have Player Spawn Points. " +
                    "Assign one only on the tutorial room, and generate " +
                    "exactly one tutorial room.",
                    this
                );
                return;
            }

            tutorialRoomIndex = i;
        }

        if (tutorialRoomIndex == -1)
        {
            Debug.LogError(
                "No tutorial spawn point was found. Make sure the " +
                "tutorial room is generated and its Player Spawn Point " +
                "is assigned.",
                this
            );
            return;
        }

        if (rooms.Count < 2)
        {
            Debug.LogError(
                "Generate at least two rooms so the Minotaur " +
                "can start outside the tutorial room.",
                this
            );
            return;
        }

        RoomDefinition tutorialRoom =
            rooms[tutorialRoomIndex];

        player.position =
            tutorialRoom.PlayerSpawnPoint.position;

        Debug.Log(
            $"Player starts in {tutorialRoom.name}; " + this
        );
    }

    private Vector3 GetRoomCenterPosition(RoomDefinition room)
    {
        Tilemap floor = room.FloorTilemap;
        BoundsInt bounds = floor.cellBounds;

        Vector3 center = (
            floor.GetCellCenterWorld(bounds.min) +
            floor.GetCellCenterWorld(
                bounds.max - Vector3Int.one
            )
        ) * 0.5f;

        Vector3 closestPosition = Vector3.zero;
        float closestDistance = float.PositiveInfinity;
        bool foundFloor = false;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!floor.HasTile(cell))
                continue;

            Vector3 position =
                floor.GetCellCenterWorld(cell);

            float distance =
                (position - center).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPosition = position;
                foundFloor = true;
            }
        }

        if (!foundFloor)
        {
            throw new InvalidOperationException(
                $"{room.name}: cannot spawn characters because " +
                "its Floor Tilemap has no painted tiles."
            );
        }

        return closestPosition;
    }

    private List<SelectedRoom> SelectRooms(System.Random random)
    {
        List<SelectedRoom> selected =
            new List<SelectedRoom>();

        List<RoomDefinition> chosenPrefabs =
            roomCatalogue.PickRooms(roomCount, random);

        foreach (RoomDefinition prefab in chosenPrefabs)
        {
            if (prefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    $"{prefab.name}: use a prefab from the " +
                    "Project window, not a scene object."
                );
            }

            if (prefab.FloorTilemap == null ||
                prefab.PlacementAnchor == null)
            {
                throw new InvalidOperationException(
                    $"{prefab.name}: assign its floor and anchor."
                );
            }

            BoundsInt bounds = prefab.GetLocalBounds();

            if (bounds.size.x < 1 || bounds.size.y < 1)
            {
                throw new InvalidOperationException(
                    $"{prefab.name}: the room has no valid bounds."
                );
            }

            Vector3Int anchor = prefab.GetAnchorCell();

            selected.Add(new SelectedRoom
            {
                prefab = prefab,
                localBounds = bounds,
                localAnchor = new Vector2Int(
                    anchor.x,
                    anchor.y
                )
            });
        }

        // Fit larger rooms first.
        selected.Sort((a, b) =>
        {
            long areaA =
                (long)a.localBounds.size.x *
                a.localBounds.size.y;

            long areaB =
                (long)b.localBounds.size.x *
                b.localBounds.size.y;

            return areaB.CompareTo(areaA);
        });

        return selected;
    }

    private List<PlacedRoom> CreateLayout(
        List<SelectedRoom> selectedRooms,
        System.Random random)
    {
        RectInt dungeonArea =
            new RectInt(dungeonOrigin, dungeonSize);

        for (int attempt = 0;
             attempt < layoutAttempts;
             attempt++)
        {
            List<PlacedRoom> placed =
                new List<PlacedRoom>();

            foreach (SelectedRoom room in selectedRooms)
            {
                PlacedRoom placement = FindPlacement(
                    room,
                    placed,
                    dungeonArea,
                    random
                );

                if (placement == null)
                    break;

                placed.Add(placement);
            }

            if (placed.Count == selectedRooms.Count)
                return placed;
        }

        return null;
    }

    private PlacedRoom FindPlacement(
        SelectedRoom room,
        List<PlacedRoom> placed,
        RectInt dungeonArea,
        System.Random random)
    {
        int width = room.localBounds.size.x;
        int height = room.localBounds.size.y;

        int minimumX = dungeonArea.xMin + roomSpacing;
        int minimumY = dungeonArea.yMin + roomSpacing;

        int maximumX =
            dungeonArea.xMax - roomSpacing - width;

        int maximumY =
            dungeonArea.yMax - roomSpacing - height;

        if (maximumX < minimumX ||
            maximumY < minimumY)
        {
            return null;
        }

        for (int attempt = 0;
             attempt < attemptsPerRoom;
             attempt++)
        {
            int x = random.Next(minimumX, maximumX + 1);
            int y = random.Next(minimumY, maximumY + 1);

            RectInt bounds =
                new RectInt(x, y, width, height);

            RectInt paddedBounds = new RectInt(
                x - roomSpacing,
                y - roomSpacing,
                width + roomSpacing * 2,
                height + roomSpacing * 2
            );

            bool overlaps = false;

            foreach (PlacedRoom other in placed)
            {
                if (paddedBounds.Overlaps(other.bounds))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            Vector2Int localMinimum = new Vector2Int(
                room.localBounds.xMin,
                room.localBounds.yMin
            );

            Vector2Int destinationAnchor =
                bounds.position +
                room.localAnchor -
                localMinimum;

            return new PlacedRoom
            {
                selected = room,
                bounds = bounds,
                destinationAnchor = destinationAnchor
            };
        }

        return null;
    }

    private void SpawnRooms(
        List<PlacedRoom> layout,
        System.Random random)
    {
        if (tilemapVisualizer == null ||
            tilemapVisualizer.FloorTilemap != dungeonFloor)
        {
            throw new InvalidOperationException(
                "Assign a TileMapVisualizer that uses " +
                "the same Dungeon Floor tilemap."
            );
        }

        GameObject newRoot =
            new GameObject("Generated Artist Rooms");

        List<RoomDefinition> instances =
            new List<RoomDefinition>();

        List<RectInt> bounds =
            new List<RectInt>();

        HashSet<Vector2Int> corridors;

        try
        {
            foreach (PlacedRoom placement in layout)
            {
#if UNITY_EDITOR
                RoomDefinition source =
                    placement.selected.prefab;

                string prefabPath =
                    UnityEditor.AssetDatabase.GetAssetPath(
                        source.gameObject
                    );

                Debug.Log(
                    $"ROOM SOURCE\n" +
                    $"Prefab: {prefabPath}\n" +
                    $"Room component: {source.name}",
                    source
                );
#endif

                RoomDefinition instance = Instantiate(
                    placement.selected.prefab,
                    newRoot.transform,
                    false
                );

                Vector3Int anchorCell =
                    instance.GetAnchorCell();

                Vector3 anchorOffset =
                    instance.PlacementAnchor.position -
                    instance.FloorTilemap.GetCellCenterWorld(
                        anchorCell
                    );

                Debug.Log(
                    $"CLONED ANCHOR BEFORE PLACEMENT\n" +
                    $"Position: {instance.PlacementAnchor.position.ToString("F6")}\n" +
                    $"Offset: {anchorOffset.ToString("F6")}",
                    instance
                );

                instance.PlaceAt(
                    dungeonFloor,
                    placement.destinationAnchor
                );

                instances.Add(instance);
                bounds.Add(placement.bounds);
            }

            RectInt dungeonArea =
                new RectInt(dungeonOrigin, dungeonSize);

            corridors = RoomCorridorBuilder.Build(
    instances,
    bounds,
    dungeonFloor,
    new RectInt(dungeonOrigin, dungeonSize),
    random,
    Mathf.Max(mazeSpacing, corridorRadius * 2 + 2),
    mazeLoopChance
);

            AddRandomBranches(
                corridors,
                bounds,
                dungeonArea,
                random
            );

            corridors = WidenCorridors(
                corridors,
                bounds,
                dungeonArea
            );

            for (int pass = 0;
                 pass < smoothingPasses;
                 pass++)
            {
                corridors = SmoothCorridorFloor(
                    corridors,
                    bounds,
                    dungeonArea
                );
            }
        }
        catch
        {
            if (Application.isPlaying)
                Destroy(newRoot);
            else
                DestroyImmediate(newRoot);

            throw;
        }

        // Replace old rooms only after routing succeeds.
        if (generatedRoomRoot != null)
        {
            generatedRoomRoot.SetActive(false);

            if (Application.isPlaying)
                Destroy(generatedRoomRoot);
            else
                DestroyImmediate(generatedRoomRoot);
        }

        WalkableFloorReady = false;
        generatedRoomRoot = newRoot;

        tilemapVisualizer.Clear();
        tilemapVisualizer.PaintFloorTiles(corridors);

        GenerateCorridorWalls(corridors, instances);

        generatedRoomRoot.SetActive(true);

        PositionCharacters(instances, random);
        RefreshWalkableFloors();

        if (corridorEnemySpawner != null)
        {
            corridorEnemySpawner.Spawn(
                corridors,
                dungeonFloor,
                instances,
                player,
                random
            );
        }
    }

    private HashSet<Vector2Int> WidenCorridors(
        HashSet<Vector2Int> corridors,
        List<RectInt> roomBounds,
        RectInt dungeonArea)
    {
        HashSet<Vector2Int> widened =
            new HashSet<Vector2Int>(corridors);

        int radius = Mathf.Max(0, corridorRadius);

        foreach (Vector2Int center in corridors)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int candidate =
                        center + new Vector2Int(x, y);

                    if (!dungeonArea.Contains(candidate))
                        continue;

                    bool insideRoom = false;

                    foreach (RectInt room in roomBounds)
                    {
                        if (room.Contains(candidate))
                        {
                            insideRoom = true;
                            break;
                        }
                    }

                    if (insideRoom)
                        continue;

                    widened.Add(candidate);
                }
            }
        }

        return widened;
    }

    private void GenerateCorridorWalls(
        HashSet<Vector2Int> corridors,
        List<RoomDefinition> rooms)
    {
        HashSet<Vector2Int> allFloorPositions =
            new HashSet<Vector2Int>(corridors);

        HashSet<Vector2Int> protectedCells =
            new HashSet<Vector2Int>();

        foreach (RoomDefinition room in rooms)
        {
            Tilemap[] tilemaps =
                room.GetComponentsInChildren<Tilemap>(true);

            foreach (Tilemap tilemap in tilemaps)
            {
                foreach (Vector3Int cell in
                         tilemap.cellBounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell))
                        continue;

                    Vector3 worldPosition =
                        tilemap.GetCellCenterWorld(cell);

                    Vector3Int dungeonCell =
                        dungeonFloor.WorldToCell(worldPosition);

                    Vector2Int position = new Vector2Int(
                        dungeonCell.x,
                        dungeonCell.y
                    );

                    // Protect floor, walls and decorations.
                    protectedCells.Add(position);

                    if (tilemap == room.FloorTilemap)
                        allFloorPositions.Add(position);
                }
            }
        }

        if (corridorWallPainter == null)
        {
            throw new InvalidOperationException(
                "Assign the Corridor Wall Painter."
            );
        }

        corridorWallPainter.Paint(
            corridors,
            allFloorPositions,
            protectedCells
        );
    }

    private void AddRandomBranches(
        HashSet<Vector2Int> corridors,
        List<RectInt> roomBounds,
        RectInt dungeonArea,
        System.Random random)
    {
        if (corridors.Count == 0 || deadEndCount <= 0)
            return;

        int minimumLength =
            Mathf.Max(1, minimumBranchLength);

        int maximumLength = Mathf.Max(
            minimumLength,
            maximumBranchLength
        );

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        HashSet<Vector2Int> protectedCells =
            new HashSet<Vector2Int>();

        foreach (RectInt room in roomBounds)
        {
            for (int x = room.xMin - 1;
                 x < room.xMax + 1;
                 x++)
            {
                for (int y = room.yMin - 1;
                     y < room.yMax + 1;
                     y++)
                {
                    protectedCells.Add(
                        new Vector2Int(x, y)
                    );
                }
            }
        }

        List<Vector2Int> startingCells =
            new List<Vector2Int>(corridors);

        startingCells.Sort((a, b) =>
        {
            int comparison = a.x.CompareTo(b.x);

            return comparison != 0
                ? comparison
                : a.y.CompareTo(b.y);
        });

        int completedBranches = 0;
        int attemptLimit = deadEndCount * 40;

        for (int attempt = 0;
             attempt < attemptLimit &&
             completedBranches < deadEndCount;
             attempt++)
        {
            Vector2Int current =
                startingCells[random.Next(startingCells.Count)];

            Vector2Int previousDirection =
                Vector2Int.zero;

            int targetLength = random.Next(
                minimumLength,
                maximumLength + 1
            );

            List<Vector2Int> branch =
                new List<Vector2Int>();

            HashSet<Vector2Int> branchCells =
                new HashSet<Vector2Int>();

            for (int step = 0;
                 step < targetLength;
                 step++)
            {
                List<Vector2Int> availableDirections =
                    new List<Vector2Int>();

                foreach (Vector2Int direction in directions)
                {
                    Vector2Int candidate =
                        current + direction;

                    if (!dungeonArea.Contains(candidate) ||
                        protectedCells.Contains(candidate) ||
                        corridors.Contains(candidate) ||
                        branchCells.Contains(candidate))
                    {
                        continue;
                    }

                    bool touchesOtherCorridor = false;

                    foreach (Vector2Int neighborDirection in
                             directions)
                    {
                        Vector2Int neighbor =
                            candidate + neighborDirection;

                        if (neighbor == current)
                            continue;

                        if (corridors.Contains(neighbor) ||
                            branchCells.Contains(neighbor))
                        {
                            touchesOtherCorridor = true;
                            break;
                        }
                    }

                    if (!touchesOtherCorridor)
                        availableDirections.Add(direction);
                }

                if (availableDirections.Count == 0)
                    break;

                Vector2Int chosenDirection;

                if (availableDirections.Contains(
                        previousDirection
                    ) &&
                    random.NextDouble() < straightPreference)
                {
                    chosenDirection = previousDirection;
                }
                else
                {
                    chosenDirection =
                        availableDirections[
                            random.Next(availableDirections.Count)
                        ];
                }

                current += chosenDirection;
                previousDirection = chosenDirection;

                branch.Add(current);
                branchCells.Add(current);
            }

            if (branch.Count < minimumLength)
                continue;

            corridors.UnionWith(branchCells);
            startingCells.AddRange(branch);

            completedBranches++;
        }

        Debug.Log(
            $"Added {completedBranches} of {deadEndCount} " +
            "requested corridor branches.",
            this
        );
    }
}