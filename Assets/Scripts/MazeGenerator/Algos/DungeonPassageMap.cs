using System.Collections.Generic;
using UnityEngine;

// Passages are traced on the narrow routing skeleton, before widening/smoothing.
// Tracing the final three-cell-wide floor would identify almost every cell as a junction.
public sealed class DungeonPassageMap
{
    public readonly List<HashSet<Vector2Int>> passages = new List<HashSet<Vector2Int>>();
    public readonly Dictionary<Vector2Int, List<int>> owners =
        new Dictionary<Vector2Int, List<int>>();

    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public DungeonPassageMap(
        HashSet<Vector2Int> spine, HashSet<Vector2Int> floor, int spread)
    {
        var interior = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in spine)
        {
            int degree = 0;
            foreach (Vector2Int direction in directions)
                if (spine.Contains(cell + direction)) degree++;
            if (degree <= 2) interior.Add(cell);
        }

        var ordered = new List<Vector2Int>(interior);
        ordered.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var seen = new HashSet<Vector2Int>();
        foreach (Vector2Int start in ordered)
        {
            if (!seen.Add(start)) continue;
            var queue = new Queue<Vector2Int>();
            var path = new HashSet<Vector2Int> { start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (!spine.Contains(next)) continue;
                    path.Add(next); // Include the junction at each end.
                    if (interior.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }

            var footprint = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in path)
                for (int x = -spread; x <= spread; x++)
                    for (int y = -spread; y <= spread; y++)
                    {
                        Vector2Int candidate = cell + new Vector2Int(x, y);
                        if (floor.Contains(candidate)) footprint.Add(candidate);
                    }

            int id = passages.Count;
            passages.Add(footprint);
            foreach (Vector2Int cell in footprint)
            {
                if (!owners.TryGetValue(cell, out List<int> ids))
                {
                    ids = new List<int>();
                    owners.Add(cell, ids);
                }
                ids.Add(id);
            }
        }
    }
}
