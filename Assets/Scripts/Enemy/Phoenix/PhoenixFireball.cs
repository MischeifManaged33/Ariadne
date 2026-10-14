using UnityEngine;

// Explosive feathers
public class PhoenixFireball : MonoBehaviour
{
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
    private SpriteRenderer _sprite;
    private bool _exploded;

    public static PhoenixFireball Launch(Vector2 start, Vector2 end, float speed, float hitRadius, float blastRadius,
        float damage, float yScale, LayerMask targets, AttackSounds sounds, Color color, float size)
    {
        var host = new GameObject("Phoenix Fireball");
        host.transform.position = start;

        var fireball = host.AddComponent<PhoenixFireball>();

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

        fireball._sprite = host.AddComponent<SpriteRenderer>();
        fireball._sprite.sprite = PlaceholderSprite.Square;
        fireball._sprite.sharedMaterial = PlaceholderSprite.Unlit;
        fireball._sprite.color = color;
        fireball._sprite.sortingLayerName = "Enemy";
        host.transform.localScale = new Vector3(size, size, 1f);

        var blastHost = new GameObject("Phoenix Fireball Blast");
        fireball._blast = blastHost.AddComponent<CircleIndicator>();
        blastHost.GetComponent<MeshRenderer>().sortingLayerName = "Enemy";

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
        _sprite.sortingOrder = Mathf.RoundToInt(-position.y * 16f);

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

public static class PlaceholderSprite
{
    private static Sprite _square;
    private static Material _unlit;

    public static Sprite Square {
        get {
            if (_square == null)
                _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);

            return _square;
        }
    }

    public static Material Unlit {
        get {
            if (_unlit == null)
                _unlit = IndicatorMaterial.CreateDefault();

            return _unlit;
        }
    }
}
