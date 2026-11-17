using UnityEngine;

// Weapon pickup item; picking it up drops the currently equipped weapon in its place
[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour, IInteractable
{
    [SerializeField]
    private WeaponData weapon;
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    public WeaponData Weapon => weapon;

    public bool CanInteract => isActiveAndEnabled && weapon != null;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        GetComponent<Collider2D>().isTrigger = true;
    }

    public void SetWeapon(WeaponData newWeapon)
    {
        weapon = newWeapon;
    }

    public void Interact(Player player)
    {
        if (!CanInteract || player == null)
            return;

        var playerWeapon = player.GetComponentInChildren<PlayerWeapon>();
        if (playerWeapon == null)
            return;

        var previous = playerWeapon.EquippedWeapon;
        playerWeapon.Equip(weapon);

        if (previous == null || previous == weapon) {
            Destroy(gameObject);
            return;
        }

        if (previous.pickupPrefab != null) {
            var dropped = Instantiate(previous.pickupPrefab, transform.position, transform.rotation, transform.parent);
            dropped.SetWeapon(previous);
            Destroy(gameObject);
            return;
        }

        weapon = previous;
        ShowSprite(previous.sprite);
    }

    private void ShowSprite(Sprite sprite)
    {
        if (spriteRenderer == null || sprite == null)
            return;

        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = false;

        spriteRenderer.sprite = sprite;
    }
}
