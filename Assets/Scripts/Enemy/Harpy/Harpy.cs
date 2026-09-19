using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class Harpy : Enemy
{
    [Header("Flight")]
    [SerializeField]
    private Transform visual;
    [SerializeField, Min(0f)]
    private float flightHeight = 2.2f;
    [SerializeField, Range(0f, 1f)]
    private float flightAlpha;
    [SerializeField, Range(0.01f, 1f)]
    private float untouchableAbove = 0.15f;



    public float FlightProgress => _flight;
    public bool IsAirborne => _flight > untouchableAbove;
    public bool IsVulnerable => IsAlive && !IsAirborne;

    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;
    private HitFlash _flash;
    private Collider2D _hurtbox;

    private Vector3 _visualHome;
    private bool _canLift;
    private bool _hurtboxOn = true;
    private float _flight = -1f;

    protected override void Awake()
    {
        base.Awake();

        _flash = HitFlash.GetOrAdd(gameObject);
        _hurtbox = GetComponent<Collider2D>();

        CacheVisual();

        SetFlight(0f);
    }

    public void SetFlight(float progress)
    {
        var clamped = Mathf.Clamp01(progress);

        if (Mathf.Approximately(clamped, _flight))
            return;

        _flight = clamped;

        var eased = Mathf.SmoothStep(0f, 1f, _flight);

        if (_canLift)
            visual.localPosition = _visualHome + new Vector3(0f, flightHeight * eased, 0f);

        SetAlpha(Mathf.Lerp(1f, flightAlpha, eased));
        var solid = !IsAirborne;

        if (_hurtbox != null && solid != _hurtboxOn) {
            _hurtbox.enabled = solid;
            _hurtboxOn = solid;
        }
    }

    public void SetGroundPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if (Body != null && Body.bodyType != RigidbodyType2D.Static)
            Body.position = position;
    }

    public override float TakeDamage(float amount)
    {
        return IsVulnerable ? base.TakeDamage(amount) : 0f;
    }

    public override void ApplyKnockback(Vector2 direction, float strength = 1f)
    {
        if (IsAirborne)
            return;

        base.ApplyKnockback(direction, strength);
    }

    protected override void Die()
    {
        SetFlight(0f);

        base.Die();
    }

    private void CacheVisual()
    {
        if (visual == null) {
            var sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null && sprite.transform != transform)
                visual = sprite.transform;
        }

        _canLift = visual != null && visual != transform;

        if (_canLift)
            _visualHome = visual.localPosition;

        var found = GetComponentsInChildren<SpriteRenderer>(true);
        var kept = new List<SpriteRenderer>(found.Length);

        foreach (var candidate in found) {
            if (candidate == null)
                continue;

            kept.Add(candidate);
        }

        _renderers = kept.ToArray();
        _baseColors = new Color[_renderers.Length];

        for (var i = 0; i < _renderers.Length; i++)
            _baseColors[i] = _renderers[i].color;

    }

    private void SetAlpha(float alpha)
    {
        if (_renderers == null)
            return;

        for (var i = 0; i < _renderers.Length; i++) {
            if (_renderers[i] == null)
                continue;

            var color = _baseColors[i];
            color.a *= alpha;

            _renderers[i].color = color;
        }

        if (_flash != null)
            _flash.SetBaseAlpha(alpha);
    }

}
