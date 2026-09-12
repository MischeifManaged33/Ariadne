using System.Collections.Generic;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    public Node currentNode;

    public List<Node> path =
        new List<Node>();

    public enum StateMachine
    {
        Patrol,
        Engage,
        Evade
    }

    public StateMachine currentState;

    [SerializeField]
    private PlayerController player;

    [SerializeField]
    private float speed = 3f;

    [SerializeField]
    private float evadeSpeedMultiplier = 2f;

    [SerializeField]
    private float detectionDistance = 5f;

    private void Update()
    {
        if (AStarManager.instance == null)
            return;

        if (currentNode == null)
        {
            currentNode =
                AStarManager.instance.FindNearestNode(
                    transform.position
                );

            if (currentNode == null)
                return;
        }

        if (player == null)
        {
            Debug.LogError(
                "NPCController has no Player assigned."
            );

            return;
        }

        UpdateState();

        switch (currentState)
        {
            case StateMachine.Patrol:
                Patrol();
                break;

            case StateMachine.Engage:
                Engage();
                break;

            case StateMachine.Evade:
                Evade();
                break;
        }

        FollowPath();
    }

    private void UpdateState()
    {
        // Evade is controlled externally, such as by
        // a separate health component.
        if (currentState == StateMachine.Evade)
            return;

        bool playerSeen =
            Vector2.Distance(
                transform.position,
                player.transform.position
            ) < detectionDistance;

        StateMachine desiredState = playerSeen
            ? StateMachine.Engage
            : StateMachine.Patrol;

        if (currentState != desiredState)
        {
            currentState = desiredState;
            path.Clear();
        }
    }

    private void Patrol()
    {
        if (path.Count > 0)
            return;

        Node[] allNodes =
            AStarManager.instance.AllNodes();

        if (allNodes.Length == 0)
            return;

        Node targetNode =
            allNodes[
                Random.Range(0, allNodes.Length)
            ];

        SetPath(targetNode);
    }

    private void Engage()
    {
        if (path.Count > 0)
            return;

        Node targetNode =
            AStarManager.instance.FindNearestNode(
                player.transform.position
            );

        SetPath(targetNode);
    }

    private void Evade()
    {
        if (path.Count > 0)
            return;

        Node targetNode =
            AStarManager.instance.FindFurthestNode(
                player.transform.position
            );

        SetPath(targetNode);
    }

    private void SetPath(Node targetNode)
    {
        if (currentNode == null || targetNode == null)
            return;

        List<Node> newPath =
            AStarManager.instance.GeneratePath(
                currentNode,
                targetNode
            );

        path = newPath ?? new List<Node>();
    }

    private void FollowPath()
    {
        if (path.Count == 0)
            return;

        Node nextNode = path[0];

        // This can happen if nodes regenerate during movement.
        if (nextNode == null)
        {
            path.Clear();
            return;
        }

        float currentSpeed = speed;

        if (currentState == StateMachine.Evade)
        {
            currentSpeed *= evadeSpeedMultiplier;
        }

        Vector3 targetPosition =
            nextNode.transform.position;

        targetPosition.z = -2f;

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                currentSpeed * Time.deltaTime
            );

        if (Vector2.Distance(
                transform.position,
                nextNode.transform.position
            ) < 0.1f)
        {
            currentNode = nextNode;
            path.RemoveAt(0);
        }
    }

    public void RefreshAfterNodeRegeneration()
    {
        path.Clear();

        if (AStarManager.instance == null)
        {
            currentNode = null;
            return;
        }

        currentNode =
            AStarManager.instance.FindNearestNode(
                transform.position
            );
    }

    public void SetState(StateMachine newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        path.Clear();
    }
}