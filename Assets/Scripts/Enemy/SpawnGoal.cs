using UnityEngine;
using System.Collections.Generic;

public class SpawnGoal : MonoBehaviour
{
    // 1. Create a public static reference to the script instance
    public static SpawnGoal Instance;

    // 2. Remove static from these fields so they show up in the Inspector
    public GameObject player;
    public GameObject goal;

    private void Awake()
    {
        // 3. Assign the instance when the game starts
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 4. Change this to a regular method (or keep it static and reference Instance)
    public static void Spawn()
    {
        if (Instance != null && Instance.goal != null && Instance.player != null)
        {
            Instantiate(Instance.goal, Instance.player.transform.position, Instance.player.transform.rotation);
        }
        else
        {
            Debug.LogWarning("SpawnGoal Instance or its references are missing!");
        }
    }
}
