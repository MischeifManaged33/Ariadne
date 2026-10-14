using UnityEngine;

// Groundhazard for burning
public class PhoenixFireTrail : MonoBehaviour
{
    private Vector2 _size;
    private float _damage;
    private float _tickInterval;
    private float _fadeTime;
    private float _expiresAt;
    private float _nextTick;
    private LayerMask _targets;
    private SpriteRenderer _sprite;
    private Color _color;

    public static PhoenixFireTrail Spawn(Vector2 position, float width, float yScale, float damage, float tickInterval,
        float lifetime, float fadeTime, LayerMask targets, Color color)
    {
        var host = new GameObject("Phoenix Fire Trail");
        host.transform.position = position;

        var trail = host.AddComponent<PhoenixFireTrail>();

        // Square on the ground, so squashed on screen like everything else
        trail._size = new Vector2(width, width * yScale);
        trail._damage = damage;
        trail._tickInterval = Mathf.Max(0.05f, tickInterval);
        trail._fadeTime = Mathf.Max(0.01f, fadeTime);
        trail._expiresAt = Time.time + lifetime;
        trail._targets = targets;
        trail._color = color;

        trail._sprite = host.AddComponent<SpriteRenderer>();
        trail._sprite.sprite = PlaceholderSprite.Square;
        trail._sprite.sharedMaterial = PlaceholderSprite.Unlit;
        trail._sprite.color = color;
        trail._sprite.sortingLayerName = "FloorDecor";
        host.transform.localScale = new Vector3(trail._size.x, trail._size.y, 1f);

        return trail;
    }

    private void Update()
    {
        var remaining = _expiresAt - Time.time;

        if (remaining <= -_fadeTime) {
            Destroy(gameObject);
            return;
        }

        // Fading patches have stopped burning
        if (remaining < 0f) {
            var faded = _color;
            faded.a *= 1f + remaining / _fadeTime;
            _sprite.color = faded;
            return;
        }

        if (Time.time >= _nextTick && Burn())
            _nextTick = Time.time + _tickInterval;
    }

    private bool Burn()
    {
        var mask = _targets.value != 0 ? _targets.value : Physics2D.AllLayers;

        foreach (var candidate in Physics2D.OverlapBoxAll(transform.position, _size, 0f, mask)) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            health.TakeDamage(_damage);
            return true;
        }

        return false;
    }
}
