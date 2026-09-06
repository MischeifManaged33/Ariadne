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

    private Vector2Int startRoom;
    private Vector2Int goalRoom;


    protected override void RunProceduralGeneration()
    {
        CreateRooms();
    }

    private void CreateRooms()
    {
        var roomList = ProceduralGenerationAlgorithms.BinarySpacePartitioning(new BoundsInt((Vector3Int)startPos, new Vector3Int(dungeonWidth, dungeonHeight, 0)), minRoomWidth, minRoomHeight);

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

        player.transform.position = new Vector3(startRoom.x, startRoom.y, 0);
        goal.transform.position = new Vector3(goalRoom.x, goalRoom.y, 0);

        tilemapVisualizer.PaintFloorTiles(floor);
        BasicWallPlacer.CreateWalls(floor, tilemapVisualizer);
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

    private HashSet<Vector2Int> CreateSimpleRooms(List<BoundsInt> roomList)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach (var room in roomList)
        {
            for (int col = offset; col < room.size.x - offset; col++)
            {
                for (int row = offset; row < room.size.y - offset; row++)
                {
                    Vector2Int position = (Vector2Int)room.min + new Vector2Int(col, row);
                    floor.Add(position);
                }
            }
        }
        return floor;
    }
}
