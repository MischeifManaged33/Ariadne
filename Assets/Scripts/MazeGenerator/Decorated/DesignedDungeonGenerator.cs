using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DesignedDungeonGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomCatalogue roomCatalogue;
    [SerializeField] private Tilemap dungeonFloor;

    [Header("Dungeon Area — Tile Coordinates")]
    [SerializeField] private Vector2Int dungeonOrigin;
    [SerializeField]
    private Vector2Int dungeonSize =
        new Vector2Int(80, 80);

    [Header("Rooms")]
    [SerializeField, Min(1)] private int roomCount = 4;

    [Tooltip("Empty cells between room footprints.")]
    [SerializeField, Min(1)] private int roomSpacing = 4;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int seed = 12345;

    [SerializeField, Min(1)]
    private int layoutAttempts = 30;

    [SerializeField, Min(1)]
    private int attemptsPerRoom = 200;

    [SerializeField, HideInInspector]
    private GameObject generatedRoomRoot;

    private class SelectedRoom
    {
        public RoomDefinition prefab;
        public BoundsInt localBounds;
        public Vector2Int localAnchor;
    }

    private class PlacedRoom
    {
        public SelectedRoom selected;
        public RectInt bounds;
        public Vector2Int destinationAnchor;
    }

    private void Start()
    {
        if (generateOnStart)
            GenerateDungeon();
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        if (roomCatalogue == null || dungeonFloor == null)
        {
            Debug.LogError(
                "Assign Room Catalogue and Dungeon Floor.",
                this
            );
            return;
        }

        if (roomCount < 1 ||
            roomSpacing < 1 ||
            dungeonSize.x < 1 ||
            dungeonSize.y < 1 ||
            layoutAttempts < 1 ||
            attemptsPerRoom < 1)
        {
            Debug.LogError(
                "Room count, spacing, size and attempt limits " +
                "must be positive.",
                this
            );
            return;
        }

        try
        {
            System.Random random = new System.Random(seed);

            // Choose every design BEFORE planning placement.
            List<SelectedRoom> selectedRooms =
                SelectRooms(random);

            List<PlacedRoom> layout =
                CreateLayout(selectedRooms, random);

            if (layout == null)
            {
                Debug.LogError(
                    "Could not fit all selected rooms. " +
                    "Increase Dungeon Size or reduce Room Count. " +
                    "The previous rooms were kept.",
                    this
                );
                return;
            }

            SpawnRooms(layout);

            Debug.Log(
                $"Placed {layout.Count} rooms. Seed: {seed}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private List<SelectedRoom> SelectRooms(System.Random random)
    {
        List<SelectedRoom> selected =
            new List<SelectedRoom>();

        for (int i = 0; i < roomCount; i++)
        {
            RoomDefinition prefab =
                roomCatalogue.PickRoom(random);

            if (prefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    $"{prefab.name}: use a prefab from the " +
                    "Project window, not a scene object."
                );
            }

            if (prefab.FloorTilemap == null ||
                prefab.PlacementAnchor == null)
            {
                throw new InvalidOperationException(
                    $"{prefab.name}: assign its floor and anchor."
                );
            }

            BoundsInt bounds = prefab.GetLocalBounds();

            if (bounds.size.x < 1 || bounds.size.y < 1)
            {
                throw new InvalidOperationException(
                    $"{prefab.name}: the room has no valid bounds."
                );
            }

            Vector3Int anchor = prefab.GetAnchorCell();

            selected.Add(new SelectedRoom
            {
                prefab = prefab,
                localBounds = bounds,
                localAnchor = new Vector2Int(
                    anchor.x,
                    anchor.y
                )
            });
        }

        // Fit larger rooms first without changing which
        // designs were selected.
        selected.Sort((a, b) =>
        {
            long areaA =
                (long)a.localBounds.size.x *
                a.localBounds.size.y;

            long areaB =
                (long)b.localBounds.size.x *
                b.localBounds.size.y;

            return areaB.CompareTo(areaA);
        });

        return selected;
    }

    private List<PlacedRoom> CreateLayout(
        List<SelectedRoom> selectedRooms,
        System.Random random)
    {
        RectInt dungeonArea =
            new RectInt(dungeonOrigin, dungeonSize);

        for (int attempt = 0;
             attempt < layoutAttempts;
             attempt++)
        {
            List<PlacedRoom> placed =
                new List<PlacedRoom>();

            foreach (SelectedRoom room in selectedRooms)
            {
                PlacedRoom placement =
                    FindPlacement(room, placed, dungeonArea, random);

                if (placement == null)
                    break;

                placed.Add(placement);
            }

            if (placed.Count == selectedRooms.Count)
                return placed;
        }

        return null;
    }

    private PlacedRoom FindPlacement(
        SelectedRoom room,
        List<PlacedRoom> placed,
        RectInt dungeonArea,
        System.Random random)
    {
        int width = room.localBounds.size.x;
        int height = room.localBounds.size.y;

        int minimumX = dungeonArea.xMin + roomSpacing;
        int minimumY = dungeonArea.yMin + roomSpacing;

        int maximumX =
            dungeonArea.xMax - roomSpacing - width;

        int maximumY =
            dungeonArea.yMax - roomSpacing - height;

        if (maximumX < minimumX || maximumY < minimumY)
            return null;

        for (int attempt = 0;
             attempt < attemptsPerRoom;
             attempt++)
        {
            int x = random.Next(minimumX, maximumX + 1);
            int y = random.Next(minimumY, maximumY + 1);

            RectInt bounds =
                new RectInt(x, y, width, height);

            RectInt paddedBounds = new RectInt(
                x - roomSpacing,
                y - roomSpacing,
                width + roomSpacing * 2,
                height + roomSpacing * 2
            );

            bool overlaps = false;

            foreach (PlacedRoom other in placed)
            {
                if (paddedBounds.Overlaps(other.bounds))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            Vector2Int localMinimum = new Vector2Int(
                room.localBounds.xMin,
                room.localBounds.yMin
            );

            // The room's minimum cell goes at bounds.position.
            // Its anchor can be anywhere within the room.
            Vector2Int destinationAnchor =
                bounds.position +
                room.localAnchor -
                localMinimum;

            return new PlacedRoom
            {
                selected = room,
                bounds = bounds,
                destinationAnchor = destinationAnchor
            };
        }

        return null;
    }

    private void SpawnRooms(List<PlacedRoom> layout)
    {
        GameObject newRoot =
            new GameObject("Generated Artist Rooms");

        // Keep room behaviours inactive while positioning.
        newRoot.SetActive(false);

        foreach (PlacedRoom placement in layout)
        {
            RoomDefinition instance = Instantiate(
                placement.selected.prefab,
                newRoot.transform,
                false
            );

            instance.PlaceAt(
                dungeonFloor,
                placement.destinationAnchor
            );
        }

        if (generatedRoomRoot != null)
        {
            generatedRoomRoot.SetActive(false);

            if (Application.isPlaying)
                Destroy(generatedRoomRoot);
            else
                DestroyImmediate(generatedRoomRoot);
        }

        generatedRoomRoot = newRoot;
        generatedRoomRoot.SetActive(true);
    }
}