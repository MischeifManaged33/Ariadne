using UnityEngine;

// Weapon pickup item
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

        playerWeapon.Equip(weapon);

        Destroy(gameObject);
    }


}
