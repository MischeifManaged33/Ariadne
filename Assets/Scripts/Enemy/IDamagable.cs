public interface IDamagable
{
    float MaxHealth { get; }
    float CurrentHealth { get; }

    float Normalized { get; }
    bool IsAlive { get; }
    float TakeDamage(float amount);
    float Heal(float amount);
}
