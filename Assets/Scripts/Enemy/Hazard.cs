using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Min(0f)]
    private float damage = 10f;

    private PlayerHealth _touching;

    private void OnCollisionEnter2D(Collision2D collision) => Touch(collision.collider);

    private void OnCollisionExit2D(Collision2D collision) => Release(collision.collider);

    private void OnTriggerEnter2D(Collider2D other) => Touch(other);

    private void OnTriggerExit2D(Collider2D other) => Release(other);

    private void OnDisable() => _touching = null;

    private void Update()
    {
        if (_touching == null)
            return;

        Damage(_touching);
    }

    private void Touch(Component source)
    {
        var health = source.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        _touching = health;
        Damage(health);
    }

    private void Release(Component source)
    {
        if (_touching != null && source.GetComponentInParent<PlayerHealth>() == _touching)
            _touching = null;
    }

    private void Damage(PlayerHealth health)
    {
        health.TakeDamage(damage);
    }
}
