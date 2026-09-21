using UnityEngine;

[ExecuteAlways]
public class SnapToGrid : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private bool snapNow;

    private void OnValidate()
    {
        if (!snapNow || grid == null)
            return;

        Vector3Int cell = grid.WorldToCell(transform.position);
        transform.position = grid.GetCellCenterWorld(cell);

        snapNow = false;
    }
}