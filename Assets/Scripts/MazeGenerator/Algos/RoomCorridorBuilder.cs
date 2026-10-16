using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RoomCorridorBuilder
{
    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private class EntranceStub
    {
        public Vector2Int outside;

        public readonly List<Vector2Int> cells =
            new List<Vector2Int>();
    }

    public static HashSet<Vector2Int> Build(
        List<RoomDefinition> rooms,
        List<RectInt> roomBounds,
        Tilemap dungeonFloor,
        RectInt dungeonArea,
        System.Random random,
        int mazeSpacing = 6,
        float mazeLoopChance = 0.08f)
    {
        if (rooms == null ||
            roomBounds == null ||
            rooms.Count == 0 ||
            rooms.Count != roomBounds.Count)
        {
            throw new InvalidOperationException(
                "Room instances and room bounds do not match."
            );
        }

        if (dungeonFloor == null || random == null)
        {
            throw new ArgumentException(
                "Assign the dungeon floor and random generator."
            );
        }

        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>();

        foreach (RectInt bounds in roomBounds)
            AddRectangle(blocked, Padded(bounds));

        List<EntranceStub> entrances =
            new List<EntranceStub>();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDefinition room = rooms[i];

            room.ValidateFloorCompatibility(dungeonFloor);

            RoomEntrance[] markers = room.Entrances;

            if (markers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{room.name}: add at least one entrance."
                );
            }

            foreach (RoomEntrance marker in markers)
            {
                marker.ValidateSavedCell(room);

                // Verify the entrance using saved cell coordinates.
                room.ToDungeonCell(
                    marker.GetRoomCell(),
                    dungeonFloor
                );

                EntranceStub entrance = new EntranceStub();

                foreach (Vector3Int localCell in
                         marker.GetOutwardConnectionCells())
                {
                    Vector3Int mapped = room.ToDungeonCell(
                        localCell,
                        dungeonFloor
                    );

                    Vector2Int outsideCell = new Vector2Int(
                        mapped.x,
                        mapped.y
                    );

                    if (!dungeonArea.Contains(outsideCell))
                    {
                        throw new InvalidOperationException(
                            $"{room.name}/{marker.name}: " +
                            "entrance passage leaves the dungeon area."
                        );
                    }

                    for (int j = 0; j < roomBounds.Count; j++)
                    {
                        if (j != i &&
                            Padded(roomBounds[j]).Contains(outsideCell))
                        {
                            throw new InvalidOperationException(
                                $"{room.name}/{marker.name}: " +
                                "entrance passage is too close " +
                                "to another room."
                            );
                        }
                    }

                    entrance.cells.Add(outsideCell);
                    blocked.Remove(outsideCell);
                }

                if (entrance.cells.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"{room.name}/{marker.name}: " +
                        "no outward passage cells."
                    );
                }

                entrance.outside =
                    entrance.cells[entrance.cells.Count - 1];

                entrances.Add(entrance);
            }
        }

        // Create the branching maze before connecting rooms.
        HashSet<Vector2Int> corridors =
            MazeCorridorCarver.Create(
                blocked,
                dungeonArea,
                random,
                mazeSpacing,
                mazeLoopChance
            );

        // Connect every entrance to the same corridor network.
        for (int i = 0; i < entrances.Count; i++)
        {
            // Fallback when there was insufficient room for a maze.
            if (corridors.Count == 0)
            {
                corridors.UnionWith(entrances[i].cells);
                continue;
            }

            List<Vector2Int> path = FindPath(
                entrances[i].outside,
                corridors,
                blocked,
                dungeonArea,
                random
            );

            if (path == null)
            {
                throw new InvalidOperationException(
                    "An entrance cannot reach the corridor network. " +
                    "Try more room spacing or another seed."
                );
            }

            corridors.UnionWith(path);
            corridors.UnionWith(entrances[i].cells);
        }

        return corridors;
    }

    private static RectInt Padded(RectInt bounds)
    {
        return new RectInt(
            bounds.xMin - 1,
            bounds.yMin - 1,
            bounds.width + 2,
            bounds.height + 2
        );
    }

    private static List<Vector2Int> FindPath(
        Vector2Int start,
        HashSet<Vector2Int> connectedCorridors,
        HashSet<Vector2Int> blocked,
        RectInt dungeonArea,
        System.Random random)
    {
        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> previous =
            new Dictionary<Vector2Int, Vector2Int>();

        frontier.Enqueue(start);
        visited.Add(start);

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            if (connectedCorridors.Contains(current))
            {
                List<Vector2Int> path =
                    new List<Vector2Int>();

                Vector2Int position = current;
                path.Add(position);

                while (position != start)
                {
                    position = previous[position];
                    path.Add(position);
                }

                return path;
            }

            Vector2Int[] shuffled =
                (Vector2Int[])directions.Clone();

            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);

                Vector2Int temporary = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temporary;
            }

            foreach (Vector2Int direction in shuffled)
            {
                Vector2Int next = current + direction;

                if (!dungeonArea.Contains(next) ||
                    blocked.Contains(next) ||
                    !visited.Add(next))
                {
                    continue;
                }

                previous[next] = current;
                frontier.Enqueue(next);
            }
        }

        return null;
    }

    private static void AddRectangle(
        HashSet<Vector2Int> cells,
        RectInt bounds)
    {
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                cells.Add(new Vector2Int(x, y));
        }
    }
}