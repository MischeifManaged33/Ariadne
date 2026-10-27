using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Measures cardinal floor-cell steps from the tutorial spawn
// to the first reachable doorway cell of each room.
public static class RoomRouteDistances
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static int[] Calculate(
        List<RoomDefinition> rooms,
        int tutorialIndex,
        HashSet<Vector2Int> corridors,
        Tilemap dungeonFloor)
    {
        if (rooms == null ||
            corridors == null ||
            dungeonFloor == null ||
            tutorialIndex < 0 ||
            tutorialIndex >= rooms.Count)
        {
            throw new ArgumentException(
                "Room distance calculation needs valid rooms, " +
                "corridors, floor and tutorial index."
            );
        }

        HashSet<Vector2Int> walkable =
            new HashSet<Vector2Int>(corridors);

        Dictionary<Vector2Int, int> owner =
            new Dictionary<Vector2Int, int>();

        Dictionary<Vector2Int, HashSet<Vector2Int>> doorways =
            new Dictionary<Vector2Int, HashSet<Vector2Int>>();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDefinition room = rooms[i];
            Tilemap floor = room.FloorTilemap;

            foreach (Vector3Int localCell in
                     floor.cellBounds.allPositionsWithin)
            {
                if (!floor.HasTile(localCell))
                    continue;

                Vector2Int cell = Map(
                    room,
                    localCell,
                    dungeonFloor
                );

                if (owner.TryGetValue(cell, out int previousOwner) &&
                    previousOwner != i)
                {
                    throw new InvalidOperationException(
                        $"{room.name}: its floor overlaps " +
                        $"another room at {cell}."
                    );
                }

                owner[cell] = i;

                if (room.IsBlockedCell(localCell))
                    walkable.Remove(cell);
                else
                    walkable.Add(cell);
            }

            int validDoorwayCount = 0;
            string doorwayProblem =
                "No entrance passage crosses the edge of the room floor.";

            // Only authored doorway lanes connect a room to corridors.
            // This prevents measuring a shortcut through a side wall.
            foreach (RoomEntrance entrance in room.Entrances)
            {
                Vector3Int direction = new Vector3Int(
                    entrance.Direction.x,
                    entrance.Direction.y,
                    0
                );

                foreach (Vector3Int outside in
                         entrance.GetOutwardConnectionCells())
                {
                    Vector3Int inside = outside - direction;

                    if (!floor.HasTile(inside) ||
                        floor.HasTile(outside))
                    {
                        continue;
                    }

                    if (room.IsBlockedCell(inside) ||
    room.IsBlockedCell(outside))
                    {
                        doorwayProblem =
                            $"{entrance.name}: doorway cells {inside} -> {outside} " +
                            "are marked blocked. Check Blocking Tilemaps.";

                        continue;
                    }

                    Vector2Int a = Map(
                        room,
                        inside,
                        dungeonFloor
                    );

                    Vector2Int b = Map(
                        room,
                        outside,
                        dungeonFloor
                    );

                    AddDoorway(doorways, a, b);
                    AddDoorway(doorways, b, a);
                    validDoorwayCount++;
                }
            }
            if (validDoorwayCount == 0)
            {
                throw new InvalidOperationException(
                    $"{room.name}: no usable doorway was found. " +
                    doorwayProblem +
                    " Check entrance position, outward direction, passage " +
                    "length, and saved entrance cells."
                );
            }

        }

        RoomDefinition tutorial = rooms[tutorialIndex];

        if (tutorial.PlayerSpawnPoint == null)
        {
            throw new InvalidOperationException(
                "The tutorial room needs its Player Spawn Point."
            );
        }

        Vector3Int spawnLocal =
            RoomDefinition.NearestFloorCell(
                tutorial.FloorTilemap,
                tutorial.PlayerSpawnPoint.position
            );

        Vector2Int start = Map(
            tutorial,
            spawnLocal,
            dungeonFloor
        );

        if (!tutorial.FloorTilemap.HasTile(spawnLocal) ||
            !walkable.Contains(start) ||
            !owner.TryGetValue(start, out int startOwner) ||
            startOwner != tutorialIndex)
        {
            throw new InvalidOperationException(
                $"{tutorial.name}: Player Spawn Point " +
                "must be on clear tutorial floor."
            );
        }

        return Measure(
            walkable,
            owner,
            doorways,
            start,
            rooms.Count
        );
    }

    public static int[] Measure(
        HashSet<Vector2Int> walkable,
        Dictionary<Vector2Int, int> owner,
        Dictionary<Vector2Int, HashSet<Vector2Int>> doorways,
        Vector2Int start,
        int roomCount)
    {
        int[] roomDistances = new int[roomCount];

        for (int i = 0; i < roomCount; i++)
            roomDistances[i] = -1;

        Dictionary<Vector2Int, int> distances =
            new Dictionary<Vector2Int, int>();

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        distances.Add(start, 0);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int distance = distances[current];

            int currentOwner =
                owner.TryGetValue(current, out int index)
                    ? index
                    : -1;

            if (currentOwner >= 0 &&
                roomDistances[currentOwner] < 0)
            {
                roomDistances[currentOwner] = distance;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;

                if (!walkable.Contains(next) ||
                    distances.ContainsKey(next))
                {
                    continue;
                }

                int nextOwner =
                    owner.TryGetValue(next, out int nextIndex)
                        ? nextIndex
                        : -1;

                if (currentOwner != nextOwner)
                {
                    if (!doorways.TryGetValue(
                            current,
                            out HashSet<Vector2Int> connections) ||
                        !connections.Contains(next))
                    {
                        continue;
                    }
                }

                distances.Add(next, distance + 1);
                queue.Enqueue(next);
            }
        }

        return roomDistances;
    }

    private static Vector2Int Map(
        RoomDefinition room,
        Vector3Int cell,
        Tilemap dungeonFloor)
    {
        Vector3Int mapped =
            room.ToDungeonCell(cell, dungeonFloor);

        return new Vector2Int(mapped.x, mapped.y);
    }

    private static void AddDoorway(
        Dictionary<Vector2Int, HashSet<Vector2Int>> doorways,
        Vector2Int from,
        Vector2Int to)
    {
        if (!doorways.TryGetValue(
            from,
            out HashSet<Vector2Int> neighbors))
        {
            neighbors = new HashSet<Vector2Int>();
            doorways.Add(from, neighbors);
        }

        neighbors.Add(to);
    }
}