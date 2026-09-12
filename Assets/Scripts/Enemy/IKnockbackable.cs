using UnityEngine;
public interface IKnockbackable
{
    void ApplyKnockback(Vector2 direction, float strength = 1f);
}
