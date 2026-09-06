using UnityEngine;

public abstract class AbstractDungeonGenerator : MonoBehaviour
{
    [SerializeField]
    protected TileMapVisualizer tileMapVisualizer = null;

    [SerializeField]
    protected Vector2Int startPos = Vector2Int.zero;

    public void Start()
    {
        GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        Debug.Log("GENERATE DUNGEON CALLED!");
        tileMapVisualizer.Clear();
        RunProceduralGeneration();
    }

    protected abstract void RunProceduralGeneration();
}
