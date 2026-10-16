using System;
using System.Collections.Generic;
using UnityEngine;

public static class MazeCorridorCarver
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static HashSet<Vector2Int> Create(
        HashSet<Vector2Int> blocked,
        RectInt area,
        System.Random random,
        int spacing = 6,
        float loopChance = 0.08f)
    {
        if (blocked == null || random == null)
            throw new ArgumentNullException();

        spacing = Mathf.Max(2, spacing);
        loopChance = Mathf.Clamp01(loopChance);

        List<Vector2Int> nodes =
            new List<Vector2Int>();

        HashSet<Vector2Int> available =
            new HashSet<Vector2Int>();

        // Leave space around the boundary for corridor widening.
        int margin = spacing / 2;

        for (int x = area.xMin + margin;
             x < area.xMax - margin;
             x += spacing)
        {
            for (int y = area.yMin + margin;
                 y < area.yMax - margin;
                 y += spacing)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (blocked.Contains(cell))
                    continue;

                nodes.Add(cell);
                available.Add(cell);
            }
        }

        Dictionary<Vector2Int, List<Vector2Int>> graph =
            new Dictionary<Vector2Int, List<Vector2Int>>();

        foreach (Vector2Int node in nodes)
        {
            List<Vector2Int> neighbors =
                new List<Vector2Int>();

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next =
                    node + direction * spacing;

                if (available.Contains(next) &&
                    ClearSegment(node, next, blocked))
                {
                    neighbors.Add(next);
                }
            }

            graph.Add(node, neighbors);
        }

        // Rooms can divide the coarse grid into disconnected areas.
        // Use the largest area; the builder connects entrances afterward.
        List<Vector2Int> largest =
            new List<Vector2Int>();

        HashSet<Vector2Int> examined =
            new HashSet<Vector2Int>();

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        foreach (Vector2Int node in nodes)
        {
            if (!examined.Add(node))
                continue;

            List<Vector2Int> component =
                new List<Vector2Int>();

            queue.Enqueue(node);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                component.Add(current);

                foreach (Vector2Int next in graph[current])
                {
                    if (examined.Add(next))
                        queue.Enqueue(next);
                }
            }

            if (component.Count > largest.Count)
                largest = component;
        }

        HashSet<Vector2Int> floor =
            new HashSet<Vector2Int>();

        if (largest.Count < 2)
        {
            Debug.LogWarning(
                "Not enough space for a maze grid. " +
                "Falling back to room connections. " +
                "Reduce maze spacing or enlarge the dungeon."
            );

            return floor;
        }

        // Randomized depth-first backtracking produces
        // winding routes, branches and dead ends.
        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Stack<Vector2Int> stack =
            new Stack<Vector2Int>();

        HashSet<Vector2Int> options =
            new HashSet<Vector2Int>(largest);

        List<Vector2Int> choices =
            new List<Vector2Int>();

        HashSet<string> carvedEdges =
            new HashSet<string>();

        Vector2Int start =
            largest[random.Next(largest.Count)];

        visited.Add(start);
        stack.Push(start);
        floor.Add(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();

            choices.Clear();

            foreach (Vector2Int next in graph[current])
            {
                if (!visited.Contains(next))
                    choices.Add(next);
            }

            if (choices.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int chosen =
                choices[random.Next(choices.Count)];

            AddSegment(floor, current, chosen);
            carvedEdges.Add(EdgeKey(current, chosen));

            visited.Add(chosen);
            stack.Push(chosen);
        }

        // Open some unused connections to create loops.
        foreach (Vector2Int current in nodes)
        {
            if (!options.Contains(current))
                continue;

            foreach (Vector2Int next in graph[current])
            {
                // Process each connection only once.
                if (!ComesBefore(current, next))
                    continue;

                if (carvedEdges.Contains(EdgeKey(current, next)))
                    continue;

                if (random.NextDouble() < loopChance)
                    AddSegment(floor, current, next);
            }
        }

        return floor;
    }

    private static bool ClearSegment(
        Vector2Int a,
        Vector2Int b,
        HashSet<Vector2Int> blocked)
    {
        Vector2Int direction = new Vector2Int(
            Math.Sign(b.x - a.x),
            Math.Sign(b.y - a.y)
        );

        for (Vector2Int cell = a; ; cell += direction)
        {
            if (blocked.Contains(cell))
                return false;

            if (cell == b)
                return true;
        }
    }

    private static void AddSegment(
        HashSet<Vector2Int> floor,
        Vector2Int a,
        Vector2Int b)
    {
        Vector2Int direction = new Vector2Int(
            Math.Sign(b.x - a.x),
            Math.Sign(b.y - a.y)
        );

        for (Vector2Int cell = a; ; cell += direction)
        {
            floor.Add(cell);

            if (cell == b)
                return;
        }
    }

    private static bool ComesBefore(
        Vector2Int a,
        Vector2Int b)
    {
        return a.x < b.x ||
               (a.x == b.x && a.y < b.y);
    }

    private static string EdgeKey(
        Vector2Int a,
        Vector2Int b)
    {
        if (!ComesBefore(a, b))
        {
            Vector2Int temporary = a;
            a = b;
            b = temporary;
        }

        return $"{a.x},{a.y}:{b.x},{b.y}";
    }
}