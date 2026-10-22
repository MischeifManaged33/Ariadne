using UnityEngine;


[CreateAssetMenu(
    fileName = "New Bow",
    menuName = "Ariadne/Bow"
)]
public class BowData : WeaponData
{
    [Header("Arrow")]
    public Arrow arrowPrefab;
    [Min(0f)] public float spawnOffset = 0.4f;

    [Header("Charge")]
    [Min(0.01f)] public float chargeTime = 1f;
    // Eases how quickly the charge builds, 0 to 1 on both axes
    public AnimationCurve chargeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Power (no charge to full charge)")]
    [Min(0f)] public float minDamage = 5f;
    [Min(0.1f)] public float minSpeed = 6f;
    [Min(0.1f)] public float maxSpeed = 16f;
    [Min(0.1f)] public float minDistance = 2f;
    [Min(0.1f)] public float maxDistance = 9f;
    [Range(0f, 2f)] public float minKnockback = 0.25f;
    [Range(0f, 2f)] public float maxKnockback = 1f;

    [Header("Indicator")]
    [Min(0.05f)] public float indicatorWidth = 0.35f;

    public float Charge(float heldSeconds)
    {
        var t = Mathf.Clamp01(heldSeconds / chargeTime);
        return chargeCurve != null && chargeCurve.length > 0 ? Mathf.Clamp01(chargeCurve.Evaluate(t)) : t;
    }

    public float DamageAt(float charge) => Mathf.Lerp(minDamage, damage, charge);
    public float SpeedAt(float charge) => Mathf.Lerp(minSpeed, maxSpeed, charge);
    public float DistanceAt(float charge) => Mathf.Lerp(minDistance, maxDistance, charge);
    public float KnockbackAt(float charge) => Mathf.Lerp(minKnockback, maxKnockback, charge);

    private void OnValidate()
    {
        minDamage = Mathf.Min(minDamage, damage);
        maxSpeed = Mathf.Max(maxSpeed, minSpeed);
        maxDistance = Mathf.Max(maxDistance, minDistance);
    }
}
