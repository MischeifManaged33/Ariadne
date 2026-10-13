using System;
using UnityEngine;

// Breakable objects by hit
[RequireComponent(typeof(Collider2D))]
public class Breakable : MonoBehaviour, IDamagable
{
    [SerializeField, Min(1)]
    private int hitsToBreak = 1;

    public event Action Broken;

    // IDamagable
    public float MaxHealth => hitsToBreak;
    public float CurrentHealth { get; private set; }
    public float Normalized => hitsToBreak > 0 ? CurrentHealth / hitsToBreak : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    private HitFlash _hitFlash;

    private void Awake()
    {
        _hitFlash = HitFlash.GetOrAdd(gameObject);

        CurrentHealth = hitsToBreak;
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        CurrentHealth--;

        if (IsAlive) {
            _hitFlash.Flash();
            return 1f;
        }

        Break();
        return 1f;
    }

    public float Heal(float amount) => 0f;

    private void Break()
    {
        Broken?.Invoke();
        Destroy(gameObject);
    }
}
