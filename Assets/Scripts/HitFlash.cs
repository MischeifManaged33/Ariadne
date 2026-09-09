using UnityEngine;

// Pulls the hit flash logic out of player
// Now applicable anywhere
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private Color flashColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField, Min(0f)]
    private float flashDuration = 0.12f;

    private float _flashUntil;
    private Color _baseColor = Color.white;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            _baseColor = spriteRenderer.color;
    }

    public void Flash() => _flashUntil = Time.time + flashDuration;

    private void Update()
    {
        if (spriteRenderer == null || flashDuration <= 0f)
            return;

        var remaining = _flashUntil - Time.time;
        if (remaining <= 0f) {
            if (spriteRenderer.color != _baseColor)
                spriteRenderer.color = _baseColor;
            return;
        }

        spriteRenderer.color = Color.Lerp(_baseColor, flashColor, remaining / flashDuration);
    }

    public static HitFlash GetOrAdd(GameObject target)
    {
        var flash = target.GetComponent<HitFlash>();
        return flash != null ? flash : target.AddComponent<HitFlash>();
    }
}
