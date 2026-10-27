using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CorridorEnemySpawner : MonoBehaviour
{
    [Serializable]
    public class EnemyEntry
    {
        public GameObject prefab;

        [Min(0f)]
        public float startingWeight = 1f;

        [Min(0f)]
        public float endingWeight = 1f;

        [Min(0.01f)]
        public float radius = 0.5f;
    }

    [Header("Difficulty Progression")]
    [SerializeField, Min(0.1f)]
    private float minutesToMaximumDifficulty = 30f;

    [Header("Enemies")]
    [SerializeField] private List<EnemyEntry> enemies = new();

    [SerializeField, Min(1)]
    private int maximumLivingEnemies = 10;

    [Header("Timing — Seconds")]
    [SerializeField, Min(0f)]
    private float startingGracePeriod = 15f;

    [SerializeField, Min(0.1f)]
    private float spawnInterval = 8f;

    [SerializeField, Min(0.1f)]
    private float retryInterval = 1f;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Extra distance outside the camera, in viewport units.")]
    [SerializeField, Min(0f)]
    private float screenBuffer = 0.08f;

    [Tooltip("How far beyond the buffer candidates may spawn.")]
    [SerializeField, Min(0.01f)]
    private float offscreenBand = 0.3f;

    [Header("Spacing — World Units")]
    [SerializeField, Min(0f)]
    private float minimumDistanceFromPlayer = 4f;

    [SerializeField, Min(0f)]
    private float minimumDistanceBetweenEnemies = 2f;

    [SerializeField, Min(0.1f)]
    private float maximumDistanceFromPlayer = 25f;

    [Header("Placement")]
    [SerializeField] private Vector3 spawnOffset;

    [Tooltip("Include wall and enemy physics layers. Exclude floor layers.")]
    [SerializeField] private LayerMask blockedLayers;

    [SerializeField, Min(1)]
    private int attemptsPerSpawn = 60;

    private readonly List<Vector3> positions = new();
    private readonly List<GameObject> livingEnemies = new();
    private readonly List<EnemyEntry> eligibleEnemies = new();
    private readonly List<float> eligibleWeights = new();

    private Tilemap floor;
    private Transform player;
    private PlayerController playerController;
    private System.Random random;
    [SerializeField, HideInInspector]
    private GameObject spawnedEnemyRoot;

    private float elapsedSeconds;
    private float nextSpawnTime;
    private bool initialized;
    private bool runStarted;

    public int SpawnedCount => livingEnemies.Count;
    public float MinutesPlayed => elapsedSeconds / 60f;

    public void Spawn(
        HashSet<Vector2Int> corridorCells,
        Tilemap corridorFloor,
        List<RoomDefinition> rooms,
        Transform player,
        System.Random random)
    {
        if (corridorCells == null ||
            corridorFloor == null ||
            player == null ||
            random == null)
        {
            throw new ArgumentException(
                "Enemy spawning needs corridor cells, floor, " +
                "player and a random generator."
            );
        }

        ClearSpawnedEnemies();

        this.floor = corridorFloor;
        this.player = player;

        // Keep future enemy choices separate from the generator's RNG.
        this.random = new System.Random(random.Next());

        playerController =
            player.GetComponentInChildren<PlayerController>(true);

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("Assign the gameplay camera.", this);
            return;
        }

        // Stable ordering before runtime position sampling.
        List<Vector2Int> cells = new(corridorCells);

        cells.Sort((a, b) =>
            a.x != b.x
                ? a.x.CompareTo(b.x)
                : a.y.CompareTo(b.y));

        foreach (Vector2Int candidate in cells)
        {
            Vector3Int cell =
                new(candidate.x, candidate.y, 0);

            if (!floor.HasTile(cell))
                continue;

            Vector3 position =
                floor.GetCellCenterWorld(cell) + spawnOffset;

            if (!IsOnCorridorFloor(position) ||
                IsOnRoomFloor(position, rooms))
            {
                continue;
            }

            positions.Add(position);
        }

        spawnedEnemyRoot =
            new GameObject("Generated Corridor Enemies");

        spawnedEnemyRoot.transform.SetParent(transform, true);

        // Inactive parent allows player references to be assigned
        // before enemy OnEnable/Start methods run.
        spawnedEnemyRoot.SetActive(false);

        initialized = true;

        if (!runStarted)
            BeginNewRun();
        else
            nextSpawnTime = elapsedSeconds + spawnInterval;

        if (positions.Count == 0)
        {
            Debug.LogWarning(
                "No valid corridor enemy positions were found.",
                this
            );
        }
    }

    // Call this when starting a genuinely new run.
    // Regenerating the maze alone does not reset difficulty.
    public void BeginNewRun()
    {
        elapsedSeconds = 0f;
        nextSpawnTime = startingGracePeriod;
        runStarted = true;
    }

    private void Update()
    {
        if (!initialized ||
            player == null ||
            mainCamera == null ||
            spawnedEnemyRoot == null)
        {
            return;
        }

        // Pausing with Time.timeScale = 0 also pauses difficulty.
        elapsedSeconds += Time.deltaTime;

        livingEnemies.RemoveAll(
            enemy => enemy == null || !enemy.activeInHierarchy
        );

        if (elapsedSeconds < nextSpawnTime)
            return;

        if (livingEnemies.Count >= maximumLivingEnemies)
        {
            nextSpawnTime = elapsedSeconds + retryInterval;
            return;
        }

        bool spawned = TrySpawnEnemy();

        nextSpawnTime = elapsedSeconds +
            (spawned ? spawnInterval : retryInterval);
    }

    private bool TrySpawnEnemy()
    {
        if (positions.Count == 0)
            return false;

        EnemyEntry entry = ChooseEnemy();

        if (entry == null)
            return false;

        for (int attempt = 0;
             attempt < attemptsPerSpawn;
             attempt++)
        {
            Vector3 position =
                positions[random.Next(positions.Count)];

            float distance =
                Vector2.Distance(position, player.position);

            if (distance < minimumDistanceFromPlayer ||
                distance > maximumDistanceFromPlayer)
            {
                continue;
            }

            if (!IsJustOffscreen(position, entry.radius))
                continue;

            if (!IsOnCorridorFloor(position))
                continue;

            if (Physics2D.OverlapCircle(
                position,
                entry.radius,
                blockedLayers) != null)
            {
                continue;
            }

            bool tooClose = false;

            foreach (GameObject other in livingEnemies)
            {
                if (Vector2.Distance(
                    position,
                    other.transform.position) <
                    minimumDistanceBetweenEnemies)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            // Temporarily disable the parent during initialization.
            // Existing enemies are re-enabled within this method.
            // Instead, use a separate inactive staging parent so
            // existing enemies are never interrupted.
            GameObject staging = new GameObject("Enemy Spawn Staging");
            staging.SetActive(false);

            GameObject enemy = Instantiate(
                entry.prefab,
                position,
                entry.prefab.transform.rotation,
                staging.transform
            );

            foreach (NPCController npc in
                enemy.GetComponentsInChildren<NPCController>(true))
            {
                npc.SetPlayer(playerController);
            }

            enemy.SetActive(true);

            // The main container stays active once spawning begins.
            spawnedEnemyRoot.SetActive(true);
            enemy.transform.SetParent(spawnedEnemyRoot.transform, true);

            Destroy(staging);

            livingEnemies.Add(enemy);
            return true;
        }

        return false;
    }

    private EnemyEntry ChooseEnemy()
    {
        eligibleEnemies.Clear();
        eligibleWeights.Clear();

        float progress = Mathf.Clamp01(
            MinutesPlayed /
            Mathf.Max(0.1f, minutesToMaximumDifficulty)
        );

        float totalWeight = 0f;

        foreach (EnemyEntry entry in enemies)
        {
            if (entry == null || entry.prefab == null)
                continue;

            float weight = Mathf.Max(
                0f,
                Mathf.Lerp(
                    entry.startingWeight,
                    entry.endingWeight,
                    progress
                )
            );

            if (weight <= 0f)
                continue;

            eligibleEnemies.Add(entry);
            eligibleWeights.Add(weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return null;

        double roll = random.NextDouble() * totalWeight;

        for (int i = 0; i < eligibleEnemies.Count; i++)
        {
            roll -= eligibleWeights[i];

            if (roll < 0d)
                return eligibleEnemies[i];
        }

        return eligibleEnemies[eligibleEnemies.Count - 1];
    }

    private bool IsJustOffscreen(Vector3 position, float radius)
    {
        Vector3 center =
            mainCamera.WorldToViewportPoint(position);

        if (center.z <= 0f)
            return false;

        float outer = screenBuffer + offscreenBand;

        // Reject positions much farther away than the camera edges.
        if (center.x < -outer || center.x > 1f + outer ||
            center.y < -outer || center.y > 1f + outer)
        {
            return false;
        }

        // Project a bounding box so the enemy's sprite/body also
        // starts outside the buffered view, not just its center.
        Vector2 minimum =
            new(float.PositiveInfinity, float.PositiveInfinity);

        Vector2 maximum =
            new(float.NegativeInfinity, float.NegativeInfinity);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 corner = position +
                    new Vector3(x * radius, y * radius, 0f);

                Vector3 viewport =
                    mainCamera.WorldToViewportPoint(corner);

                if (viewport.z <= 0f)
                    return false;

                minimum = Vector2.Min(minimum, viewport);
                maximum = Vector2.Max(maximum, viewport);
            }
        }

        return maximum.x < -screenBuffer ||
               minimum.x > 1f + screenBuffer ||
               maximum.y < -screenBuffer ||
               minimum.y > 1f + screenBuffer;
    }

    private bool IsOnCorridorFloor(Vector3 position)
    {
        Vector3 probe = position;
        probe.z = floor.CellToWorld(Vector3Int.zero).z;

        Vector3Int cell = floor.WorldToCell(probe);
        cell.z = 0;

        return floor.HasTile(cell);
    }

    private static bool IsOnRoomFloor(
        Vector3 position,
        List<RoomDefinition> rooms)
    {
        if (rooms == null)
            return false;

        foreach (RoomDefinition room in rooms)
        {
            if (room == null || room.FloorTilemap == null)
                continue;

            Tilemap roomFloor = room.FloorTilemap;

            Vector3 probe = position;
            probe.z = roomFloor.CellToWorld(Vector3Int.zero).z;

            Vector3Int cell = roomFloor.WorldToCell(probe);
            cell.z = 0;

            if (roomFloor.HasTile(cell))
                return true;
        }

        return false;
    }

    // Call from enemy death if the corpse stays active.
    public void NotifyEnemyDied(GameObject enemy)
    {
        livingEnemies.Remove(enemy);
    }

    public void ClearSpawnedEnemies()
    {
        initialized = false;
        positions.Clear();
        livingEnemies.Clear();

        // Include leftover containers whose references were lost.
        HashSet<GameObject> roots = new();

        if (spawnedEnemyRoot != null)
            roots.Add(spawnedEnemyRoot);

        foreach (Transform candidate in
            UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene &&
                candidate.name == "Generated Corridor Enemies")
            {
                roots.Add(candidate.gameObject);
            }
        }

        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            root.SetActive(false);

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        spawnedEnemyRoot = null;
    }
}