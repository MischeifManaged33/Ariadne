using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using System;

public class TileMapVisualizer : MonoBehaviour
{
    [SerializeField]
    private Tilemap floorTilemap, wallTilemap;
    [SerializeField]
    private TileBase floorTile, wallTop, wallSideRight, wallSideLeft, wallBottom, wallFull, wallInnerCornerDownLeft, wallInnerCornerDownRight, wallDiagonalCornerDownRight, wallDiagonalCornerDownLeft, wallDiagonalCornerUpRight, wallDiagonalCornerUpLeft;
    [SerializeField]
    private TileBase isometricWallTile;
    private int paintedWallCount;
    [SerializeField]
    private Tilemap[] upperWallTilemaps;
    [SerializeField]
    private float wallLayerSpacing = 0.5f;

    public Tilemap FloorTilemap => floorTilemap;

    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    private void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
    {
        foreach (var position in positions)
        {
            PaintSingleTile(tilemap, tile, position);
        }
    }

    public void ResetWallPaintCount()
    {
        paintedWallCount = 0;
    }

    private void PositionUpperWallLayers()
    {
        if (upperWallTilemaps == null)
            return;

        Vector3 basePosition = wallTilemap.transform.localPosition;

        for (int i = 0; i < upperWallTilemaps.Length; i++)
        {
            Tilemap upperLayer = upperWallTilemaps[i];

            if (upperLayer == null)
                continue;

            Vector3 layerPosition = basePosition;

            layerPosition.y += wallLayerSpacing * (i + 1);

            layerPosition.z = upperLayer.transform.localPosition.z;

            upperLayer.transform.localPosition = layerPosition;
        }
    }

    public int GetPaintedWallCount()
    {
        return paintedWallCount;
    }

    public void RefreshWallTiles()
    {
        wallTilemap.RefreshAllTiles();
    }

    private void PaintSingleTile(
    Tilemap tilemap,
    TileBase tile,
    Vector2Int position)
    {
        Vector3Int cell = new Vector3Int(
            position.x,
            position.y,
            0
        );

        tilemap.SetTile(cell, tile);
    }

    public void PaintIsometricWalls(HashSet<Vector2Int> floorPositions)
    {
        PositionUpperWallLayers();
        HashSet<Vector2Int> wallPositions = new HashSet<Vector2Int>();

        foreach (Vector2Int floorPosition in floorPositions)
        {
            foreach (Vector2Int direction in Direction2D.eightDirList)
            {
                Vector2Int neighborPosition =
                    floorPosition + direction;

                // Only put a wall where there is no floor.
                if (!floorPositions.Contains(neighborPosition))
                {
                    wallPositions.Add(neighborPosition);
                }
            }
        }

        foreach (Vector2Int wallPosition in wallPositions)
        {
            // Ground-level wall with collision.
            PaintSingleTile(
                wallTilemap,
                isometricWallTile,
                wallPosition
            );

            // Additional visual wall layers.
            foreach (Tilemap upperWallTilemap in upperWallTilemaps)
            {
                if (upperWallTilemap == null)
                    continue;

                PaintSingleTile(
                    upperWallTilemap,
                    isometricWallTile,
                    wallPosition
                );
            }
        }
    }


    internal void PaintSingleBasicWall(Vector2Int position, string binaryType)
    {
        int typeAsInt = Convert.ToInt32(binaryType, 2);
        TileBase tile = null;
        if(WallTypesHelper.wallTop.Contains(typeAsInt))
        {
            tile = wallTop;
        }else if (WallTypesHelper.wallSideRight.Contains(typeAsInt))
        {
            tile = wallSideRight;
        } else if (WallTypesHelper.wallSideLeft.Contains(typeAsInt))
        {
            tile = wallSideLeft;
        } else if (WallTypesHelper.wallBottom.Contains(typeAsInt))
        {
            tile = wallBottom;
        } else if (WallTypesHelper.wallFull.Contains(typeAsInt))
        {
            tile = wallFull;
        }

        if (tile == null)
        {
            Debug.LogWarning(
                $"No basic wall tile matched binary type {binaryType}"
            );
        }

        if (tile != null)
        {
            PaintSingleTile(wallTilemap, tile, position);
            paintedWallCount++;
        }
        else
        {
            Debug.LogWarning(
                $"No basic wall matched {binaryType}"
            );
        }
    }

    internal void PaintSingleCornerWall(Vector2Int position, string binaryType)
    {
        int typeAsInt = Convert.ToInt32(binaryType, 2);
        TileBase tile = null;

        if (WallTypesHelper.wallInnerCornerDownLeft.Contains(typeAsInt))
        {
            tile = wallInnerCornerDownLeft;
        } else if (WallTypesHelper.wallInnerCornerDownRight.Contains(typeAsInt))
        {
            tile = wallInnerCornerDownRight;
        } else if (WallTypesHelper.wallDiagonalCornerDownLeft.Contains(typeAsInt))
        {
            tile = wallDiagonalCornerDownLeft;
        }
        else if (WallTypesHelper.wallDiagonalCornerDownRight.Contains(typeAsInt))
        {
            tile = wallDiagonalCornerDownRight;
        }
        else if (WallTypesHelper.wallDiagonalCornerUpLeft.Contains(typeAsInt))
        {
            tile = wallDiagonalCornerUpLeft;
        }
        else if (WallTypesHelper.wallDiagonalCornerUpRight.Contains(typeAsInt))
        {
            tile = wallDiagonalCornerUpRight;
        }
        else if (WallTypesHelper.wallFullEightDirections.Contains(typeAsInt))
        {
            tile = wallFull;
        }
        else if (WallTypesHelper.wallBottomEightDirections.Contains(typeAsInt))
        {
            tile = wallBottom;
        }

        if (tile != null)
        {
            PaintSingleTile(wallTilemap, tile, position);
            paintedWallCount++;
        }
    }

    private Vector3Int PositionToCell(Vector2Int position)
    {
        return floorTilemap.WorldToCell(
            new Vector3(position.x, position.y, 0)
        );
    }

    public Vector3 GetFloorWorldPosition(Vector2Int position)
    {
        Vector3Int cellPosition =
            new Vector3Int(position.x, position.y, 0);

        return floorTilemap.GetCellCenterWorld(cellPosition);
    }

    public Vector2Int GetFloorCellPosition(
    Vector3 worldPosition)
    {
        Vector3Int cellPosition =
            floorTilemap.WorldToCell(worldPosition);

        return new Vector2Int(
            cellPosition.x,
            cellPosition.y
        );
    }

    public int GetWallTileCount()
    {
        return wallTilemap.GetUsedTilesCount();
    }

    public void Clear()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();

        foreach (Tilemap upperWallTilemap in upperWallTilemaps)
        {
            if (upperWallTilemap != null)
            {
                upperWallTilemap.ClearAllTiles();
            }
        }
    }

    [ContextMenu("Diagnose Corridor Walls")]
    private void DiagnoseCorridorWalls()
    {
        if (floorTilemap == null || wallTilemap == null)
        {
            Debug.LogError("Assign the floor and wall Tilemaps.", this);
            return;
        }

        Vector3 floorOrigin =
            floorTilemap.GetCellCenterWorld(Vector3Int.zero);

        Vector3 wallOrigin =
            wallTilemap.GetCellCenterWorld(Vector3Int.zero);

        Vector3 floorX =
            floorTilemap.GetCellCenterWorld(Vector3Int.right)
            - floorOrigin;

        Vector3 wallX =
            wallTilemap.GetCellCenterWorld(Vector3Int.right)
            - wallOrigin;

        Vector3 floorY =
            floorTilemap.GetCellCenterWorld(Vector3Int.up)
            - floorOrigin;

        Vector3 wallY =
            wallTilemap.GetCellCenterWorld(Vector3Int.up)
            - wallOrigin;

        int floorCount = 0;
        int wallCount = 0;
        int overlappingCells = 0;
        string firstOverlap = "none";

        foreach (Vector3Int cell in
                 floorTilemap.cellBounds.allPositionsWithin)
        {
            if (!floorTilemap.HasTile(cell))
                continue;

            floorCount++;

            if (wallTilemap.HasTile(cell))
            {
                overlappingCells++;

                if (firstOverlap == "none")
                    firstOverlap = cell.ToString();
            }
        }

        foreach (Vector3Int cell in
                 wallTilemap.cellBounds.allPositionsWithin)
        {
            if (wallTilemap.HasTile(cell))
                wallCount++;
        }

        Debug.Log(
            $"CORRIDOR WALL DIAGNOSTICS\n" +
            $"Floor cells: {floorCount}\n" +
            $"Base wall cells: {wallCount}\n" +
            $"Walls sharing floor cells: {overlappingCells}\n" +
            $"First overlap: {firstOverlap}\n" +
            $"Wall grid offset: {(wallOrigin - floorOrigin).ToString("F6")}\n" +
            $"X step difference: {(wallX - floorX).ToString("F6")}\n" +
            $"Y step difference: {(wallY - floorY).ToString("F6")}",
            this
        );
    }

    public void PaintSingleCorridorWall(Vector2Int position)
    {
        if (isometricWallTile == null)
        {
            throw new InvalidOperationException(
                "Assign Isometric Wall Tile on TileMapVisualizer."
            );
        }

        PaintSingleTile(
            wallTilemap,
            isometricWallTile,
            position
        );

        paintedWallCount++;
    }
}
