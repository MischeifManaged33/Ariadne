using UnityEngine;

// Pulls the hit flash logic out of player
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
        spriteRenderer = FindSprite();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = FindSprite();
        if (spriteRenderer != null)
            _baseColor = spriteRenderer.color;
    }

    public void Flash() => _flashUntil = Time.time + flashDuration;

    public void SetBaseAlpha(float alpha)
    {
        if (Mathf.Approximately(_baseColor.a, alpha))
            return;

        _baseColor.a = alpha;

        if (spriteRenderer != null && Time.time >= _flashUntil)
            spriteRenderer.color = _baseColor;
    }

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

    // Special case for health bars
    private SpriteRenderer FindSprite()
    {
        foreach (var candidate in GetComponentsInChildren<SpriteRenderer>()) {
            if (candidate.GetComponentInParent<EnemyHealthBar>() == null)
                return candidate;
        }

        return null;
    }

    public static HitFlash GetOrAdd(GameObject target)
    {
        var flash = target.GetComponent<HitFlash>();
        return flash != null ? flash : target.AddComponent<HitFlash>();
    }
}
