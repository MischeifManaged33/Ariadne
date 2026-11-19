using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Add beside the other DesignedDungeonGenerator partial files. No existing
// generator, shifting or decoration-spawner file needs to be replaced.
public partial class DesignedDungeonGenerator
{
    private MazeTorchGuidance torchGuidance;
    private HashSet<Vector2Int> torchGuidanceFloor;
    private RoomDefinition torchGuidanceBoss;
    private Vector3 torchGuidanceBossPosition;
    private BoundsInt torchGuidanceBossBounds;
    private Tilemap torchGuidanceFloorMap;

    private void LateUpdate()
    {
        if (!Application.isPlaying || !WalkableFloorReady || shiftBusy ||
            shiftLayout == null || shiftRooms == null || shiftRooms.Count == 0 || shiftLayout.Count != shiftRooms.Count) return;
        if (torchGuidance == null)
        {
            torchGuidance = GetComponent<MazeTorchGuidance>();
            if (torchGuidance == null) torchGuidance = gameObject.AddComponent<MazeTorchGuidance>();
        }
        if (!torchGuidance.isActiveAndEnabled) return;
        // The accepted floor set is replaced after normal generation or commit.
        // Trials are restored before yielding, and shiftBusy skips their frames.
        if (ReferenceEquals(torchGuidanceFloor, shiftFloor) && torchGuidanceFloorMap == dungeonFloor &&
            torchGuidance.IsReady && torchGuidanceBoss != null &&
            torchGuidanceBoss.FloorTilemap != null &&
            torchGuidanceBossPosition == torchGuidanceBoss.transform.position &&
            torchGuidanceBossBounds == torchGuidanceBoss.FloorTilemap.cellBounds) return;

        RoomDefinition boss = null;
        for (int i = 0; i < shiftLayout.Count; i++)
            if (shiftLayout[i].selected.prefab == minotaurRoomPrefab) { boss = shiftRooms[i]; break; }
        torchGuidance.Configure(dungeonFloor, boss);
        torchGuidanceFloor = shiftFloor;
        torchGuidanceFloorMap = dungeonFloor;
        torchGuidanceBoss = boss;
        if (boss != null && boss.FloorTilemap != null)
        {
            torchGuidanceBossPosition = boss.transform.position;
            torchGuidanceBossBounds = boss.FloorTilemap.cellBounds;
        }
    }
}
