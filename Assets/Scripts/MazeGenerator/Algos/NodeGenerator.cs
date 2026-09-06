using UnityEngine;
using System.Collections.Generic;

public class NodeGenerator : MonoBehaviour
{
    [SerializeField]
    private GameObject nodePrefab;
    [SerializeField]
    private TileMapVisualizer tilemapVisualizer;

    private Dictionary<Vector2Int, Node> nodes = new Dictionary<Vector2Int, Node> ();

    public void GenerateNodes(HashSet<Vector2Int> floorPositions)
    {
        Debug.Log("GENERATE NODES CALLED!");

        ClearNodes();

        foreach (Vector2Int position in floorPositions)
        {
            Vector3 worldPosition =
                tilemapVisualizer.GetFloorWorldPosition(position);

            GameObject nodeObject = Instantiate(
                nodePrefab,
                worldPosition,
                Quaternion.identity,
                transform
            );

            Node node = nodeObject.GetComponent<Node>();

            nodes.Add(position, node);
        }

        foreach (Vector2Int position in floorPositions)
        {
            Node currentNode = nodes[position];

            foreach (Vector2Int direction in Direction2D.cardinalDirList)
            {
                Vector2Int neighborPosition = position + direction;

                if (nodes.ContainsKey(neighborPosition))
                {
                    currentNode.connections.Add(nodes[neighborPosition]);
                }
            }
        }
    }

    private void ClearNodes ()
    {
        foreach (Node node in nodes.Values)
        {
            Destroy(node.gameObject);
        }

        nodes.Clear();
    }
}
