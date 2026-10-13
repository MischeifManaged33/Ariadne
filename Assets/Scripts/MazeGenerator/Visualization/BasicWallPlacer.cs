using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public static class BasicWallPlacer
{
    // Keeps the original generators working.
    public static void CreateWalls(
        HashSet<Vector2Int> floorPositions,
        TileMapVisualizer tilemapVisualizer)
    {
        CreateWalls(
            floorPositions,
            floorPositions,
            new HashSet<Vector2Int>(),
            tilemapVisualizer
        );
    }

    // Generates corridor walls while protecting authored rooms.
    public static void CreateWalls(
        HashSet<Vector2Int> corridorPositions,
        HashSet<Vector2Int> allFloorPositions,
        HashSet<Vector2Int> protectedCells,
        TileMapVisualizer tilemapVisualizer)
    {
        var basicWallPositions = FindWallsInDirections(
            corridorPositions,
            Direction2D.cardinalDirList
        );

        var cornerWallPositions = FindWallsInDirections(
            corridorPositions,
            Direction2D.diagonalDirList
        );

        // Walls cannot occupy floor or painted room cells.
        basicWallPositions.ExceptWith(allFloorPositions);
        basicWallPositions.ExceptWith(protectedCells);

        cornerWallPositions.ExceptWith(allFloorPositions);
        cornerWallPositions.ExceptWith(protectedCells);

        // Include room floors when choosing wall shapes,
        // especially where corridors meet doorways.
        CreateBasicWall(
            tilemapVisualizer,
            basicWallPositions,
            allFloorPositions
        );

        CreateCornerWalls(
            tilemapVisualizer,
            cornerWallPositions,
            allFloorPositions
        );
    }

    private static void CreateCornerWalls(TileMapVisualizer tilemapVisualizer, HashSet<Vector2Int> cornerWallPositions, HashSet<Vector2Int> floorPositions)
    {
        foreach (var position in cornerWallPositions)
        {
            string neighborsBinaryType = "";
            foreach(var direction in Direction2D.eightDirList)
            {
                var neighborPosition = position + direction;
                if(floorPositions.Contains(neighborPosition))
                {
                    neighborsBinaryType += "1";
                } else
                {
                    neighborsBinaryType += "0";
                }
            }
            tilemapVisualizer.PaintSingleCornerWall(position, neighborsBinaryType);
        }
    }

    private static void CreateBasicWall(TileMapVisualizer tilemapVisualizer, HashSet<Vector2Int> basicWallPositions, HashSet<Vector2Int> floorPositions)
    {
        foreach (var position in basicWallPositions)
        {
            string neighborsBinaryType = "";
            foreach (var direction in Direction2D.cardinalDirList)
            {
                var neighborPosition = position + direction;
                if (floorPositions.Contains(neighborPosition)) 
                {
                    neighborsBinaryType += "1";
                } else
                {
                    neighborsBinaryType += "0";
                }
            }
            tilemapVisualizer.PaintSingleBasicWall(position, neighborsBinaryType);
        }
    }

    private static HashSet<Vector2Int> FindWallsInDirections(HashSet<Vector2Int> floorPositions, List<Vector2Int> directionsList)
    {
        HashSet<Vector2Int> wallPositions = new HashSet<Vector2Int>();

        foreach (var position in floorPositions)
        {
            foreach (var direction in directionsList)
            {
                var neighborPosition = position + direction;

                if (floorPositions.Contains(neighborPosition) == false)
                {
                    wallPositions.Add(neighborPosition);
                }
            }
        }
        return wallPositions;
    }
}
