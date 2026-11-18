using UnityEngine;

// Adds thread to the player's spool on contact (can exceed the starting length)
[RequireComponent(typeof(Collider2D))]
public class YarnPickup : MonoBehaviour
{
    [Header("Thread")]
    [SerializeField, Min(0f)]
    private float threadAmount = 15f;

    public float ThreadAmount => threadAmount;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

    private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

    private void TryCollect(Collider2D other)
    {
        if (!isActiveAndEnabled || threadAmount <= 0f)
            return;

        var thread = FindThread(other);
        if (thread == null)
            return;

        thread.AddThread(threadAmount);

        enabled = false;
        Destroy(gameObject);
    }

    private static AriadneThread FindThread(Collider2D other)
    {
        var thread = other.GetComponentInParent<AriadneThread>();
        if (thread != null)
            return thread;

        var player = other.GetComponentInParent<PlayerHealth>();
        return player != null
            ? player.GetComponentInChildren<AriadneThread>(true)
            : null;
    }
}
