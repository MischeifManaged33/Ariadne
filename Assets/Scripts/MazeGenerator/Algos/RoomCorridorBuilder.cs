using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RoomCorridorBuilder
{
    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    private class EntranceStub
    {
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
    }

    // The optional arguments are used only by ShiftDungeon. Existing calls work.
    public static HashSet<Vector2Int> Build(
        List<RoomDefinition> rooms, List<RectInt> roomBounds,
        Tilemap dungeonFloor, RectInt dungeonArea, System.Random random,
        int mazeSpacing = 6, float mazeLoopChance = 0.08f,
        HashSet<Vector2Int> lockedCells = null,
        HashSet<Vector2Int> retainedFloor = null,
        HashSet<Vector2Int> retainedSpine = null,
        int roomPadding = 1)
    {
        if (rooms == null || roomBounds == null || rooms.Count == 0 ||
            rooms.Count != roomBounds.Count)
            throw new InvalidOperationException("Room instances and bounds do not match.");
        if (dungeonFloor == null || random == null)
            throw new ArgumentException("Assign the dungeon floor and random generator.");

        roomPadding = Mathf.Max(1, roomPadding);
        var blocked = new HashSet<Vector2Int>();
        foreach (RectInt bounds in roomBounds)
            AddRectangle(blocked, Padded(bounds, roomPadding));

        var entrances = new List<EntranceStub>();
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDefinition room = rooms[i];
            room.ValidateFloorCompatibility(dungeonFloor);
            RoomEntrance[] markers = room.Entrances;
            if (markers.Length == 0)
                throw new InvalidOperationException(room.name + ": add at least one entrance.");

            foreach (RoomEntrance marker in markers)
            {
                marker.ValidateSavedCell(room);
                room.ToDungeonCell(marker.GetRoomCell(), dungeonFloor);
                var entrance = new EntranceStub();
                foreach (Vector3Int local in marker.GetOutwardConnectionCells())
                {
                    Vector3Int mapped = room.ToDungeonCell(local, dungeonFloor);
                    var cell = new Vector2Int(mapped.x, mapped.y);
                    if (!dungeonArea.Contains(cell))
                        throw new InvalidOperationException("An entrance cannot reach the corridor network: its passage leaves the dungeon area.");
                    for (int j = 0; j < roomBounds.Count; j++)
                        if (j != i && Padded(roomBounds[j], roomPadding).Contains(cell))
                            throw new InvalidOperationException("An entrance cannot reach the corridor network: its passage is too close to another room.");

                    // A shift may use old floor, but must not open a preserved wall.
                    if (lockedCells != null && lockedCells.Contains(cell) &&
                        (retainedFloor == null || !retainedFloor.Contains(cell)))
                        throw new InvalidOperationException("An entrance would change protected geometry.");
                    entrance.cells.Add(cell);
                    blocked.Remove(cell);
                }
                if (entrance.cells.Count == 0)
                    throw new InvalidOperationException("An entrance has no outward passage cells.");

                // RoomEntrance ends beyond its original one-cell reservation.
                // Extend only this doorway's center lane across the wider band.
                Vector2Int outside = entrance.cells[entrance.cells.Count - 1];
                RectInt reservation = Padded(roomBounds[i], roomPadding);
                while (reservation.Contains(outside))
                {
                    outside += marker.Direction;
                    if (!dungeonArea.Contains(outside))
                        throw new InvalidOperationException(
                            "An entrance cannot reach the corridor network: room clearance reaches the dungeon edge.");
                    for (int j = 0; j < roomBounds.Count; j++)
                        if (j != i && Padded(roomBounds[j], roomPadding).Contains(outside))
                            throw new InvalidOperationException(
                                "An entrance cannot reach the corridor network: another room blocks its clearance band.");
                    if (lockedCells != null && lockedCells.Contains(outside) &&
                        (retainedFloor == null || !retainedFloor.Contains(outside)))
                        throw new InvalidOperationException(
                            "An entrance cannot reach the corridor network without altering protected geometry.");
                    entrance.cells.Add(outside);
                    blocked.Remove(outside);
                }
                entrances.Add(entrance);
            }
        }

        if (lockedCells != null)
            foreach (Vector2Int cell in lockedCells)
                if (retainedFloor == null || !retainedFloor.Contains(cell))
                    blocked.Add(cell);

        HashSet<Vector2Int> corridors = MazeCorridorCarver.Create(
            blocked, dungeonArea, random, mazeSpacing, mazeLoopChance);

        if (retainedSpine != null)
            foreach (Vector2Int cell in retainedSpine)
            {
                if (!dungeonArea.Contains(cell) || blocked.Contains(cell))
                    throw new InvalidOperationException("A preserved passage conflicts with a room.");
                corridors.Add(cell);
            }

        // Union all stubs first, then connect every disconnected component.
        // This also handles several separate protected passages and maze fallback.
        foreach (EntranceStub entrance in entrances)
            corridors.UnionWith(entrance.cells);
        ConnectComponents(corridors, blocked, dungeonArea, random);
        return corridors;
    }

    private static void ConnectComponents(
        HashSet<Vector2Int> floor, HashSet<Vector2Int> blocked,
        RectInt area, System.Random random)
    {
        if (floor.Count == 0) return;
        var ordered = new List<Vector2Int>(floor);
        ordered.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        HashSet<Vector2Int> connected = Flood(ordered[0], floor);
        foreach (Vector2Int start in ordered)
        {
            if (connected.Contains(start)) continue;
            List<Vector2Int> path = FindPath(start, connected, blocked, area, random);
            if (path == null)
                throw new InvalidOperationException(
                    "An entrance cannot reach the corridor network. Try more room spacing or another seed.");
            floor.UnionWith(path);
            connected = Flood(ordered[0], floor);
        }
    }

    private static HashSet<Vector2Int> Flood(Vector2Int start, HashSet<Vector2Int> floor)
    {
        var found = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (floor.Contains(next) && found.Add(next)) queue.Enqueue(next);
            }
        }
        return found;
    }

    private static List<Vector2Int> FindPath(
        Vector2Int start, HashSet<Vector2Int> target,
        HashSet<Vector2Int> blocked, RectInt area, System.Random random)
    {
        var queue = new Queue<Vector2Int>();
        var seen = new HashSet<Vector2Int> { start };
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (target.Contains(current))
            {
                var path = new List<Vector2Int> { current };
                while (current != start)
                {
                    current = previous[current];
                    path.Add(current);
                }
                return path;
            }
            var shuffled = (Vector2Int[])directions.Clone();
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Vector2Int swap = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = swap;
            }
            foreach (Vector2Int direction in shuffled)
            {
                Vector2Int next = current + direction;
                if (!area.Contains(next) || blocked.Contains(next) || !seen.Add(next)) continue;
                previous.Add(next, current);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private static RectInt Padded(RectInt bounds, int padding)
    {
        return new RectInt(bounds.xMin - padding, bounds.yMin - padding,
            bounds.width + padding * 2, bounds.height + padding * 2);
    }

    private static void AddRectangle(HashSet<Vector2Int> cells, RectInt bounds)
    {
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                cells.Add(new Vector2Int(x, y));
    }
}
