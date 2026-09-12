using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

public class Node : MonoBehaviour
{
    public Node cameFrom;
    public List<Node> connections = new List<Node>();

    public float gscore;
    public float hscore;

    public float FScore()
    {
        return gscore + hscore;
    }

    private void OnDrawGizmos()
    {
        if (connections == null)
            return;

        Gizmos.color = Color.blue;

        foreach (Node connection in connections)
        {
            if (connection != null)
            {
                Gizmos.DrawLine(
                    transform.position,
                    connection.transform.position
                );
            }
        }
    }
}
