using UnityEngine;
using System.Collections.Generic;

public class NodeGenerator : MonoBehaviour
{
    [SerializeField]
    private GameObject nodePrefab;

    [SerializeField]
    private TileMapVisualizer tilemapVisualizer;

    [SerializeField]
    private Transform nodeParent;

    private Dictionary<Vector2Int, Node> nodes =
        new Dictionary<Vector2Int, Node>();

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
                nodeParent
            );

            Node node = nodeObject.GetComponent<Node>();

            if (node == null)
            {
                Debug.LogError(
                    "The node prefab does not have a Node component!"
                );

                Destroy(nodeObject);
                continue;
            }

            node.connections.Clear();
            nodes.Add(position, node);
        }

        foreach (Vector2Int position in floorPositions)
        {
            Node currentNode = nodes[position];

            foreach (Vector2Int direction
                     in Direction2D.cardinalDirList)
            {
                Vector2Int neighborPosition =
                    position + direction;

                if (nodes.TryGetValue(
                    neighborPosition,
                    out Node neighborNode))
                {
                    currentNode.connections.Add(neighborNode);
                }
            }
        }
    }

    private void ClearNodes()
    {
        for (int i = nodeParent.childCount - 1; i >= 0; i--)
        {
            GameObject nodeObject =
                nodeParent.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                // FindObjectsOfType will immediately stop
                // returning this node.
                nodeObject.SetActive(false);
                Destroy(nodeObject);
            }
            else
            {
                DestroyImmediate(nodeObject);
            }
        }

        nodes.Clear();
    }
}