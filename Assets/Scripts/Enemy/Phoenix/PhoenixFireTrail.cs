using UnityEngine;

// Groundhazard for burning
public class PhoenixFireTrail : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private bool randomStartFrame = true;

    private Vector2 _size;
    private float _damage;
    private float _tickInterval;
    private float _fadeTime;
    private float _expiresAt;
    private float _nextTick;
    private LayerMask _targets;
    private Color _color;

    public static PhoenixFireTrail Spawn(PhoenixFireTrail prefab, Vector2 position, float width, float yScale,
        float damage, float tickInterval, float lifetime, float fadeTime, LayerMask targets)
    {
        var trail = prefab != null
            ? Instantiate(prefab, position, Quaternion.identity)
            : new GameObject("Phoenix Fire Trail").AddComponent<PhoenixFireTrail>();

        trail.transform.position = position;

        // Square on the ground, so squashed on screen like everything else
        trail._size = new Vector2(width, width * yScale);
        trail._damage = damage;
        trail._tickInterval = Mathf.Max(0.05f, tickInterval);
        trail._fadeTime = Mathf.Max(0.01f, fadeTime);
        trail._expiresAt = Time.time + lifetime;
        trail._targets = targets;

        if (trail.spriteRenderer == null)
            trail.spriteRenderer = trail.GetComponentInChildren<SpriteRenderer>();
        if (trail.animator == null)
            trail.animator = trail.GetComponentInChildren<Animator>();

        if (trail.spriteRenderer != null)
            trail._color = trail.spriteRenderer.color;

        if (trail.randomStartFrame && trail.animator != null && trail.animator.runtimeAnimatorController != null)
            trail.animator.Play(0, 0, Random.value);

        return trail;
    }

    // Width of the drawn flames, from the tight sprite mesh so the PNG's empty padding doesn't count
    public float ArtWidth
    {
        get {
            var renderer = spriteRenderer != null ? spriteRenderer : GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
                return 0f;

            var vertices = renderer.sprite.vertices;
            if (vertices.Length == 0)
                return 0f;

            float min = vertices[0].x, max = vertices[0].x;
            foreach (var vertex in vertices) {
                min = Mathf.Min(min, vertex.x);
                max = Mathf.Max(max, vertex.x);
            }

            return (max - min) * Mathf.Abs(renderer.transform.lossyScale.x);
        }
    }

    private void Update()
    {
        var remaining = _expiresAt - Time.time;

        if (remaining <= -_fadeTime) {
            Destroy(gameObject);
            return;
        }

        if (remaining < 0f) {
            if (spriteRenderer != null) {
                var faded = _color;
                faded.a *= 1f + remaining / _fadeTime;
                spriteRenderer.color = faded;
            }

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
