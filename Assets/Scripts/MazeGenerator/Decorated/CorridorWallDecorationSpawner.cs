using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CorridorWallDecorationSpawner : MonoBehaviour
{
    public enum WallSide
    {
        North,
        East,
        South,
        West
    }

    [Serializable]
    public class Decoration
    {
        public GameObject prefab;
        public WallSide wallSide = WallSide.North;

        public Vector3 offset =
            new Vector3(0f, 0.35f, 0f);
    }

    [Header("Prefabs and Placement")]
    [SerializeField]
    private List<Decoration> decorations =
        new List<Decoration>();

    [SerializeField, Min(0)]
    private int decorationCount = 20;

    [SerializeField, Min(0f)]
    private float minimumSpacing = 2f;

    [SerializeField, Range(0f, 1f)]
    private float wallPosition = 0.8f;

    [SerializeField, Min(0)]
    private int roomClearance = 1;

    [SerializeField]
    private bool requireStraightWall = true;

    [Header("Generated Visible Walls")]
    [Tooltip(
        "Assign visible corridor wall Tilemaps using the same " +
        "cell coordinates as the corridor floor. " +
        "Exclude invisible collision Tilemaps."
    )]
    [SerializeField]
    private Tilemap[] visibleWallTilemaps = new Tilemap[0];

    [SerializeField, HideInInspector]
    private GameObject generatedRoot;

    public int SpawnedCount { get; private set; }

    private struct Candidate
    {
        public Vector2Int wall;
        public Vector3 mount;
        public Decoration decoration;
    }

    public void Spawn(
        HashSet<Vector2Int> corridors,
        Tilemap floor,
        List<RoomDefinition> rooms,
        System.Random random)
    {
        if (corridors == null ||
            floor == null ||
            rooms == null ||
            random == null)
        {
            throw new ArgumentException(
                "Decoration spawning needs corridors, " +
                "floor, rooms and a random generator."
            );
        }

        ClearSpawned();

        if (decorationCount <= 0)
            return;

        // Reserve rooms and their surrounding clearance.
        List<RectInt> protectedRooms =
            new List<RectInt>();

        foreach (RoomDefinition room in rooms)
        {
            BoundsInt bounds = room.GetLocalBounds();

            Vector3Int minimum = room.ToDungeonCell(
                new Vector3Int(
                    bounds.xMin,
                    bounds.yMin,
                    0
                ),
                floor
            );

            protectedRooms.Add(new RectInt(
                minimum.x - roomClearance,
                minimum.y - roomClearance,
                bounds.size.x + roomClearance * 2,
                bounds.size.y + roomClearance * 2
            ));
        }

        // Stable ordering keeps fixed seeds reproducible.
        List<Vector2Int> cells =
            new List<Vector2Int>(corridors);

        cells.Sort((a, b) =>
        {
            return a.x != b.x
                ? a.x.CompareTo(b.x)
                : a.y.CompareTo(b.y);
        });

        List<Candidate> candidates =
            new List<Candidate>();

        foreach (Vector2Int cell in cells)
        {
            if (!floor.HasTile(Cell3(cell)))
                continue;

            foreach (Decoration decoration in decorations)
            {
                if (decoration == null ||
                    decoration.prefab == null)
                {
                    continue;
                }

                Vector2Int direction =
                    Direction(decoration.wallSide);

                Vector2Int wall = cell + direction;

                // A mount must be on a visible wall beside floor.
                if (corridors.Contains(wall) ||
                    !HasVisibleWall(wall))
                {
                    continue;
                }

                bool nearRoom = false;

                foreach (RectInt bounds in protectedRooms)
                {
                    if (!bounds.Contains(cell) &&
                        !bounds.Contains(wall))
                    {
                        continue;
                    }

                    nearRoom = true;
                    break;
                }

                if (nearRoom)
                    continue;

                if (requireStraightWall)
                {
                    Vector2Int along = new Vector2Int(
                        -direction.y,
                        direction.x
                    );

                    if (!corridors.Contains(cell + along) ||
                        !corridors.Contains(cell - along) ||
                        !HasVisibleWall(wall + along) ||
                        !HasVisibleWall(wall - along))
                    {
                        continue;
                    }
                }

                Vector3 floorCenter =
                    floor.GetCellCenterWorld(Cell3(cell));

                Vector3 wallCenter =
                    floor.GetCellCenterWorld(Cell3(wall));

                candidates.Add(new Candidate
                {
                    wall = wall,
                    mount = Vector3.Lerp(
                        floorCenter,
                        wallCenter,
                        wallPosition
                    ),
                    decoration = decoration
                });
            }
        }

        // Shuffle locations and matching prefab choices together.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            Candidate temporary = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temporary;
        }

        generatedRoot = new GameObject(
            "Generated Corridor Decorations"
        );

        generatedRoot.transform.SetParent(transform, true);
        generatedRoot.SetActive(false);

        List<Vector3> usedPositions =
            new List<Vector3>();

        HashSet<Vector2Int> usedWalls =
            new HashSet<Vector2Int>();

        foreach (Candidate candidate in candidates)
        {
            if (SpawnedCount >= decorationCount)
                break;

            if (usedWalls.Contains(candidate.wall))
                continue;

            bool tooClose = false;

            foreach (Vector3 position in usedPositions)
            {
                if (Vector2.Distance(
                        position,
                        candidate.mount) >= minimumSpacing)
                {
                    continue;
                }

                tooClose = true;
                break;
            }

            if (tooClose)
                continue;

            GameObject prefab =
                candidate.decoration.prefab;

            GameObject instance = Instantiate(
                prefab,
                candidate.mount + candidate.decoration.offset,
                prefab.transform.rotation,
                generatedRoot.transform
            );

            instance.SetActive(true);

            usedPositions.Add(candidate.mount);
            usedWalls.Add(candidate.wall);

            SpawnedCount++;
        }

        generatedRoot.SetActive(true);

        if (SpawnedCount < decorationCount)
        {
            Debug.LogWarning(
                $"Placed {SpawnedCount}/{decorationCount} " +
                "wall decorations. Check visible wall Tilemaps, " +
                "wall sides, room clearance and spacing.",
                this
            );
        }
    }

    private bool HasVisibleWall(Vector2Int cell)
    {
        foreach (Tilemap map in visibleWallTilemaps)
        {
            if (map == null)
                continue;

            Vector3Int tileCell = Cell3(cell);

            if (map.HasTile(tileCell) &&
                map.GetSprite(tileCell) != null &&
                map.GetColor(tileCell).a > 0f &&
                map.color.a > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3Int Cell3(Vector2Int cell)
    {
        return new Vector3Int(cell.x, cell.y, 0);
    }

    private static Vector2Int Direction(WallSide side)
    {
        switch (side)
        {
            case WallSide.North:
                return Vector2Int.up;

            case WallSide.East:
                return Vector2Int.right;

            case WallSide.South:
                return Vector2Int.down;

            default:
                return Vector2Int.left;
        }
    }

    public void ClearSpawned()
    {
        SpawnedCount = 0;

        if (generatedRoot == null)
            return;

        generatedRoot.SetActive(false);

        if (Application.isPlaying)
            Destroy(generatedRoot);
        else
            DestroyImmediate(generatedRoot);

        generatedRoot = null;
    }
}