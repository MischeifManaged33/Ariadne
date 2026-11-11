using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// This is the second part of DesignedDungeonGenerator, not a separate component.
// Follow SETUP.md to add the small hooks to the original file and AriadneThread.
public partial class DesignedDungeonGenerator
{
    [Header("Maze Shifting")]
    [SerializeField] private bool enableMazeShifting = false;
    [SerializeField] private AriadneThread ariadneThread;
    [SerializeField, Min(1)] private int corridorEntriesBeforeWarning = 6;
    [SerializeField, Min(0f)] private float shiftWarningSeconds = 2f;
    [SerializeField, Min(1)] private int shiftAttempts = 30;
    [Tooltip("Keep the Minotaur farthest after a shift, using the existing distance settings.")]
    [SerializeField] private bool keepBossFarthestOnShift = true;

    [Header("Shift Sound / Effects Events")]
    [SerializeField] private UnityEvent onShiftWarning = new UnityEvent();
    [SerializeField] private UnityEvent onDungeonShifted = new UnityEvent();
    [SerializeField] private UnityEvent onShiftFailed = new UnityEvent();

    public int CorridorEntriesSinceShift => shiftEntryCount;
    public bool ShiftPending => shiftWarningRoutine != null;

    private List<RoomDefinition> shiftRooms = new List<RoomDefinition>();
    private List<PlacedRoom> shiftLayout = new List<PlacedRoom>();
    private HashSet<Vector2Int> shiftFloor = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> shiftSpine = new HashSet<Vector2Int>();
    private DungeonPassageMap shiftPassages;
    private Coroutine shiftWarningRoutine;
    private int shiftEntryCount;
    private int currentShiftPassage = -1;
    private bool shiftBusy;
    private bool shiftHasPlayerPosition;
    private Vector3 shiftPreviousPlayerPosition;
    private readonly List<Vector3> shiftThreadPoints = new List<Vector3>();
    private readonly List<Vector2Int> shiftTraceCells = new List<Vector2Int>();

    // Called once, after a normal GenerateDungeon succeeds.
    private void RememberDungeonForShifting(
        List<PlacedRoom> layout, List<RoomDefinition> rooms,
        HashSet<Vector2Int> spine, HashSet<Vector2Int> floor)
    {
        CancelShiftWarning();
        shiftRooms = new List<RoomDefinition>(rooms);
        shiftLayout = new List<PlacedRoom>(layout);
        shiftFloor = new HashSet<Vector2Int>(floor);
        shiftSpine = new HashSet<Vector2Int>(spine);
        if (ariadneThread == null && player != null)
            ariadneThread = player.GetComponentInChildren<AriadneThread>(true);
        if (ariadneThread != null) ariadneThread.ResetSpool();
        RebuildShiftPassages();
    }

    private void RebuildShiftPassages()
    {
        shiftPassages = new DungeonPassageMap(
            shiftSpine, shiftFloor, Mathf.Max(0, corridorRadius) + Mathf.Max(0, smoothingPasses));
        shiftEntryCount = 0;
        currentShiftPassage = -1;
        shiftHasPlayerPosition = false;
    }

    private void Update()
    {
        if (!enableMazeShifting)
        {
            CancelShiftWarning();
            shiftHasPlayerPosition = false;
            return;
        }
        if (!Application.isPlaying || Time.deltaTime <= 0f || shiftBusy ||
            player == null || shiftPassages == null || !WalkableFloorReady) return;

        Vector3 position = player.position;
        if (!shiftHasPlayerPosition)
        {
            currentShiftPassage = PassageAt(ShiftCell(position), -1);
            shiftPreviousPlayerPosition = position;
            shiftHasPlayerPosition = true;
            return;
        }

        shiftTraceCells.Clear();
        TraceShiftLine(shiftPreviousPlayerPosition, position, shiftTraceCells);
        shiftPreviousPlayerPosition = position;
        foreach (Vector2Int cell in shiftTraceCells)
        {
            int next = PassageAt(cell, currentShiftPassage);
            if (next == currentShiftPassage) continue;
            currentShiftPassage = next;
            if (next < 0 || ShiftPending) continue;
            shiftEntryCount++;
            if (shiftEntryCount >= Mathf.Max(1, corridorEntriesBeforeWarning))
            {
                shiftWarningRoutine = StartCoroutine(WarnThenShift());
                break;
            }
        }
    }

    private int PassageAt(Vector2Int cell, int preferred)
    {
        foreach (PlacedRoom room in shiftLayout)
            if (room.bounds.Contains(cell)) return -1;
        if (shiftPassages.owners.TryGetValue(cell, out List<int> ids))
        {
            if (ids.Contains(preferred)) return preferred;
            return ids[0];
        }
        // A junction is a transition between passages, not a new entry itself.
        return shiftFloor.Contains(cell) ? preferred : -1;
    }

    private IEnumerator WarnThenShift()
    {
        // Yield once so StartCoroutine's handle is assigned even for zero delay.
        yield return null;
        onShiftWarning.Invoke();
        if (shiftWarningSeconds > 0f)
            yield return new WaitForSeconds(shiftWarningSeconds);
        shiftWarningRoutine = null;
        if (enableMazeShifting && isActiveAndEnabled) ShiftDungeon();
    }

    private void CancelShiftWarning()
    {
        if (shiftWarningRoutine == null) return;
        StopCoroutine(shiftWarningRoutine);
        shiftWarningRoutine = null;
    }

    private void OnDisable()
    {
        CancelShiftWarning();
        shiftHasPlayerPosition = false;
    }

    [ContextMenu("Shift Dungeon (Play Mode)")]
    public void ShiftDungeon()
    {
        if (!Application.isPlaying || shiftBusy) return;
        CancelShiftWarning();
        if (shiftRooms.Count == 0 || shiftPassages == null || player == null ||
            tilemapVisualizer == null || corridorWallPainter == null)
        {
            Debug.LogError("Generate a dungeon first and assign Player and the corridor references.", this);
            return;
        }
        if (shiftLayout.Count != shiftRooms.Count ||
            shiftRooms.Exists(room => room == null))
        {
            Debug.LogError("The generated room list changed. Generate the dungeon again before shifting.", this);
            return;
        }

        shiftBusy = true;
        bool changed = false;
        try
        {
            var pinnedRooms = new HashSet<int>();
            var lockedCells = new HashSet<Vector2Int>();
            CollectShiftProtection(pinnedRooms, lockedCells);
            var retainedFloor = new HashSet<Vector2Int>(shiftFloor);
            retainedFloor.IntersectWith(lockedCells);
            var retainedSpine = new HashSet<Vector2Int>(shiftSpine);
            retainedSpine.IntersectWith(lockedCells);

            // Keep the original, active Grid hierarchy. Inactive validation
            // clones can have a null Tilemap.layoutGrid. Evaluate synchronously
            // and restore each trial in finally, before accepting or rejecting it.
            var candidates = shiftRooms;

            var selected = new List<SelectedRoom>();
            foreach (PlacedRoom placement in shiftLayout) selected.Add(placement.selected);
            FindSpecialRoomIndices(selected, out int tutorialIndex, out int bossIndex);

            var area = new RectInt(dungeonOrigin, dungeonSize);
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            string reason = "No candidate fitted around the protected areas.";
            for (int attempt = 0; attempt < Mathf.Max(1, shiftAttempts); attempt++)
            {
                List<PlacedRoom> layout = CreateShiftLayout(pinnedRooms, lockedCells, area, random);
                if (layout == null) continue;
                var bounds = new List<RectInt>();
                var trialMovedRooms = new List<int>();
                HashSet<Vector2Int> spine = null;
                HashSet<Vector2Int> floor = null;
                try
                {
                    for (int i = 0; i < layout.Count; i++)
                    {
                        if (layout[i].destinationAnchor != shiftLayout[i].destinationAnchor)
                        {
                            // Record before PlaceAt so even a partial failure
                            // attempts to restore this room to its old anchor.
                            trialMovedRooms.Add(i);
                            candidates[i].PlaceAt(dungeonFloor, layout[i].destinationAnchor);
                        }
                        bounds.Add(layout[i].bounds);
                    }
                    spine = RoomCorridorBuilder.Build(
    candidates, bounds, dungeonFloor, area, random,
    Mathf.Max(mazeSpacing, corridorRadius * 2 + 2),
    mazeLoopChance,
    lockedCells, retainedFloor, retainedSpine,
    roomPadding: RoomWallClearance
);
                    AddRandomBranches(spine, bounds, area, random);
                    // Branches must respect the protected empty cells too.
                    spine.RemoveWhere(cell => lockedCells.Contains(cell) && !retainedFloor.Contains(cell));
                    floor = WidenCorridors(spine, bounds, area);
                    for (int pass = 0; pass < smoothingPasses; pass++)
                        floor = SmoothCorridorFloor(floor, bounds, area);
                    floor.ExceptWith(lockedCells);
                    floor.UnionWith(retainedFloor);

                    int[] distances = RoomRouteDistances.Calculate(
                        candidates, tutorialIndex, floor, dungeonFloor);
                    if (Array.Exists(distances, distance => distance < 0))
                    {
                        reason = "A room would be unreachable.";
                        continue;
                    }
                    if (keepBossFarthestOnShift &&
                        !BossIsFarthest(distances, tutorialIndex, bossIndex, out reason)) continue;
                    if (!ShiftFloorIsConnected(candidates, floor))
                    {
                        reason = "The new floor would leave a disconnected corridor or strand the player.";
                        continue;
                    }
                }
                catch (InvalidOperationException exception)
                {
                    reason = exception.Message;
                    continue;
                }
                finally
                {
                    // No yield, physics step, or activation occurs during a
                    // trial. Restore all moved rooms, including on rejection.
                    // Do not call SyncTransforms here; only the committed
                    // layout should be submitted to physics.
                    Exception restoreFailure = null;
                    foreach (int i in trialMovedRooms)
                    {
                        try
                        {
                            candidates[i].PlaceAt(dungeonFloor, shiftLayout[i].destinationAnchor);
                        }
                        catch (Exception exception)
                        {
                            if (restoreFailure == null) restoreFailure = exception;
                        }
                    }
                    if (restoreFailure != null)
                        throw new InvalidOperationException(
                            "A room could not be restored after shift validation. " +
                            "Check its Grid and anchor references.", restoreFailure);
                }

                // No-op candidates do not count as a successful shift.
                bool movedRoom = false;
                for (int i = 0; i < layout.Count; i++)
                    movedRoom |= layout[i].destinationAnchor != shiftLayout[i].destinationAnchor;
                if (!movedRoom && floor.SetEquals(shiftFloor))
                {
                    reason = "The protected areas left no different valid layout.";
                    continue;
                }

                CommitShift(layout, spine, floor, lockedCells);
                changed = true;
                Debug.Log($"Maze shifted. Kept {pinnedRooms.Count} protected/occupied rooms; " +
                    $"moved {lastShiftMovedRooms} rooms. Candidate {attempt + 1}.", this);
                break;
            }
            if (!changed) Debug.LogWarning("Maze shift skipped; current dungeon kept. " + reason, this);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            shiftBusy = false;
            shiftEntryCount = 0;
            shiftHasPlayerPosition = false;
        }
        if (changed) onDungeonShifted.Invoke();
        else onShiftFailed.Invoke();
    }

    private int lastShiftMovedRooms;

    private void CollectShiftProtection(HashSet<int> pinned, HashSet<Vector2Int> locked)
    {
        shiftThreadPoints.Clear();
        if (ariadneThread != null) ariadneThread.CopyTiedPath(shiftThreadPoints);
        shiftTraceCells.Clear();
        for (int i = 0; i < shiftThreadPoints.Count; i++)
        {
            if (i == 0) shiftTraceCells.Add(ShiftCell(shiftThreadPoints[i]));
            else TraceShiftLine(shiftThreadPoints[i - 1], shiftThreadPoints[i], shiftTraceCells);
        }
        // A room's knot marker may sit outside its painted tile bounds.
        if (ariadneThread != null)
            foreach (ThreadConnectionPoint point in
                UnityEngine.Object.FindObjectsByType<ThreadConnectionPoint>(FindObjectsSortMode.None))
            {
                if (point.Owner != ariadneThread) continue;
                RoomDefinition ownerRoom = point.GetComponentInParent<RoomDefinition>();
                int index = shiftRooms.IndexOf(ownerRoom);
                if (index >= 0) pinned.Add(index);
                shiftTraceCells.Add(ShiftCell(point.Position));
            }
        // Protect the current location even when no thread is tied.
        shiftTraceCells.Add(ShiftCell(player.position));
        var protectedPassages = new HashSet<int>();
        foreach (Vector2Int cell in shiftTraceCells)
        {
            for (int i = 0; i < shiftLayout.Count; i++)
                if (shiftLayout[i].bounds.Contains(cell)) pinned.Add(i);
            if (shiftPassages.owners.TryGetValue(cell, out List<int> ids))
                protectedPassages.UnionWith(ids);
            // Include both floor and empty cells beside the rope/player.
            AddShiftSquare(locked, cell, 2);
        }
        foreach (int id in protectedPassages)
            foreach (Vector2Int cell in shiftPassages.passages[id])
                AddShiftSquare(locked, cell, 2);
        foreach (int i in pinned)
        {
            RectInt bounds = shiftLayout[i].bounds;
            for (int x = bounds.xMin - 2; x < bounds.xMax + 2; x++)
                for (int y = bounds.yMin - 2; y < bounds.yMax + 2; y++)
                    locked.Add(new Vector2Int(x, y));
        }
    }

    private List<PlacedRoom> CreateShiftLayout(
        HashSet<int> pinned, HashSet<Vector2Int> locked,
        RectInt area, System.Random random)
    {
        var result = new List<PlacedRoom>(new PlacedRoom[shiftLayout.Count]);
        var placed = new List<PlacedRoom>();
        foreach (int index in pinned)
        {
            PlacedRoom placement = shiftLayout[index];
            if (!area.Contains(placement.bounds.min) ||
                !area.Contains(placement.bounds.max - Vector2Int.one)) return null;
            result[index] = placement;
            placed.Add(placement);
        }
        // The original list is sorted largest-first.
        for (int i = 0; i < shiftLayout.Count; i++)
        {
            if (pinned.Contains(i)) continue;
            PlacedRoom chosen = null;
            for (int attempt = 0; attempt < Mathf.Max(1, attemptsPerRoom); attempt++)
            {
                PlacedRoom candidate = FindPlacement(shiftLayout[i].selected, placed, area, random);
                if (candidate == null) break;
                bool overlapsLock = false;
                // This extra margin keeps changed room art outside preserved walls.
                RectInt bounds = candidate.bounds;
                for (int x = bounds.xMin - 1; x < bounds.xMax + 1 && !overlapsLock; x++)
                    for (int y = bounds.yMin - 1; y < bounds.yMax + 1; y++)
                        if (locked.Contains(new Vector2Int(x, y))) { overlapsLock = true; break; }
                if (!overlapsLock) { chosen = candidate; break; }
            }
            if (chosen == null) return null;
            result[i] = chosen;
            placed.Add(chosen);
        }
        return result;
    }

    private bool ShiftFloorIsConnected(List<RoomDefinition> rooms, HashSet<Vector2Int> corridors)
    {
        var walkable = new HashSet<Vector2Int>(corridors);
        foreach (RoomDefinition room in rooms)
            foreach (Vector3Int cell in room.FloorTilemap.cellBounds.allPositionsWithin)
            {
                if (!room.FloorTilemap.HasTile(cell) || room.IsBlockedCell(cell)) continue;
                Vector3Int mapped = room.ToDungeonCell(cell, dungeonFloor);
                walkable.Add(new Vector2Int(mapped.x, mapped.y));
            }
        Vector2Int start = ShiftCell(player.position);
        if (!walkable.Contains(start)) return false;
        var seen = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (walkable.Contains(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }
        // Reject isolated corridor islands, including preserved islands.
        foreach (Vector2Int cell in corridors)
            if (!seen.Contains(cell)) return false;
        return true;
    }

    private void CommitShift(
        List<PlacedRoom> layout, HashSet<Vector2Int> spine,
        HashSet<Vector2Int> floor, HashSet<Vector2Int> preservedCells)
    {
        lastShiftMovedRooms = 0;
        for (int i = 0; i < layout.Count; i++)
            if (layout[i].destinationAnchor != shiftLayout[i].destinationAnchor) lastShiftMovedRooms++;
        // Trial room positions have already been restored. Capture independent
        // spawned enemies inside rooms before moving the accepted live layout.
        if (corridorEnemySpawner != null)
            corridorEnemySpawner.CaptureRoomOccupants(shiftRooms);
        WalkableFloorReady = false;
        try
        {
            for (int i = 0; i < shiftRooms.Count; i++)
                if (layout[i].destinationAnchor != shiftLayout[i].destinationAnchor)
                    shiftRooms[i].PlaceAt(dungeonFloor, layout[i].destinationAnchor);
            // Do not call tilemapVisualizer.Clear(): it can clear other maps too.
            dungeonFloor.ClearAllTiles();
            tilemapVisualizer.PaintFloorTiles(floor);
            GenerateCorridorWalls(floor, shiftRooms);
            Physics2D.SyncTransforms();
        }
        catch
        {
            // Restore the live layout if applying the candidate fails.
            for (int i = 0; i < shiftRooms.Count; i++)
                shiftRooms[i].PlaceAt(dungeonFloor, shiftLayout[i].destinationAnchor);
            dungeonFloor.ClearAllTiles();
            tilemapVisualizer.PaintFloorTiles(shiftFloor);
            GenerateCorridorWalls(shiftFloor, shiftRooms);
            Physics2D.SyncTransforms();
            throw;
        }
        finally { RefreshWalkableFloors(); }
        shiftLayout = layout;
        shiftSpine = spine;
        shiftFloor = floor;
        RebuildShiftPassages();

        // Geometry is committed. Population updates must not roll it back after
        // any objects have already been removed; report errors independently.
        var populationRandom = new System.Random(Guid.NewGuid().GetHashCode());
        if (corridorDecorations != null)
        {
            try
            {
                corridorDecorations.UpdateAfterShift(
                    floor, dungeonFloor, shiftRooms, populationRandom, preservedCells);
            }
            catch (Exception exception)
            {
                Debug.LogError("Maze shifted, but updating corridor decorations failed.", this);
                Debug.LogException(exception, corridorDecorations);
            }
        }
        if (corridorEnemySpawner != null)
        {
            try
            {
                corridorEnemySpawner.UpdateAfterShift(
                    floor, dungeonFloor, shiftRooms, player, populationRandom, preservedCells);
            }
            catch (Exception exception)
            {
                Debug.LogError("Maze shifted, but updating corridor enemies failed.", this);
                Debug.LogException(exception, corridorEnemySpawner);
            }
        }
    }

    private Vector2Int ShiftCell(Vector3 world)
    {
        world.z = dungeonFloor.CellToWorld(Vector3Int.zero).z;
        Vector3Int cell = dungeonFloor.WorldToCell(world);
        return new Vector2Int(cell.x, cell.y);
    }

    // Visit every crossed cell, including when frame movement is several cells.
    // WorldToCell alone at rope vertices misses cells between distant vertices.
    private void TraceShiftLine(Vector3 a, Vector3 b, List<Vector2Int> cells)
    {
        Vector2 origin = dungeonFloor.CellToWorld(Vector3Int.zero);
        Vector2 x = (Vector2)dungeonFloor.CellToWorld(Vector3Int.right) - origin;
        Vector2 y = (Vector2)dungeonFloor.CellToWorld(Vector3Int.up) - origin;
        float det = x.x * y.y - x.y * y.x;
        if (Mathf.Abs(det) < 0.000001f)
            throw new InvalidOperationException("The dungeon Grid axes are degenerate.");
        Vector2 p = (Vector2)a - origin;
        Vector2 q = (Vector2)b - origin;
        floorCrossings.Clear();
        floorCrossings.Add(0f);
        floorCrossings.Add(1f);
        AddFloorCrossings((p.x * y.y - p.y * y.x) / det, (q.x * y.y - q.y * y.x) / det);
        AddFloorCrossings((x.x * p.y - x.y * p.x) / det, (x.x * q.y - x.y * q.x) / det);
        floorCrossings.Sort();
        cells.Add(ShiftCell(a));
        for (int i = 1; i < floorCrossings.Count; i++)
            cells.Add(ShiftCell(Vector3.Lerp(a, b, (floorCrossings[i - 1] + floorCrossings[i]) * 0.5f)));
        cells.Add(ShiftCell(b));
    }

    private static void AddShiftSquare(HashSet<Vector2Int> cells, Vector2Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                cells.Add(center + new Vector2Int(x, y));
    }
}
