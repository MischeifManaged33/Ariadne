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
        System.Random random)
    {
        if (rooms.Count == 0 ||
            rooms.Count != roomBounds.Count)
        {
            throw new InvalidOperationException(
                "Room instances and room bounds do not match."
            );
        }

        HashSet<Vector2Int> reserved =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>();

        // Protect each room and one surrounding cell.
        // That surrounding space will accommodate walls.
        foreach (RectInt bounds in roomBounds)
        {
            AddRectangle(reserved, bounds);

            RectInt paddedBounds = new RectInt(
                bounds.xMin - 1,
                bounds.yMin - 1,
                bounds.width + 2,
                bounds.height + 2
            );

            AddRectangle(blocked, paddedBounds);
        }

        List<EntranceStub> entrances =
            new List<EntranceStub>();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDefinition room = rooms[i];
            RoomEntrance[] markers = room.Entrances;

            if (markers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{room.name}: add at least one entrance."
                );
            }

            foreach (RoomEntrance marker in markers)
            {
                Vector3Int cell3 =
                    dungeonFloor.WorldToCell(
                        marker.WorldPosition
                    );

                Vector2Int cell =
                    new Vector2Int(cell3.x, cell3.y);

                Vector2Int direction = marker.Direction;

                Vector3 center =
                    dungeonFloor.GetCellCenterWorld(cell3);

                if ((center - marker.WorldPosition)
    .sqrMagnitude > 0.000001f)
                {
                    Vector3Int localCell =
                        room.FloorTilemap.WorldToCell(marker.WorldPosition);

                    Vector3 roomCenter =
                        room.FloorTilemap.GetCellCenterWorld(localCell);

                    Vector3 anchorCenter =
                        room.FloorTilemap.GetCellCenterWorld(
                            room.GetAnchorCell()
                        );

                    throw new InvalidOperationException(
                        $"{room.name}/{marker.name}: alignment mismatch.\n" +
                        $"Entrance offset from room cell: " +
                        $"{(marker.WorldPosition - roomCenter).ToString("F6")}\n" +
                        $"Anchor offset from room cell: " +
                        $"{(room.PlacementAnchor.position - anchorCenter).ToString("F6")}\n" +
                        $"Entrance offset from dungeon cell: " +
                        $"{(marker.WorldPosition - center).ToString("F6")}"
                    );
                }

                Vector3Int roomCell =
                    room.FloorTilemap.WorldToCell(
                        marker.WorldPosition
                    );

                Vector3Int inwardCell =
                    roomCell - new Vector3Int(
                        direction.x,
                        direction.y,
                        0
                    );

                if (!room.FloorTilemap.HasTile(roomCell) ||
                    !room.FloorTilemap.HasTile(inwardCell))
                {
                    throw new InvalidOperationException(
                        $"{room.name}/{marker.name}: " +
                        "the doorway cell and the cell inward " +
                        "must both contain floor."
                    );
                }

                EntranceStub entrance = new EntranceStub();

                foreach (Vector3Int localCell in
                         marker.GetOutwardConnectionCells())
                {
                    Vector3 worldPosition =
                        room.FloorTilemap.GetCellCenterWorld(localCell);

                    Vector3Int dungeonCell =
                        dungeonFloor.WorldToCell(worldPosition);

                    Vector2Int outsideCell =
                        new Vector2Int(dungeonCell.x, dungeonCell.y);

                    if (!dungeonArea.Contains(outsideCell))
                    {
                        throw new InvalidOperationException(
                            $"{room.name}/{marker.name}: " +
                            "the entrance passage leaves the dungeon area."
                        );
                    }

                    // Allow the passage through this room's empty recess,
                    // while protecting every other room and its wall clearance.
                    for (int j = 0; j < roomBounds.Count; j++)
                    {
                        if (j == i)
                            continue;

                        RectInt other = roomBounds[j];

                        RectInt protectedArea = new RectInt(
                            other.xMin - 1,
                            other.yMin - 1,
                            other.width + 2,
                            other.height + 2
                        );

                        if (protectedArea.Contains(outsideCell))
                        {
                            throw new InvalidOperationException(
                                $"{room.name}/{marker.name}: " +
                                "the entrance passage is too close to another room."
                            );
                        }
                    }

                    entrance.cells.Add(outsideCell);
                    blocked.Remove(outsideCell);
                }

                entrance.outside =
                    entrance.cells[entrance.cells.Count - 1];

                entrances.Add(entrance);
            }
        }

        HashSet<Vector2Int> corridors =
            new HashSet<Vector2Int>();

        corridors.UnionWith(entrances[0].cells);

        // Join every entrance to the connected corridor network.
        for (int i = 1; i < entrances.Count; i++)
        {
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

            // Randomize equally short route choices.
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
            {
                cells.Add(new Vector2Int(x, y));
            }
        }
    }
}