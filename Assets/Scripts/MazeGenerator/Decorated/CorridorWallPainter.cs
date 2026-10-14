using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CorridorWallPainter : MonoBehaviour
{
    [Header("Wall Tilemaps")]
    [SerializeField] private Tilemap wallsAlongX;
    [SerializeField] private Tilemap wallsAlongY;

    // Reuses your existing corner Tilemap for small connectors.
    [SerializeField] private Tilemap corners;

    [Header("Wall Pieces")]
    [SerializeField] private TileBase straightAlongX;
    [SerializeField] private TileBase straightAlongY;
    [SerializeField] private TileBase connectorTile;

    [Header("Connector Offsets — Local Units")]
    [SerializeField] private Vector3 insideJoinOffset;
    [SerializeField] private Vector3 outsideJoinOffset;

    public void Paint(
    HashSet<Vector2Int> corridorFloor,
    HashSet<Vector2Int> allFloor,
    HashSet<Vector2Int> protectedCells)
    {
        if (wallsAlongX == null ||
            wallsAlongY == null ||
            straightAlongX == null ||
            straightAlongY == null)
        {
            throw new InvalidOperationException(
                "Assign both straight-wall Tilemaps and tiles."
            );
        }

        if (wallsAlongX == wallsAlongY)
        {
            throw new InvalidOperationException(
                "The two wall directions need separate Tilemaps."
            );
        }

        HashSet<Vector2Int> xEdges = new HashSet<Vector2Int>();
        HashSet<Vector2Int> yEdges = new HashSet<Vector2Int>();

        foreach (Vector2Int floor in corridorFloor)
        {
            Vector2Int xEdge = floor + Vector2Int.up;
            Vector2Int yEdge = floor + Vector2Int.right;

            if (CanPaint(xEdge, allFloor, protectedCells))
                xEdges.Add(xEdge);

            if (CanPaint(yEdge, allFloor, protectedCells))
                yEdges.Add(yEdge);
        }
        // Find missing diagonal joins using the original edge sets.
        HashSet<Vector2Int> joins = new HashSet<Vector2Int>();

        foreach (Vector2Int floor in corridorFloor)
        {
            Vector2Int join =
                floor + Vector2Int.up + Vector2Int.right;

            if (!CanPaint(join, allFloor, protectedCells))
                continue;

            bool connectsBothDirections =
                xEdges.Contains(join + Vector2Int.left) &&
                yEdges.Contains(join + Vector2Int.down);

            if (connectsBothDirections)
                joins.Add(join);
        }

        // Extend both straight faces into the shared join cell.
        // No corner or connector sprite is used.
        //xEdges.UnionWith(joins);


        wallsAlongX.ClearAllTiles();
        wallsAlongY.ClearAllTiles();

        if (corners != null)
            corners.ClearAllTiles();

        // Keep both wall faces when they share a cell.
        PaintCells(
            wallsAlongX,
            straightAlongX,
            xEdges,
            Vector3.zero
        );

        PaintCells(
            wallsAlongY,
            straightAlongY,
            yEdges,
            Vector3.zero
        );
    }

    private static bool CanPaint(
        Vector2Int cell,
        HashSet<Vector2Int> allFloor,
        HashSet<Vector2Int> protectedCells)
    {
        return !allFloor.Contains(cell) &&
               !protectedCells.Contains(cell);
    }

    private static void PaintCells(
        Tilemap tilemap,
        TileBase tile,
        IEnumerable<Vector2Int> cells,
        Vector3 offset)
    {
        foreach (Vector2Int position in cells)
        {
            Vector3Int cell = new Vector3Int(
                position.x,
                position.y,
                0
            );

            tilemap.SetTile(cell, tile);

            if (offset == Vector3.zero)
                continue;

            Matrix4x4 original = tilemap.GetTransformMatrix(cell);

            tilemap.SetTileFlags(
                cell,
                tilemap.GetTileFlags(cell) & ~TileFlags.LockTransform
            );

            tilemap.SetTransformMatrix(
                cell,
                Matrix4x4.Translate(offset) * original
            );
        }
    }
}