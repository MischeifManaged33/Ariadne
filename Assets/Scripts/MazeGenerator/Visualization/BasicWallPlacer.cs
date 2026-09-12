using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public static class BasicWallPlacer
{
    public static void CreateWalls(
    HashSet<Vector2Int> floorPositions,
    TileMapVisualizer tilemapVisualizer)
    {
        var basicWallPositions =
            FindWallsInDirections(
                floorPositions,
                Direction2D.cardinalDirList
            );

        var cornerWallPositions =
            FindWallsInDirections(
                floorPositions,
                Direction2D.diagonalDirList
            );

        Debug.Log(
            $"Wall placer received {floorPositions.Count} floors. " +
            $"Found {basicWallPositions.Count} basic walls and " +
            $"{cornerWallPositions.Count} corner walls."
        );

        CreateBasicWall(
            tilemapVisualizer,
            basicWallPositions,
            floorPositions
        );

        CreateCornerWalls(
            tilemapVisualizer,
            cornerWallPositions,
            floorPositions
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
