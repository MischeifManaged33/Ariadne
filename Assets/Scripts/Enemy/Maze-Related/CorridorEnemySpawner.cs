using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CorridorEnemySpawner : MonoBehaviour
{
    [Header("Enemies")]
    [SerializeField]
    private List<GameObject> enemyPrefabs =
        new List<GameObject>();

    [SerializeField, Min(0)]
    private int enemyCount = 10;

    [Header("Spacing — World Units")]
    [SerializeField, Min(0f)]
    private float minimumDistanceFromPlayer = 4f;

    [SerializeField, Min(0f)]
    private float minimumDistanceBetweenEnemies = 2f;

    [Header("Placement")]
    [Tooltip(
        "Added to each corridor floor-cell center. " +
        "Keep X/Y at zero unless your prefab root needs adjustment."
    )]
    [SerializeField]
    private Vector3 spawnOffset = Vector3.zero;

    [SerializeField, HideInInspector]
    private GameObject spawnedEnemyRoot;

    public int SpawnedCount { get; private set; }

    public void Spawn(
        HashSet<Vector2Int> corridorCells,
        Tilemap corridorFloor,
        List<RoomDefinition> rooms,
        Transform player,
        System.Random random)
    {
        if (corridorCells == null ||
            corridorFloor == null ||
            random == null)
        {
            throw new ArgumentException(
                "Enemy spawning needs corridor cells, " +
                "a floor Tilemap and a random generator."
            );
        }

        List<GameObject> prefabs =
            new List<GameObject>();

        foreach (GameObject prefab in enemyPrefabs)
        {
            if (prefab != null &&
                !prefabs.Contains(prefab))
            {
                prefabs.Add(prefab);
            }
        }

        ClearSpawnedEnemies();

        if (enemyCount <= 0)
            return;

        if (prefabs.Count == 0)
        {
            Debug.LogWarning(
                "Assign at least one enemy prefab " +
                "in CorridorEnemySpawner.",
                this
            );
            return;
        }

        List<Vector2Int> candidates =
            new List<Vector2Int>(corridorCells);

        // Stable ordering before shuffling allows
        // fixed dungeon seeds to repeat the placement.
        candidates.Sort((a, b) =>
        {
            int comparison = a.x.CompareTo(b.x);

            return comparison != 0
                ? comparison
                : a.y.CompareTo(b.y);
        });

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);

            Vector2Int temporary = candidates[i];
            candidates[i] = candidates[other];
            candidates[other] = temporary;
        }

        spawnedEnemyRoot =
            new GameObject("Generated Corridor Enemies");

        // Set references before enabling enemy scripts.
        spawnedEnemyRoot.SetActive(false);

        PlayerController playerController = player != null
            ? player.GetComponentInChildren<PlayerController>(true)
            : null;

        List<Vector2> usedPositions =
            new List<Vector2>();

        foreach (Vector2Int candidate in candidates)
        {
            if (SpawnedCount >= enemyCount)
                break;

            Vector3Int cell = new Vector3Int(
                candidate.x,
                candidate.y,
                0
            );

            if (!corridorFloor.HasTile(cell))
                continue;

            Vector3 position =
                corridorFloor.GetCellCenterWorld(cell)
                + spawnOffset;

            // Exclude room floors.
            if (IsOnRoomFloor(position, rooms))
                continue;

            // Make sure the offset hasn't moved
            // the spawn position outside the floor.
            Vector3 floorProbe = new Vector3(
                position.x,
                position.y,
                corridorFloor.CellToWorld(Vector3Int.zero).z
            );

            if (!corridorFloor.HasTile(
                corridorFloor.WorldToCell(floorProbe)))
            {
                continue;
            }

            if (player != null &&
                Vector2.Distance(position, player.position)
                    < minimumDistanceFromPlayer)
            {
                continue;
            }

            bool tooClose = false;

            foreach (Vector2 used in usedPositions)
            {
                if (Vector2.Distance(position, used)
                    < minimumDistanceBetweenEnemies)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            GameObject prefab =
                prefabs[random.Next(prefabs.Count)];

            GameObject enemy = Instantiate(
                prefab,
                position,
                prefab.transform.rotation,
                spawnedEnemyRoot.transform
            );

            enemy.SetActive(true);

            // Your NPCController needs a player reference.
            foreach (NPCController npc in
                     enemy.GetComponentsInChildren<NPCController>(true))
            {
                npc.SetPlayer(playerController);
            }

            usedPositions.Add(position);
            SpawnedCount++;
        }

        spawnedEnemyRoot.SetActive(true);

        if (SpawnedCount < enemyCount)
        {
            Debug.LogWarning(
                $"Spawned {SpawnedCount}/{enemyCount} corridor enemies. " +
                "There were not enough positions satisfying " +
                "the floor and spacing checks.",
                this
            );
        }
    }

    private static bool IsOnRoomFloor(
        Vector3 position,
        List<RoomDefinition> rooms)
    {
        if (rooms == null)
            return false;

        foreach (RoomDefinition room in rooms)
        {
            if (room == null ||
                room.FloorTilemap == null)
            {
                continue;
            }

            Tilemap floor = room.FloorTilemap;

            Vector3 probe = position;
            probe.z = floor.CellToWorld(Vector3Int.zero).z;

            Vector3Int cell = floor.WorldToCell(probe);
            cell.z = 0;

            if (floor.HasTile(cell))
                return true;
        }

        return false;
    }

    public void ClearSpawnedEnemies()
    {
        SpawnedCount = 0;

        if (spawnedEnemyRoot == null)
            return;

        spawnedEnemyRoot.SetActive(false);

        if (Application.isPlaying)
            Destroy(spawnedEnemyRoot);
        else
            DestroyImmediate(spawnedEnemyRoot);

        spawnedEnemyRoot = null;
    }
}
