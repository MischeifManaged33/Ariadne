using System;
using System.Collections;
using UnityEngine;

// Rock dropped by the pillar
public class BossRock : MonoBehaviour
{
    public enum State
    {
        Falling,
        Grounded,
        Held,
        Flying
    }

    [Serializable]
    public class Settings
    {
        public Sprite sprite;
        public string sortingLayer = "Enemy";
        [Min(0.05f)]
        public float size = 0.8f;
        [Min(0.1f)]
        public float radius = 0.9f;
        [Min(0f)]
        public float damage = 15f;
        [Min(0f)]
        public float dropHeight = 6f;
        [Min(0.05f)]
        public float dropDuration = 0.35f;
        [Range(0f, 1f)]
        public float startFill = 0.1f;
        public LayerMask targetLayers;
        public AttackSounds sounds = new AttackSounds();
    }

    public State Current { get; private set; }
    public bool CanPickUp => Current == State.Grounded;
    public bool IsFlying => Current == State.Flying;
    public Vector2 Position => transform.position;

    private Settings _settings;
    private Transform _visual;
    private SpriteRenderer _renderer;
    private CircleIndicator _indicator;
    private float _yScale;

    public static BossRock Create(Settings settings, Vector2 point, float yScale)
    {
        var host = new GameObject("Boss Rock");
        host.transform.position = new Vector3(point.x, point.y, 0f);

        var rock = host.AddComponent<BossRock>();
        rock.Build(settings, yScale);

        return rock;
    }

    private void Build(Settings settings, float yScale)
    {
        _settings = settings;
        _yScale = yScale;

        var visual = new GameObject("Sprite");
        _visual = visual.transform;
        _visual.SetParent(transform, false);

        _renderer = visual.AddComponent<SpriteRenderer>();
        _renderer.sprite = settings.sprite;
        _renderer.sortingLayerName = settings.sortingLayer;
        _renderer.enabled = false;

        if (settings.sprite != null) {
            var bounds = settings.sprite.bounds.size;
            var largest = Mathf.Max(bounds.x, bounds.y);
            if (largest > 0.0001f)
                _visual.localScale = Vector3.one * (settings.size / largest);
        }

        var indicator = new GameObject("Rock Indicator");
        indicator.transform.SetParent(transform, false);

        _indicator = indicator.AddComponent<CircleIndicator>();
        _indicator.SetSortingLayer(settings.sortingLayer);
    }

    private void OnDestroy()
    {
        // Lets the landing flash play out after the rock itself is gone
        if (gameObject.scene.isLoaded && _indicator != null && _indicator.transform.parent == transform) {
            _indicator.SetVisible(false);
            _indicator.transform.SetParent(null, true);
            Destroy(_indicator.gameObject, 0.3f);
        }
    }

    public void Drop(float warning)
    {
        StopAllCoroutines();
        StartCoroutine(DropRoutine(Mathf.Max(warning, _settings.dropDuration)));
    }

    public void PickUp()
    {
        StopAllCoroutines();

        Current = State.Held;
        _indicator.SetVisible(false);
        _renderer.enabled = true;
        _visual.localPosition = Vector3.zero;
    }

    public void HoldAt(Vector2 point)
    {
        if (Current == State.Held)
            transform.position = new Vector3(point.x, point.y, transform.position.z);
    }

    // Telegraph for a throw that hasn't left the hand yet
    public void Aim(Vector2 point, float radius, float fill)
    {
        _indicator.Aim(point, radius, fill, _yScale);
        _indicator.SetVisible(true);
    }

    public void Lob(Vector2 to, float duration, float arcHeight, float radius, float damage)
    {
        StopAllCoroutines();
        StartCoroutine(LobRoutine(to, Mathf.Max(0.05f, duration), arcHeight, radius, damage));
    }

    public void Discard() => Destroy(gameObject);

    private IEnumerator DropRoutine(float warning)
    {
        Current = State.Falling;

        var point = Position;
        var dropFrom = warning - _settings.dropDuration;

        _settings.sounds.PlayWindup(point);

        for (var elapsed = 0f; elapsed < warning; elapsed += Time.deltaTime) {
            _indicator.Aim(point, _settings.radius, Mathf.Lerp(_settings.startFill, 1f, elapsed / warning), _yScale);
            _indicator.SetVisible(true);

            if (elapsed >= dropFrom) {
                // Accelerates in so it reads as falling, not sliding
                var t = (elapsed - dropFrom) / _settings.dropDuration;
                _renderer.enabled = true;
                _visual.localPosition = new Vector3(0f, _settings.dropHeight * (1f - t * t), 0f);
            }

            yield return null;
        }

        _visual.localPosition = Vector3.zero;
        _renderer.enabled = true;

        Land(point, _settings.radius, _settings.damage);

        Current = State.Grounded;
    }

    private IEnumerator LobRoutine(Vector2 to, float duration, float arcHeight, float radius, float damage)
    {
        Current = State.Flying;

        var from = Position;

        _settings.sounds.PlaySwing(from);

        for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime) {
            var t = elapsed / duration;
            var ground = Vector2.Lerp(from, to, t);

            transform.position = new Vector3(ground.x, ground.y, transform.position.z);
            _visual.localPosition = new Vector3(0f, arcHeight * 4f * t * (1f - t), 0f);

            _indicator.Aim(to, radius, 1f, _yScale);
            _indicator.SetVisible(true);

            yield return null;
        }

        transform.position = new Vector3(to.x, to.y, transform.position.z);
        _visual.localPosition = Vector3.zero;

        Land(to, radius, damage);

        Destroy(gameObject);
    }

    private void Land(Vector2 point, float radius, float damage)
    {
        _indicator.Aim(point, radius, 1f, _yScale);
        _indicator.Flash();
        _indicator.SetVisible(false);

        _settings.sounds.PlayImpact(point);

        var mask = _settings.targetLayers.value != 0 ? _settings.targetLayers.value : Physics2D.AllLayers;
        var candidates = Physics2D.OverlapCircleAll(point, radius, mask);

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            var relative = Isometric.ToGround(candidate.ClosestPoint(point) - point, _yScale);
            if (relative.sqrMagnitude > radius * radius)
                continue;

            health.TakeDamage(damage);
            return;
        }
    }
}
