using UnityEngine;

// Explosive feathers
public class PhoenixFireball : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private bool faceTravel = true;
    [SerializeField, Range(-180f, 180f)]
    private float artAngle = -135f;
    [SerializeField, Min(1f)]
    private float sortingPrecision = 16f;

    private Vector2 _start;
    private Vector2 _end;
    private float _groundLength;
    private float _speed;
    private float _travelled;

    private float _hitRadius;
    private float _blastRadius;
    private float _damage;
    private float _yScale;
    private LayerMask _targets;
    private AttackSounds _sounds;

    private CircleIndicator _blast;
    private bool _exploded;

    public static PhoenixFireball Launch(PhoenixFireball prefab, Vector2 start, Vector2 end, float speed,
        float hitRadius, float blastRadius, float damage, float yScale, LayerMask targets, AttackSounds sounds)
    {
        var fireball = prefab != null
            ? Instantiate(prefab, start, Quaternion.identity)
            : new GameObject("Phoenix Fireball").AddComponent<PhoenixFireball>();

        fireball.transform.position = start;

        fireball._start = start;
        fireball._end = end;
        fireball._groundLength = Isometric.GroundDistance(start, end, yScale);
        fireball._speed = Mathf.Max(0.1f, speed);
        fireball._hitRadius = hitRadius;
        fireball._blastRadius = blastRadius;
        fireball._damage = damage;
        fireball._yScale = yScale;
        fireball._targets = targets;
        fireball._sounds = sounds;

        if (fireball.spriteRenderer == null)
            fireball.spriteRenderer = fireball.GetComponentInChildren<SpriteRenderer>();

        var travel = end - start;
        if (fireball.faceTravel && travel.sqrMagnitude > 0.0001f)
            fireball.transform.rotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg - fireball.artAngle);

        var blastHost = new GameObject("Phoenix Fireball Blast");
        fireball._blast = blastHost.AddComponent<CircleIndicator>();
        fireball._blast.SetSortingLayer("Enemy");

        return fireball;
    }

    private void Update()
    {
        if (_exploded)
            return;

        _travelled += _speed * Time.deltaTime;

        var t = _groundLength > 0.0001f ? Mathf.Clamp01(_travelled / _groundLength) : 1f;
        var position = Vector2.Lerp(_start, _end, t);

        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-position.y * sortingPrecision);

        _blast.Aim(_end, _blastRadius, t, _yScale);
        _blast.SetVisible(true);

        if (t >= 1f) {
            Explode(_end);
            return;
        }

        if (TouchingTarget(position))
            Explode(position);
    }

    private bool TouchingTarget(Vector2 position)
    {
        var mask = _targets.value != 0 ? _targets.value : Physics2D.AllLayers;

        foreach (var candidate in Physics2D.OverlapCircleAll(position, _hitRadius, mask)) {
            if (candidate.GetComponentInParent<PlayerHealth>() != null)
                return true;
        }

        return false;
    }

    private void Explode(Vector2 centre)
    {
        _exploded = true;

        PhoenixMove.Detonate(centre, _blastRadius, _damage, _yScale, _targets, _sounds);

        _blast.Aim(centre, _blastRadius, 1f, _yScale);
        _blast.Flash();
        _blast.SetVisible(false);

        Destroy(_blast.gameObject, 0.4f);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!_exploded && _blast != null)
            Destroy(_blast.gameObject);
    }
}
