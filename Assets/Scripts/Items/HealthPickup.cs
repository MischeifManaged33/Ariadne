using UnityEngine;

// Heals the player on contact if they are missing health
[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour
{
    [Header("Healing")]
    [SerializeField, Min(0f)]
    private float healAmount = 25f;

    public float HealAmount => healAmount;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHeal(other);

    private void OnTriggerStay2D(Collider2D other) => TryHeal(other);

    private void TryHeal(Collider2D other)
    {
        if (!isActiveAndEnabled || healAmount <= 0f)
            return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.CurrentHealth >= health.MaxHealth)
            return;

        if (health.Heal(healAmount) <= 0f)
            return;

        enabled = false;
        Destroy(gameObject);
    }
}
