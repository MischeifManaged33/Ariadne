using System;
using UnityEngine;
using UnityEngine.Events;

// Dummy for testing
[RequireComponent(typeof(Collider2D))]
public class DummyEnemy : MonoBehaviour, IDamagable
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 50f;


    [Header("Death")]
    [SerializeField]
    private bool destroyOnDeath = true;
    [SerializeField, Min(0f)]
    private float destroyDelay = 0f;


    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action Died;

    // IDamagable
    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsAlive => CurrentHealth > 0f;
    private bool _dead;

    private void Reset()
    {
        Anchor(GetComponent<Rigidbody2D>());
    }

    private void Awake()
    {

        Anchor(GetComponent<Rigidbody2D>());

        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        RaiseHealthChanged();
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= applied;

        Damaged?.Invoke(applied);
        RaiseHealthChanged();

        if (!IsAlive)
            Die();

        return applied;
    }

    public float Heal(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, maxHealth - CurrentHealth);
        if (applied <= 0f)
            return 0f;

        CurrentHealth += applied;

        Healed?.Invoke(applied);
        RaiseHealthChanged();

        return applied;
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        _dead = false;
        RaiseHealthChanged();
    }

    private void Die()
    {
        if (_dead)
            return;

        _dead = true;

        Died?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }


    private void RaiseHealthChanged() => HealthChanged?.Invoke(CurrentHealth, maxHealth);

    private static void Anchor(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Static;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
    }
}
