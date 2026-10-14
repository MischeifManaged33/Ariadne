using UnityEngine;

// Heals the player when picked up
[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour, IInteractable
{
    [Header("Healing")]
    [SerializeField, Min(0f)]
    private float healAmount = 25f;

    public float HealAmount => healAmount;

    public bool CanInteract => isActiveAndEnabled && healAmount > 0f
        && (PlayerHealth.Current == null || PlayerHealth.Current.CurrentHealth < PlayerHealth.Current.MaxHealth);

    private void Awake()
    {

        GetComponent<Collider2D>().isTrigger = true;
    }

    public void Interact(Player player)
    {
        if (!CanInteract || player == null)
            return;

        var health = player.GetComponentInParent<PlayerHealth>();
        if (health == null || health.Heal(healAmount) <= 0f)
            return;

        Destroy(gameObject);
    }
}
