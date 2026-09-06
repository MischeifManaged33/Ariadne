using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyAttack : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float damage = 10f;

    [SerializeField, Min(0.01f)]
    private float attackInterval = 1f;

    private float nextAttackTime;

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        Debug.Log("Enemy hits you");
        playerHealth.TakeDamage(damage);
        nextAttackTime = Time.time + attackInterval;
    }
}