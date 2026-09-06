using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class NPCController : MonoBehaviour, IDamagable
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => CurrentHealth / MaxHealth;
    public bool IsAlive => CurrentHealth > 0f;
    public int panicMultiplier = 1;


    public Node currentNode;
    public List<Node> path = new List<Node>();

    public enum StateMachine
    {
        Patrol,
        Engage,
        Evade
    }

    public StateMachine currentState;

    public PlayerController player;

    public float speed = 3f;

    private void Start()
    {
        CurrentHealth = maxHealth;
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
        {
            return 0f;
        }

        float appliedDamage = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= appliedDamage;

        Debug.Log($"{name} took {appliedDamage} damage.");

        if (!IsAlive)
        {
            Die();
        }

        return appliedDamage;
    }

    public float Heal(float amount)
    {
        if (amount <= 0f || !IsAlive)
        {
            return 0f;
        }

        float appliedHealing =
            Mathf.Min(amount, MaxHealth - CurrentHealth);

        CurrentHealth += appliedHealing;
        return appliedHealing;
    }

    private void Die()
    {
        path.Clear();
        Destroy(gameObject);
    }

    private void Update()
    {
        if (AStarManager.instance == null)
        {
            return;
        }

        // Nodes may not exist during Start(), so keep trying until they do.
        if (currentNode == null)
        {
            currentNode =
                AStarManager.instance.FindNearestNode(transform.position);

            if (currentNode == null)
            {
                return;
            }
        }

        if (player == null)
        {
            Debug.LogError("NPCController has no Player assigned.");
            return;
        }

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

        bool playerSeen =
            Vector2.Distance(transform.position, player.transform.position) < 5f;

        if (!playerSeen &&
            currentState != StateMachine.Patrol &&
            CurrentHealth > (MaxHealth * 20) / 100)
        {
            currentState = StateMachine.Patrol;
            path.Clear();
        }
        else if (playerSeen &&
                 currentState != StateMachine.Engage &&
                 CurrentHealth > (MaxHealth * 20) / 100)
        {
            currentState = StateMachine.Engage;
            path.Clear();
        }
        else if (currentState != StateMachine.Evade &&
                 CurrentHealth <= (MaxHealth * 20) / 100)
        {
            panicMultiplier = 2;
            currentState = StateMachine.Evade;
            path.Clear();
        }

        CreatePath();
    }

    void Patrol()
    {
        if (currentNode == null || path.Count != 0)
        {
            return;
        }

        Node[] allNodes = AStarManager.instance.AllNodes();

        if (allNodes.Length == 0)
        {
            return;
        }

        Node targetNode =
            allNodes[Random.Range(0, allNodes.Length)];

        List<Node> newPath =
            AStarManager.instance.GeneratePath(currentNode, targetNode);

        if (newPath != null)
        {
            path = newPath;
        }
    }

    void Engage()
    {
        if (path.Count == 0)
        {
            path = AStarManager.instance.GeneratePath(currentNode, AStarManager.instance.FindNearestNode(player.transform.position));
        }
    }

    void Evade()
    {
        if (path.Count == 0)
        {
            path = AStarManager.instance.GeneratePath(currentNode, AStarManager.instance.FindFurthestNode(player.transform.position));
        }
    }

    public void CreatePath()
    {
        if (path.Count > 0)
        {
            int x = 0;
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(path[x].transform.position.x, path[x].transform.position.y, -2), (speed * panicMultiplier) * Time.deltaTime);

            if (Vector2.Distance(transform.position, path[x].transform.position) < 0.1f)
            {
                currentNode = path[x];
                path.RemoveAt(x);
            }
        }
    }
}