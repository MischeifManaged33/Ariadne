using UnityEngine;

[CreateAssetMenu(
    fileName = "New Weapon",
    menuName = "Ariadne/Weapon"
)]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponName;
    public Sprite sprite;

    [Header("Combat")]
    [Min(0f)] public float damage = 20f;
    [Min(0.01f)] public float attackCooldown = 0.5f;
    [Min(0.1f)] public float attackRange = 1.2f;
    [Min(0.1f)] public float hitRadius = 0.5f;
}