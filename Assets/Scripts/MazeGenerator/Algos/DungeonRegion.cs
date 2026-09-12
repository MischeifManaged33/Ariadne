using System.Collections.Generic;
using UnityEngine;

public enum DungeonRegionType
{
    Room,
    Corridor
}

public class DungeonRegion
{
    public DungeonRegionType Type { get; private set; }

    public HashSet<Vector2Int> FloorPositions
    {
        get;
        private set;
    }

    public Vector2Int Center { get; private set; }

    public bool Visited { get; set; }

    public DungeonRegion(
        DungeonRegionType type,
        HashSet<Vector2Int> floorPositions,
        Vector2Int center)
    {
        Type = type;

        FloorPositions =
            new HashSet<Vector2Int>(floorPositions);

        Center = center;
        Visited = false;
    }

    public bool Contains(Vector2Int position)
    {
        return FloorPositions.Contains(position);
    }
}