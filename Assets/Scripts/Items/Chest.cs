using System.Collections;
using UnityEngine;

// Opens once when interacted with & pops its item out
[RequireComponent(typeof(Collider2D))]
public class Chest : MonoBehaviour, IInteractable
{
    [Header("Visuals")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private Sprite closedSprite;
    [SerializeField]
    private Sprite openSprite;

    [Header("Contents")]
    [SerializeField]
    private GameObject itemPrefab;
    [SerializeField]
    private WeaponData weaponOverride;

    [Header("Pop Out")]
    [SerializeField]
    private Vector2 landingOffset = new(0f, -0.6f);
    [SerializeField, Min(0f)]
    private float popHeight = 0.5f;
    [SerializeField, Min(0.01f)]
    private float popDuration = 0.35f;

    public bool IsOpen { get; private set; }

    public bool CanInteract => isActiveAndEnabled && !IsOpen;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && closedSprite != null)
            spriteRenderer.sprite = closedSprite;
    }

    public void Interact(Player player)
    {
        if (!CanInteract)
            return;

        IsOpen = true;

        if (spriteRenderer != null && openSprite != null)
            spriteRenderer.sprite = openSprite;

        SpawnItem();
    }

    private void SpawnItem()
    {
        if (itemPrefab == null)
            return;

        var item = Instantiate(itemPrefab, transform.position, Quaternion.identity);

        if (weaponOverride != null && item.TryGetComponent<WeaponPickup>(out var pickup))
            pickup.SetWeapon(weaponOverride);

        StartCoroutine(PopOut(item.transform));
    }

    private IEnumerator PopOut(Transform item)
    {
        var colliders = item.GetComponentsInChildren<Collider2D>();
        SetEnabled(colliders, false);

        Vector3 start = transform.position;
        var end = start + (Vector3)landingOffset;

        for (var elapsed = 0f; elapsed < popDuration; elapsed += Time.deltaTime) {
            if (item == null)
                yield break;

            var t = elapsed / popDuration;
            var arc = 4f * popHeight * t * (1f - t);

            item.position = Vector3.Lerp(start, end, t) + Vector3.up * arc;

            yield return null;
        }

        if (item == null)
            yield break;

        item.position = end;
        SetEnabled(colliders, true);
    }

    private static void SetEnabled(Collider2D[] colliders, bool enabled)
    {
        foreach (var collider in colliders)
            collider.enabled = enabled;
    }
}
