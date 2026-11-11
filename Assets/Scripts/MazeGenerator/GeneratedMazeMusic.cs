using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public class GeneratedMazeMusic : MonoBehaviour
{
    [Tooltip("Small delay prevents rapid track changes when standing on a doorway boundary.")]
    [SerializeField, Min(0f)] private float transitionDelay = 0.15f;

    private Transform player;
    private RoomDefinition minotaurRoom;
    private SoundManager appliedManager;
    private bool hasAppliedMusic;
    private bool appliedBossMusic;
    private bool pendingBossMusic;
    private float pendingSeconds;

    // Called only after a successful dungeon generation and player placement.
    public void Configure(Transform playerTransform, RoomDefinition generatedMinotaurRoom)
    {
        player = playerTransform;
        minotaurRoom = generatedMinotaurRoom;
        hasAppliedMusic = false;
        pendingSeconds = 0f;
        UpdateMusic(0f);
    }

    private void LateUpdate()
    {
        if (Time.deltaTime > 0f) UpdateMusic(Time.deltaTime);
    }

    private void OnEnable()
    {
        hasAppliedMusic = false;
    }

    private void UpdateMusic(float deltaTime)
    {
        if (player == null || minotaurRoom == null ||
            !minotaurRoom.gameObject.activeInHierarchy) return;
        Tilemap floor = minotaurRoom.FloorTilemap;
        SoundManager manager = SoundManager.Instance;
        if (floor == null || manager == null) return;

        Vector3 probe = player.position;
        probe.z = floor.CellToWorld(Vector3Int.zero).z;
        Vector3Int cell = floor.WorldToCell(probe);
        cell.z = 0;
        bool wantsBoss = floor.HasTile(cell);

        if (!hasAppliedMusic || appliedManager != manager)
        {
            ApplyMusic(manager, wantsBoss);
            return;
        }
        if (wantsBoss == appliedBossMusic)
        {
            pendingSeconds = 0f;
            pendingBossMusic = wantsBoss;
            return;
        }
        if (pendingBossMusic != wantsBoss)
        {
            pendingBossMusic = wantsBoss;
            pendingSeconds = 0f;
        }
        pendingSeconds += deltaTime;
        if (pendingSeconds >= transitionDelay) ApplyMusic(manager, wantsBoss);
    }

    private void ApplyMusic(SoundManager manager, bool boss)
    {
        if (boss) manager.PlayBossMusic();
        else manager.PlayMazeMusic();
        appliedManager = manager;
        appliedBossMusic = boss;
        pendingBossMusic = boss;
        pendingSeconds = 0f;
        hasAppliedMusic = true;
    }
}
