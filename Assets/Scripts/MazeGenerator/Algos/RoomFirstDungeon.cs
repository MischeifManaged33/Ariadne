using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class RoomFirstDungeon : SimpleRandomWalkDungeonGenerator
{
    [SerializeField]
    private int minRoomWidth = 4, minRoomHeight = 4;
    [SerializeField]
    private int dungeonWidth = 20, dungeonHeight = 20;
    [SerializeField]
    [Range(0, 10)]
    private int offset = 1;
    [SerializeField]
    private bool randomWalkRooms = false;
    [SerializeField]
    [Range(0f, 1f)]
    private float extraCorridorChance = .25f;
    [SerializeField]
    [Range(0f, 1f)]
    private float deadEndChance = 0.3f;
    [SerializeField]
    private int minDeadEndLength = 3;
    [SerializeField]
    private int maxDeadEndLength = 8;
    [SerializeField]
    [Range(0f, 1f)]
    private float branchChance = 0.25f;
    [SerializeField]
    private int minBranchLength = 3;
    [SerializeField]
    private int maxBranchLength = 7;
    [SerializeField]
    private GameObject player;
    [SerializeField]
    private GameObject goal;
    [SerializeField]
    [Range(0f, 1f)]
    private float corridorWindiness = 0.3f;
    private DungeonRegion currentPlayerRegion;
    private HashSet<Vector2Int> permanentFloorPositions = new HashSet<Vector2Int>();
    private bool isRegenerating;

    private List<DungeonRegion> dungeonRegions = new List<DungeonRegion>();
    private Vector2Int startRoom;
    private Vector2Int goalRoom;


    protected override void RunProceduralGeneration()
    {
        CreateRooms();
    }

    private void CreateRooms()
    {
        tilemapVisualizer.Clear();
        permanentFloorPositions.Clear();
        dungeonRegions.Clear();
        currentPlayerRegion = null;

        var roomList =
            ProceduralGenerationAlgorithms.BinarySpacePartitioning(
                new BoundsInt(
                    (Vector3Int)startPos,
                    new Vector3Int(dungeonWidth, dungeonHeight, 1)
                ),
                minRoomWidth,
                minRoomHeight
            );

        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        floor = CreateSimpleRooms(roomList);

        List<Vector2Int> roomCenters = new List<Vector2Int>();
        foreach (var room in roomList)
        {
            roomCenters.Add((Vector2Int)Vector3Int.RoundToInt(room.center));
        }


        HashSet<Vector2Int> corridors = ConnectRooms(new List<Vector2Int>(roomCenters));
        floor.UnionWith(corridors);


        var rand = Random.Range(0, roomCenters.Count);

        startRoom = roomCenters[rand];

        goalRoom = roomCenters[Random.Range(0, roomCenters.Count)];

        player.transform.position = tilemapVisualizer.GetFloorWorldPosition(startRoom);

        goal.transform.position = tilemapVisualizer.GetFloorWorldPosition(goalRoom); ;

        tilemapVisualizer.PaintFloorTiles(floor);

        tilemapVisualizer.PaintIsometricWalls(floor);

        nodeGenerator.GenerateNodes(floor);

        int roomCount = 0;
        int corridorCount = 0;

        foreach (DungeonRegion region in dungeonRegions)
        {
            if (region.Type == DungeonRegionType.Room)
                roomCount++;
            else if (region.Type == DungeonRegionType.Corridor)
                corridorCount++;
        }

        Debug.Log(
            $"Tracked {roomCount} rooms and " +
            $"{corridorCount} corridors."
        );
    } 

    private DungeonRegion FindRegionAtPosition (Vector2Int position)
    {
        foreach (DungeonRegion region in dungeonRegions)
        {
            if (region.Type == DungeonRegionType.Room && region.Contains(position))
            {
                return region;
            }
        }

        foreach (DungeonRegion region in dungeonRegions)
        {
            if (region.Type == DungeonRegionType.Corridor && region.Contains(position))
            {
                return region;
            }
        }

        return null;
    }

    private void Update()
    {
        TrackPlayerRegion();
    }

    private void TrackPlayerRegion()
    {
        if (isRegenerating ||
        player == null ||
        dungeonRegions.Count == 0)
        {
            return;
        }

        Vector2Int playerCell = tilemapVisualizer.GetFloorCellPosition(player.transform.position);

        DungeonRegion newRegion = FindRegionAtPosition(playerCell);

        if (newRegion == null)
            return;
        if (newRegion == currentPlayerRegion)
            return;

        DungeonRegion previousRegion = currentPlayerRegion;
        currentPlayerRegion = newRegion;
        currentPlayerRegion.Visited = true;

        permanentFloorPositions.UnionWith(currentPlayerRegion.FloorPositions);

        Debug.Log(
        $"Player entered {currentPlayerRegion.Type} " +
        $"at {currentPlayerRegion.Center}"
    );

        if (previousRegion != null &&
    previousRegion.Type == DungeonRegionType.Room &&
    currentPlayerRegion.Type ==
        DungeonRegionType.Corridor)
        {
            Debug.Log(
                $"REGENERATION POINT: Player left room " +
                $"{previousRegion.Center}. " +
                $"{permanentFloorPositions.Count} tiles " +
                $"must be preserved."
            );

            PrepareForRegeneration();
        }
    }

    private void PrepareForRegeneration()
    {
        isRegenerating = true;

        try
        {
            List<DungeonRegion> visitedRegions = new List<DungeonRegion>();

            foreach (DungeonRegion region in dungeonRegions)
            {
                if (region.Visited)
                    visitedRegions.Add(region);
            }

            dungeonRegions.Clear();
            dungeonRegions.AddRange(visitedRegions);

            List<BoundsInt> newRoomBounds = ProceduralGenerationAlgorithms.BinarySpacePartitioning(new BoundsInt((Vector3Int)startPos, new Vector3Int(dungeonWidth, dungeonHeight, 0)), minRoomWidth, minRoomHeight);

            newRoomBounds.RemoveAll(RoomOverlapsPermanentFloor);

            HashSet<Vector2Int> newFloor = CreateSimpleRooms(newRoomBounds);

            List<Vector2Int> newRoomCenters = new List<Vector2Int>();
            
            foreach (BoundsInt room in newRoomBounds)
            {
                Vector2Int center = (Vector2Int)Vector3Int.RoundToInt(room.center);
                newRoomCenters.Add(center);
            }

            if (newRoomCenters.Count > 0)
            {
                HashSet<Vector2Int> newCorridors = ConnectRooms(new List<Vector2Int>(newRoomCenters));

                newFloor.UnionWith(newCorridors);

                foreach (DungeonRegion region in visitedRegions)
                {
                    if (region.Type != DungeonRegionType.Room)
                        continue;

                    Vector2Int closestNewRoom = FindClosestCenter(region.Center, newRoomCenters);

                    HashSet<Vector2Int> connection = CreateCorridor(region.Center, closestNewRoom);

                    newFloor.UnionWith(connection);
                    RegisterCorridor(connection);
                }
            }
            else
            {
                Debug.LogWarning("No space for remaining replacement rooms");
            }
            HashSet<Vector2Int> completeFloor = new HashSet<Vector2Int>(permanentFloorPositions);

            completeFloor.UnionWith(newFloor);

            tilemapVisualizer.Clear();

            tilemapVisualizer.PaintFloorTiles(
                completeFloor
            );

            tilemapVisualizer.PaintIsometricWalls(
                completeFloor
            );

            Debug.Log(
                $"Regenerated dungeon with " +
                $"{completeFloor.Count} total floor tiles. " +
                $"{permanentFloorPositions.Count} are permanent."
            );
        } finally
        {
            isRegenerating = false;
        }        
    }

    private bool RoomOverlapsPermanentFloor(BoundsInt room)
    {
        for (int col = offset; col < room.size.x - offset; col++)
        {
            for (int row = offset; row < room.size.y - offset; row++)
            {
                Vector2Int position = (Vector2Int)room.min + new Vector2Int(col, row);

                if (permanentFloorPositions.Contains(position))
                    return true;
            }
        }
        return false;
    }

    private Vector2Int FindClosestCenter(
    Vector2Int position,
    List<Vector2Int> centers)
    {
        Vector2Int closest = centers[0];
        float closestDistance = float.MaxValue;

        foreach (Vector2Int center in centers)
        {
            float distance =
                Vector2Int.Distance(position, center);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = center;
            }
        }

        return closest;
    }

    private void RegisterCorridor(HashSet<Vector2Int> corridorFloor)
    {
        if (corridorFloor.Count == 0)
            return;

        int totalX = 0;
        int totalY = 0;

        foreach (Vector2Int position in corridorFloor)
        {
            totalX += position.x;
            totalY += position.y;
        }

        Vector2Int corridorCenter = new Vector2Int(Mathf.RoundToInt((float)totalX / corridorFloor.Count), Mathf.RoundToInt((float)totalY / corridorFloor.Count));

        DungeonRegion corridorRegion = new DungeonRegion(DungeonRegionType.Corridor, corridorFloor, corridorCenter);

        dungeonRegions.Add(corridorRegion);
    }

    private HashSet<Vector2Int> ConnectRooms(List<Vector2Int> roomCenters)
    {
        HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
        List<Vector2Int> connectedRooms = new List<Vector2Int>();

        // Keep track of existing connections
        HashSet<(Vector2Int, Vector2Int)> connections =
            new HashSet<(Vector2Int, Vector2Int)>();

        // Pick a starting room
        Vector2Int currentRoom =
            roomCenters[Random.Range(0, roomCenters.Count)];

        connectedRooms.Add(currentRoom);
        roomCenters.Remove(currentRoom);

        // Build the main connected network
        while (roomCenters.Count > 0)
        {
            float closestDistance = float.MaxValue;
            Vector2Int closestRoom = Vector2Int.zero;
            Vector2Int closestConnectedRoom = Vector2Int.zero;

            foreach (Vector2Int connectedRoom in connectedRooms)
            {
                foreach (Vector2Int unconnectedRoom in roomCenters)
                {
                    float distance =
                        Vector2Int.Distance(connectedRoom, unconnectedRoom);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestConnectedRoom = connectedRoom;
                        closestRoom = unconnectedRoom;
                    }
                }
            }

            // Create corridor
            HashSet<Vector2Int> newCorridor =
                CreateCorridor(closestConnectedRoom, closestRoom);

            corridors.UnionWith(newCorridor);
            RegisterCorridor(newCorridor);

            // Remember this connection
            connections.Add((closestConnectedRoom, closestRoom));
            connections.Add((closestRoom, closestConnectedRoom));

            // Add room to network
            connectedRooms.Add(closestRoom);
            roomCenters.Remove(closestRoom);
        }

        // Add extra corridors
        foreach (Vector2Int room in connectedRooms)
        {
            if (Random.value > extraCorridorChance)
                continue;

            Vector2Int closestRoom = FindClosestUnconnectedRoom(
                room,
                connectedRooms,
                connections
            );

            if (closestRoom != room)
            {
                HashSet<Vector2Int> extraCorridor =
                    CreateCorridor(room, closestRoom);

                corridors.UnionWith(extraCorridor);
                RegisterCorridor(extraCorridor);

                connections.Add((room, closestRoom));
                connections.Add((closestRoom, room));
            }
        }

        foreach (Vector2Int room in connectedRooms)
        {
            if (Random.value > deadEndChance)
                continue;

            int length = Random.Range(
                minDeadEndLength,
                maxDeadEndLength + 1
            );

            HashSet<Vector2Int> deadEnd = CreateDeadEnd(room, length);

            corridors.UnionWith(deadEnd);
            RegisterCorridor(deadEnd);
        }

        foreach (Vector2Int room in connectedRooms)
        {
            if (Random.value > branchChance)
                continue;

            int length = Random.Range(
                minBranchLength,
                maxBranchLength + 1
            );

            Vector2Int direction = Direction2D.GetRandCardDir();

            Vector2Int branchStart = room + direction;

            HashSet<Vector2Int> branch =
                CreateBranch(branchStart, length);

            corridors.UnionWith(branch);
            RegisterCorridor(branch);
        }

        return corridors;
    }

    private Vector2Int FindClosestUnconnectedRoom(
    Vector2Int currentRoom,
    List<Vector2Int> rooms,
    HashSet<(Vector2Int, Vector2Int)> connections)
    {
        Vector2Int closest = currentRoom;
        float distance = float.MaxValue;

        foreach (Vector2Int room in rooms)
        {
            if (room == currentRoom)
                continue;

            // Don't connect rooms that already have a corridor
            if (connections.Contains((currentRoom, room)))
                continue;

            float currentDistance =
                Vector2.Distance(currentRoom, room);

            if (currentDistance < distance)
            {
                distance = currentDistance;
                closest = room;
            }
        }

        return closest;
    }

    private HashSet<Vector2Int> CreateCorridor(
    Vector2Int currentRoomCenter,
    Vector2Int closest)
    {
        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();

        Vector2Int position = currentRoomCenter;

        corridor.Add(position);

        while (position != closest)
        {
            Vector2Int direction;

            // Determine the directions that move us toward the target
            Vector2Int horizontalDirection = closest.x > position.x
                ? Vector2Int.right
                : Vector2Int.left;

            Vector2Int verticalDirection = closest.y > position.y
                ? Vector2Int.up
                : Vector2Int.down;

            bool canMoveHorizontal = position.x != closest.x;
            bool canMoveVertical = position.y != closest.y;

            // If we're already aligned, we have to move in that direction
            if (!canMoveHorizontal)
            {
                direction = verticalDirection;
            }
            else if (!canMoveVertical)
            {
                direction = horizontalDirection;
            }
            else
            {
                // Occasionally prioritize changing direction
                if (Random.value < corridorWindiness)
                {
                    // Pick one of the two directions toward the target
                    direction = Random.value < 0.5f
                        ? horizontalDirection
                        : verticalDirection;
                }
                else
                {
                    // Favor the direction with the greater distance
                    int xDistance = Mathf.Abs(closest.x - position.x);
                    int yDistance = Mathf.Abs(closest.y - position.y);

                    if (xDistance > yDistance)
                        direction = horizontalDirection;
                    else
                        direction = verticalDirection;
                }
            }

            position += direction;
            corridor.Add(position);
        }

        return corridor;
    }

    private HashSet<Vector2Int> CreateDeadEnd(Vector2Int startPosition,int length)
    {
        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();

        Vector2Int position = startPosition;

        corridor.Add(position);

        Vector2Int direction = Direction2D.GetRandCardDir();

        for (int i = 0; i < length; i++)
        {
            position += direction;
            corridor.Add(position);
        }

        return corridor;
    }

    private HashSet<Vector2Int> CreateBranch(
    Vector2Int startPosition,
    int length)
    {
        HashSet<Vector2Int> branch = new HashSet<Vector2Int>();

        Vector2Int position = startPosition;

        branch.Add(position);

        Vector2Int previousDirection = Direction2D.GetRandCardDir();

        for (int i = 0; i < length; i++)
        {
            Vector2Int direction;

            // Usually continue in the same direction
            if (Random.value < 0.7f)
            {
                direction = previousDirection;
            }
            else
            {
                // Occasionally turn
                direction = Direction2D.GetRandCardDir();
            }

            position += direction;

            branch.Add(position);

            previousDirection = direction;
        }

        return branch;
    }

    private Vector2Int FindClosestPointTo(Vector2Int currentRoomCenter, List<Vector2Int> roomCenters)
    {
        Vector2Int closest = Vector2Int.zero;
        float distance = float.MaxValue;

        foreach (var room in roomCenters)
        {
            float currentDistance = Vector2.Distance(room,  currentRoomCenter);
            if (currentDistance < distance)
            {
                distance = currentDistance;
                closest = room;
            }
        }
        return closest;
    }

    private HashSet<Vector2Int> CreateSimpleRooms(
    List<BoundsInt> roomList)
    {
        HashSet<Vector2Int> completeRoomFloor =
            new HashSet<Vector2Int>();

        foreach (BoundsInt room in roomList)
        {
            HashSet<Vector2Int> individualRoomFloor =
                new HashSet<Vector2Int>();

            for (
                int col = offset;
                col < room.size.x - offset;
                col++)
            {
                for (
                    int row = offset;
                    row < room.size.y - offset;
                    row++)
                {
                    Vector2Int position =
                        (Vector2Int)room.min +
                        new Vector2Int(col, row);

                    individualRoomFloor.Add(position);
                    completeRoomFloor.Add(position);
                }
            }

            Vector2Int roomCenter =
                (Vector2Int)Vector3Int.RoundToInt(
                    room.center
                );

            DungeonRegion roomRegion =
                new DungeonRegion(
                    DungeonRegionType.Room,
                    individualRoomFloor,
                    roomCenter
                );

            dungeonRegions.Add(roomRegion);
        }

        return completeRoomFloor;
    }
}
