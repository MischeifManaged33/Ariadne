using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class IsometricGridGizmo : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap boundsTilemap;
    [SerializeField]
    private Color lineColor =
        new Color(0f, 1f, 1f, 0.65f);

    [SerializeField, Min(0)] private int padding = 1;
    [SerializeField] private float pointSize = 0.06f;

    private void OnDrawGizmos()
    {
        if (grid == null || boundsTilemap == null)
            return;

        Gizmos.color = lineColor;

        BoundsInt bounds = boundsTilemap.cellBounds;

        int minX = bounds.xMin - padding;
        int maxX = bounds.xMax + padding;
        int minY = bounds.yMin - padding;
        int maxY = bounds.yMax + padding;

        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                Vector3 bottom =
                    grid.CellToWorld(cell);

                Vector3 right =
                    grid.CellToWorld(cell + Vector3Int.right);

                Vector3 top =
                    grid.CellToWorld(
                        cell + Vector3Int.right + Vector3Int.up
                    );

                Vector3 left =
                    grid.CellToWorld(cell + Vector3Int.up);

                Gizmos.DrawLine(bottom, right);
                Gizmos.DrawLine(right, top);
                Gizmos.DrawLine(top, left);
                Gizmos.DrawLine(left, bottom);

                Vector3 center =
                    grid.GetCellCenterWorld(cell);

                Gizmos.DrawSphere(center, pointSize);
            }
        }
    }
}