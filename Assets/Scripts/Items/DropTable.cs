using System;
using UnityEngine;

// Basic weight based droptable
[CreateAssetMenu(fileName = "DropTable", menuName = "Items/Drop Table")]
public class DropTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public GameObject prefab;
        [Range(0f, 1f)]
        public float chance;
    }

    [SerializeField]
    private Entry[] entries;

    public GameObject Roll()
    {
        if (entries == null)
            return null;

        var roll = UnityEngine.Random.value;
        var total = 0f;

        foreach (var entry in entries) {
            total += entry.chance;
            if (roll < total)
                return entry.prefab;
        }

        return null;
    }

    public GameObject RollGuaranteed()
    {
        if (entries == null)
            return null;

        var total = 0f;
        foreach (var entry in entries) {
            if (entry.prefab != null)
                total += entry.chance;
        }

        if (total <= 0f)
            return null;

        var roll = UnityEngine.Random.value * total;

        foreach (var entry in entries) {
            if (entry.prefab == null || entry.chance <= 0f)
                continue;

            roll -= entry.chance;
            if (roll < 0f)
                return entry.prefab;
        }

        for (var i = entries.Length - 1; i >= 0; i--) {
            if (entries[i].prefab != null && entries[i].chance > 0f)
                return entries[i].prefab;
        }

        return null;
    }

    public GameObject Drop(Vector2 position)
    {
        var prefab = Roll();
        return prefab != null ? Instantiate(prefab, position, Quaternion.identity) : null;
    }
}
