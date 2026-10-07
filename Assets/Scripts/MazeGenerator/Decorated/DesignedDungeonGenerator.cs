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
    [SerializeField] private TileMapVisualizer tilemapVisualizer;

    [Header("Corridor Width")]
    [Tooltip("0 = 1 cell wide, 1 = 3 cells, 2 = 5 cells.")]
    [SerializeField, Range(0, 3)]
    private int corridorRadius = 1;

    [Header("Extra Corridors")]
    [SerializeField, Min(0)]
    private int deadEndCount = 20;

    [Header("Character Spawning")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform minotaur;

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
        if (generateOnStart)
            GenerateDungeon();
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
            System.Random random = new System.Random(seed);

            // Choose every design BEFORE planning placement.
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

    private void PositionCharacters(
    List<RoomDefinition> rooms,
    System.Random random)
    {
        if (player == null || minotaur == null)
        {
            Debug.LogError(
                "Assign the Player and Minotaur scene objects.",
                this
            );
            return;
        }

        if (rooms.Count < 2)
        {
            Debug.LogError(
                "Generate at least two rooms so the Player " +
                "and Minotaur can start in different rooms.",
                this
            );
            return;
        }

        int playerRoomIndex = random.Next(rooms.Count);

        // Choose among every room except the player's room.
        int minotaurRoomIndex = random.Next(rooms.Count - 1);

        if (minotaurRoomIndex >= playerRoomIndex)
            minotaurRoomIndex++;

        RoomDefinition playerRoom = rooms[playerRoomIndex];
        RoomDefinition minotaurRoom = rooms[minotaurRoomIndex];

        player.position = GetRoomCenterPosition(playerRoom);
        minotaur.position = GetRoomCenterPosition(minotaurRoom);

        Debug.Log(
            $"Player starts in {playerRoom.name}; " +
            $"Minotaur starts in {minotaurRoom.name}.",
            this
        );
    }

    private Vector3 GetRoomCenterPosition(RoomDefinition room)
    {
        var floor = room.FloorTilemap;
        BoundsInt bounds = floor.cellBounds;

        Vector3 center = (
    floor.GetCellCenterWorld(bounds.min) +
    floor.GetCellCenterWorld(bounds.max - Vector3Int.one)
) * 0.5f;

        Vector3 closestPosition = Vector3.zero;
        float closestDistance = float.PositiveInfinity;
        bool foundFloor = false;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!floor.HasTile(cell))
                continue;

            Vector3 position = floor.GetCellCenterWorld(cell);
            float distance = (position - center).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPosition = position;
                foundFloor = true;
            }
        }

        if (!foundFloor)
        {
            throw new System.InvalidOperationException(
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

        for (int i = 0; i < roomCount; i++)
        {
            RoomDefinition prefab =
                roomCatalogue.PickRoom(random);

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

        // Fit larger rooms first without changing which
        // designs were selected.
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
                PlacedRoom placement =
                    FindPlacement(room, placed, dungeonArea, random);

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

        if (maximumX < minimumX || maximumY < minimumY)
            return null;

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

            // The room's minimum cell goes at bounds.position.
            // Its anchor can be anywhere within the room.
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
RoomDefinition source = placement.selected.prefab;

string prefabPath =
    UnityEditor.AssetDatabase.GetAssetPath(source.gameObject);

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

                Vector3Int anchorCell = instance.GetAnchorCell();

                Vector3 anchorOffset =
                    instance.PlacementAnchor.position -
                    instance.FloorTilemap.GetCellCenterWorld(anchorCell);

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

            corridors = RoomCorridorBuilder.Build(instances, bounds, dungeonFloor, new RectInt(dungeonOrigin, dungeonSize), random);
            AddRandomBranches(corridors, bounds, new RectInt(dungeonOrigin, dungeonSize), random);
            corridors = WidenCorridors(corridors, bounds, new RectInt(dungeonOrigin, dungeonSize));
        }
        catch
        {
            if (Application.isPlaying)
                Destroy(newRoot);
            else
                DestroyImmediate(newRoot);

            throw;
        }

        // Replace the previous rooms after routing succeeds.
        if (generatedRoomRoot != null)
        {
            generatedRoomRoot.SetActive(false);

            if (Application.isPlaying)
                Destroy(generatedRoomRoot);
            else
                DestroyImmediate(generatedRoomRoot);
        }

        generatedRoomRoot = newRoot;

        tilemapVisualizer.Clear();
        tilemapVisualizer.PaintFloorTiles(corridors);

        GenerateCorridorWalls(corridors, instances);

        generatedRoomRoot.SetActive(true);

        PositionCharacters(instances, random);
    }

    private HashSet<Vector2Int> WidenCorridors(
    HashSet<Vector2Int> corridors,
    List<RectInt> roomBounds,
    RectInt dungeonArea)
    {
        // Preserve the original paths, including entrance passages.
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

                    // Extra floor must not cover the authored room.
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
            UnityEngine.Tilemaps.Tilemap[] tilemaps =
                room.GetComponentsInChildren<
                    UnityEngine.Tilemaps.Tilemap
                >(true);

            foreach (UnityEngine.Tilemaps.Tilemap tilemap in tilemaps)
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

                    // Protect every painted room tile:
                    // floor, walls, and decorations.
                    protectedCells.Add(position);

                    if (tilemap == room.FloorTilemap)
                        allFloorPositions.Add(position);
                }
            }
        }

        BasicWallPlacer.CreateWalls(
            corridors,
            allFloorPositions,
            protectedCells,
            tilemapVisualizer
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

        int minimumLength = Mathf.Max(1, minimumBranchLength);
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

        // Protect the entire room footprint and its clearance.
        HashSet<Vector2Int> protectedCells =
            new HashSet<Vector2Int>();

        foreach (RectInt room in roomBounds)
        {
            for (int x = room.xMin - 1; x < room.xMax + 1; x++)
            {
                for (int y = room.yMin - 1; y < room.yMax + 1; y++)
                {
                    protectedCells.Add(new Vector2Int(x, y));
                }
            }
        }

        List<Vector2Int> startingCells =
            new List<Vector2Int>(corridors);

        // Stable ordering makes the same seed repeatable.
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

            Vector2Int previousDirection = Vector2Int.zero;

            int targetLength = random.Next(
                minimumLength,
                maximumLength + 1
            );

            List<Vector2Int> branch =
                new List<Vector2Int>();

            HashSet<Vector2Int> branchCells =
                new HashSet<Vector2Int>();

            for (int step = 0; step < targetLength; step++)
            {
                List<Vector2Int> availableDirections =
                    new List<Vector2Int>();

                foreach (Vector2Int direction in directions)
                {
                    Vector2Int candidate = current + direction;

                    if (!dungeonArea.Contains(candidate) ||
                        protectedCells.Contains(candidate) ||
                        corridors.Contains(candidate) ||
                        branchCells.Contains(candidate))
                    {
                        continue;
                    }

                    bool touchesOtherCorridor = false;

                    foreach (Vector2Int neighborDirection in directions)
                    {
                        Vector2Int neighbor =
                            candidate + neighborDirection;

                        // Connection to the previous cell is allowed.
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

                if (availableDirections.Contains(previousDirection) &&
                    random.NextDouble() < straightPreference)
                {
                    chosenDirection = previousDirection;
                }
                else
                {
                    chosenDirection = availableDirections[
                        random.Next(availableDirections.Count)
                    ];
                }

                current += chosenDirection;
                previousDirection = chosenDirection;

                branch.Add(current);
                branchCells.Add(current);
            }

            // Discard branches that could not grow far enough.
            if (branch.Count < minimumLength)
                continue;

            corridors.UnionWith(branchCells);

            // Future branches can grow from these new corridors too.
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