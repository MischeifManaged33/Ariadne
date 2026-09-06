using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField]
    private WeaponData equippedWeapon;

    [Header("References")]
    [SerializeField]
    private Transform weaponPivot;

    [SerializeField]
    private SpriteRenderer weaponRenderer;

    [Header("Targets")]
    [SerializeField]
    private LayerMask enemyLayers;

    private float nextAttackTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Equip(equippedWeapon);
    }

    // Update is called once per frame
    void Update()
    {

        if (Keyboard.current != null &&
    Keyboard.current.eKey.wasPressedThisFrame)
        {
            Attack();
        }
    }

    public void Equip(WeaponData newWeapon)
    {
        equippedWeapon = newWeapon;

        if (weaponRenderer != null)
        {
            weaponRenderer.sprite = equippedWeapon != null ? weaponRenderer.sprite : null;
        }
    }

    private void Attack()
    {
        if (equippedWeapon == null)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + equippedWeapon.attackCooldown;

        Vector2 attackPosition = (Vector2)weaponPivot.position + (Vector2)weaponPivot.right * equippedWeapon.attackRange;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPosition, equippedWeapon.hitRadius, enemyLayers);

        foreach (Collider2D hit in hits)
        {
            IDamagable target = hit.GetComponentInParent<IDamagable>();
            Debug.Log("Hit enemy");

            if (target != null || weaponPivot == null)
            {
                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (equippedWeapon == null || weaponPivot == null)
        {
            return;
        }

        Vector2 attackPosition =
            (Vector2)weaponPivot.position
            + (Vector2)weaponPivot.right
            * equippedWeapon.attackRange;

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            attackPosition,
            equippedWeapon.hitRadius
        );
    }
}
