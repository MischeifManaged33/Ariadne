using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Shard : MonoBehaviour
{
    private const float Gravity = 18f;
    private const float GroundFriction = 6f;

    private SpriteRenderer _renderer;
    private Vector2 _groundPosition;
    private Vector2 _velocity;
    private float _height;
    private float _verticalSpeed;
    private float _spin;
    private float _lifetime;
    private float _fadeDuration;
    private float _age;

    public static Shard Spawn(Sprite sprite, Vector2 position, Vector2 velocity, float hop,
        float spin, float lifetime, float fadeDuration, SpriteRenderer sortingSource)
    {
        var go = new GameObject("Shard");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;

        if (sortingSource != null) {
            renderer.sharedMaterial = sortingSource.sharedMaterial;
            renderer.sortingLayerID = sortingSource.sortingLayerID;
            renderer.sortingOrder = sortingSource.sortingOrder;
        }

        var shard = go.AddComponent<Shard>();
        shard._renderer = renderer;
        shard._groundPosition = position;
        shard._velocity = velocity;
        shard._verticalSpeed = hop;
        shard._spin = spin;
        shard._lifetime = Mathf.Max(lifetime, 0.01f);
        shard._fadeDuration = Mathf.Clamp(fadeDuration, 0f, shard._lifetime);
        return shard;
    }

    private void Update()
    {
        var dt = Time.deltaTime;
        _age += dt;

        if (_age >= _lifetime) {
            Destroy(gameObject);
            return;
        }

        _groundPosition += _velocity * dt;

        // Fake height so shards arc up and settle instead of sliding flat
        _verticalSpeed -= Gravity * dt;
        _height += _verticalSpeed * dt;

        if (_height <= 0f) {
            _height = 0f;
            _verticalSpeed = Mathf.Abs(_verticalSpeed) > 1f ? -_verticalSpeed * 0.35f : 0f;
            _velocity = Vector2.MoveTowards(_velocity, Vector2.zero, GroundFriction * dt);
            _spin = Mathf.MoveTowards(_spin, 0f, 720f * dt);
        }

        transform.position = new Vector3(_groundPosition.x, _groundPosition.y + _height, transform.position.z);
        transform.Rotate(0f, 0f, _spin * dt);

        var fadeStart = _lifetime - _fadeDuration;
        if (_age > fadeStart && _fadeDuration > 0f) {
            var color = _renderer.color;
            color.a = 1f - (_age - fadeStart) / _fadeDuration;
            _renderer.color = color;
        }
    }
}
