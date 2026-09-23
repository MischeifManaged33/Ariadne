using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Grid grid;

    [Tooltip("Cell occupied by the bottom-left character of the layout.")]
    [SerializeField] private Vector3Int originCell;

    [Tooltip("Moves every spawned puzzle object visually without changing its cell.")]
    [SerializeField] private Vector3 worldOffset;

    [Header("Layout")]
    [SerializeField, TextArea(10, 20)]
    private string layout =
@"####E####
#.......#
#.P.P.P.#
#.......#
#.B...B.#
#...#...#
#...B...#
#...S...#
####.####";

    [Header("Prefabs")]
    [SerializeField] private PuzzleBlock blockPrefab;
    [SerializeField] private PuzzlePlate platePrefab;
    [SerializeField] private Transform spawnedObjectsParent;

    [Header("Movement")]
    [SerializeField, Min(0.01f)]
    private float moveDuration = 0.15f;
    [SerializeField, Min(0.1f)]
    private float maximumPushDistance = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onPuzzleSolved;
    [SerializeField] private UnityEvent onPuzzleUnsolved;

    private readonly HashSet<Vector3Int> walkableCells = new();
    private readonly Dictionary<Vector3Int, PuzzleBlock> blocksByCell = new();
    private readonly Dictionary<Vector3Int, PuzzlePlate> platesByCell = new();
    private readonly Dictionary<PuzzleBlock, Vector3Int> startingBlockCells = new();

    private string[] layoutRows;
    private bool puzzleSolved;

    private void Start()
    {
        BuildPuzzle();
    }

    private void BuildPuzzle()
    {
        walkableCells.Clear();
        blocksByCell.Clear();
        platesByCell.Clear();
        startingBlockCells.Clear();

        if (grid == null)
        {
            Debug.LogError("PuzzleManager needs a Grid.");
            return;
        }

        if (blockPrefab == null || platePrefab == null)
        {
            Debug.LogError(
                "PuzzleManager needs block and plate prefabs."
            );

            return;
        }

        walkableCells.Clear();
        blocksByCell.Clear();
        platesByCell.Clear();

        layoutRows = GetLayoutRows();

        int height = layoutRows.Length;

        // Build cells and spawn plates first.
        for (int row = 0; row < height; row++)
        {
            string line = layoutRows[row];

            for (int column = 0; column < line.Length; column++)
            {
                char symbol = line[column];
                Vector3Int cell =
                    GetCell(column, row, height);

                if (IsWalkableSymbol(symbol))
                    walkableCells.Add(cell);

                if (symbol == 'P' || symbol == '*')
                    SpawnPlate(cell);
            }
        }

        // Spawn blocks after plates so blocks render above them.
        for (int row = 0; row < height; row++)
        {
            string line = layoutRows[row];

            for (int column = 0; column < line.Length; column++)
            {
                char symbol = line[column];

                if (symbol != 'B' && symbol != '*')
                    continue;

                Vector3Int cell =
                    GetCell(column, row, height);

                SpawnBlock(cell);
            }
        }

        UpdatePressurePlates();
    }

    private string[] GetLayoutRows()
    {
        string cleaned = layout
            .Replace("\r", "")
            .Trim('\n');

        return cleaned.Split('\n');
    }

    private Vector3Int GetCell(
        int column,
        int row,
        int height)
    {
        // The first text row is displayed as the top row.
        int cellY = height - 1 - row;

        return originCell +
            new Vector3Int(column, cellY, 0);
    }

    private static bool IsWalkableSymbol(char symbol)
    {
        return symbol == '.' ||
               symbol == 'B' ||
               symbol == 'P' ||
               symbol == '*' ||
               symbol == 'S' ||
               symbol == 'E';
    }

    private void SpawnBlock(Vector3Int cell)
    {
        PuzzleBlock block = Instantiate(
            blockPrefab,
            CellToWorld(cell),
            Quaternion.identity,
            spawnedObjectsParent
        );

        block.Initialize(this, cell);

        blocksByCell.Add(cell, block);
        startingBlockCells.Add(block, cell);
    }

    public void ResetPuzzle()
    {
        // Stop any blocks currently animating.
        StopAllCoroutines();

        blocksByCell.Clear();

        foreach (
            KeyValuePair<PuzzleBlock, Vector3Int> entry
            in startingBlockCells)
        {
            PuzzleBlock block = entry.Key;
            Vector3Int startingCell = entry.Value;

            if (block == null)
                continue;

            block.Initialize(this, startingCell);
            block.transform.position = CellToWorld(startingCell);

            blocksByCell.Add(startingCell, block);
        }

        UpdatePressurePlates();

        Debug.Log("Puzzle reset.");
    }

    private void SpawnPlate(Vector3Int cell)
    {
        PuzzlePlate plate = Instantiate(
            platePrefab,
            CellToWorld(cell),
            Quaternion.identity,
            spawnedObjectsParent
        );

        plate.Initialize(cell);
        platesByCell.Add(cell, plate);
    }

    public bool TryPush(
    PuzzleBlock block,
    Vector3Int direction)
    {
        if (block == null || block.IsMoving)
            return false;

        if (direction == Vector3Int.zero)
            return false;

        Vector3Int destination = block.Cell + direction;

        if (!walkableCells.Contains(destination))
            return false;

        if (blocksByCell.ContainsKey(destination))
            return false;

        StartCoroutine(MoveBlock(block, destination));
        return true;
    }

    private IEnumerator MoveBlock(
        PuzzleBlock block,
        Vector3Int destination)
    {
        block.SetMoving(true);

        blocksByCell.Remove(block.Cell);
        blocksByCell.Add(destination, block);

        Vector3 startPosition = block.transform.position;
        Vector3 destinationPosition = CellToWorld(destination);

        block.SetCell(destination);

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float amount =
                Mathf.Clamp01(elapsed / moveDuration);

            block.transform.position = Vector3.Lerp(
                startPosition,
                destinationPosition,
                amount
            );

            yield return null;
        }

        block.transform.position = destinationPosition;
        block.SetMoving(false);

        UpdatePressurePlates();
    }

    public Vector3Int GetPushDirection(
    Vector3Int blockCell,
    Vector3 playerWorldPosition)
    {
        Vector3Int[] directions =
        {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

        float closestDistance = float.PositiveInfinity;
        Vector3Int playerSide = Vector3Int.zero;

        foreach (Vector3Int direction in directions)
        {
            Vector3Int neighboringCell =
                blockCell + direction;

            Vector3 neighboringPosition =
                CellToWorld(neighboringCell);

            float distance = Vector2.SqrMagnitude(
                (Vector2)(playerWorldPosition - neighboringPosition)
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                playerSide = direction;
            }
        }

        // Player must be close to one of the four neighboring cells.
        if (closestDistance >
            maximumPushDistance * maximumPushDistance)
        {
            return Vector3Int.zero;
        }

        return -playerSide;
    }

    private void UpdatePressurePlates()
    {
        bool allPressed = platesByCell.Count > 0;

        foreach (
            KeyValuePair<Vector3Int, PuzzlePlate> plate
            in platesByCell)
        {
            bool pressed =
                blocksByCell.ContainsKey(plate.Key);

            plate.Value.SetPressed(pressed);

            if (!pressed)
                allPressed = false;
        }

        if (allPressed == puzzleSolved)
            return;

        puzzleSolved = allPressed;

        if (puzzleSolved)
            onPuzzleSolved?.Invoke();
        else
            onPuzzleUnsolved?.Invoke();
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        return grid.GetCellCenterWorld(cell) + worldOffset;
    }

    private void OnDrawGizmosSelected()
    {
        if (grid == null)
            return;

        string[] rows = GetLayoutRows();
        int height = rows.Length;

        for (int row = 0; row < height; row++)
        {
            string line = rows[row];

            for (int column = 0; column < line.Length; column++)
            {
                char symbol = line[column];

                if (symbol == ' ' || symbol == '_')
                    continue;

                Vector3Int cell =
                    GetCell(column, row, height);

                DrawCell(cell, symbol);
            }
        }
    }

    public Vector3Int InputToGridDirection(Vector2 input)
    {
        const float threshold = 0.1f;

        bool left = input.x < -threshold;
        bool right = input.x > threshold;
        bool up = input.y > threshold;
        bool down = input.y < -threshold;

        // Isometric diagonal controls
        if (up && left)
            return Vector3Int.up;

        if (up && right)
            return Vector3Int.right;

        if (down && left)
            return Vector3Int.left;

        if (down && right)
            return Vector3Int.down;

        // Fallbacks for a single key
        if (up)
            return Vector3Int.up;

        if (right)
            return Vector3Int.right;

        if (down)
            return Vector3Int.down;

        if (left)
            return Vector3Int.left;

        return Vector3Int.zero;
    }

    private void DrawCell(Vector3Int cell, char symbol)
    {
        if (symbol == '#')
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        else if (symbol == 'B')
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        else if (symbol == 'P')
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        else if (symbol == '*')
            Gizmos.color = Color.magenta;
        else
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);

        Vector3 bottom =
            grid.CellToWorld(cell) + worldOffset;

        Vector3 right =
            grid.CellToWorld(cell + Vector3Int.right) +
            worldOffset;

        Vector3 top =
            grid.CellToWorld(
                cell + Vector3Int.right + Vector3Int.up
            ) + worldOffset;

        Vector3 left =
            grid.CellToWorld(cell + Vector3Int.up) +
            worldOffset;

        Gizmos.DrawLine(bottom, right);
        Gizmos.DrawLine(right, top);
        Gizmos.DrawLine(top, left);
        Gizmos.DrawLine(left, bottom);

        Gizmos.DrawSphere(CellToWorld(cell), 0.06f);
    }
}