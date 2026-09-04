//using UnityEngine;
//using System;
//using System.Collections;
//using System.Collections.Generic;

//public class RoomFirstDungeon : SimpleRandomWalkDungeonGenerator
//{
//    [SerializeField]
//    private int minRoomWidth = 4, minRoomHeight = 4;
//    [SerializeField]
//    private int dungeonWidth = 20, dungeonHeight = 20;
//    [SerializeField]
//    [Range(0,10)]
//    private int offset = 1;
//    [SerializeField]
//    private bool randomWalkRooms = false;

//    protected override void RunProceduralGeneration()
//    {
//        CreateRooms();
//    }

//    private void CreateRooms()
//    {
//        var roomList = ProceduralGenerationAlgorithms.BinarySpacePartitioning(new BoundsInt((Vector3Int)startPosition, new Vector3Int(dungeonWidth, dungeonHeight, 0)), minRoomWidth, minRoomHeight);

//        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
//        floor = CreateSimpleRooms(roomsList);

//        tilemapVisualizer.PaintFloorTiles(floor);

//    }
//}
