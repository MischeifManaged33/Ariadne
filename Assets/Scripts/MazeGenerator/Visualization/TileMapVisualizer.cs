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

    private void PaintSingleTile(
    Tilemap tilemap,
    TileBase tile,
    Vector2Int position)
    {
        Vector3Int cellPosition =
            new Vector3Int(position.x, position.y, 0);

        tilemap.SetTile(cellPosition, tile);
    }

    public void PaintIsometricWalls(HashSet<Vector2Int> floorPositions)
    {
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
            PaintSingleTile(
                wallTilemap,
                isometricWallTile,
                wallPosition
            );
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


        if (tile != null)
        {
            PaintSingleTile(wallTilemap, tile, position);
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

    public void Clear()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
    }
}
