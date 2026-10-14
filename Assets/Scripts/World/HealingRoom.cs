using UnityEngine;

// For the spa or other rooms
[RequireComponent(typeof(Collider2D))]
public class HealingRoom : MonoBehaviour
{
    [Header("Healing")]
    [SerializeField, Min(0f)]
    private float healPerSecond = 5f;

    public bool IsUsed { get; private set; }
    public bool IsHealing => _target != null;

    private PlayerHealth _target;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (_target == null)
            return;

        if (_target.CurrentHealth >= _target.MaxHealth) {
            _target = null;
            return;
        }

        _target.Heal(healPerSecond * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsUsed)
            return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        IsUsed = true;
        _target = health;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_target == null)
            return;

        if (other.GetComponentInParent<PlayerHealth>() == _target)
            _target = null;
    }
}
