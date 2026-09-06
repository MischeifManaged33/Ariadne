using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerHealth health;
    // Can be either a fill or a slider
    [SerializeField]
    private Image fillImage;
    [SerializeField]
    private Slider slider;
    [SerializeField]
    private Image trailImage;

    [Header("Easing")]
    [SerializeField, Min(0f)]
    private float fillSpeed = 14f;
    [SerializeField, Min(0f)]
    private float trailDelay = 0.4f;
    [SerializeField, Min(0f)]
    private float trailSpeed = 4f;

    [Header("Color")]
    [SerializeField]
    private bool tintByHealth = true;
    [SerializeField]
    private Gradient healthGradient;

    [Header("Punch")]
    [SerializeField, Min(0f)]
    private float damagePunch = 0.08f;
    [SerializeField, Min(0.01f)]
    private float punchDuration = 0.18f;

    private RectTransform _root;
    private Vector3 _baseScale;
    private float _target = 1f;
    private float _displayed = 1f;
    private float _trail = 1f;
    private float _trailHoldUntil;
    private float _punchRemaining;

    private void Reset()
    {
        fillImage = GetComponentInChildren<Image>();
        slider = GetComponentInChildren<Slider>();
        healthGradient = BuildDefaultGradient();
    }

    private void Awake()
    {
        _root = (RectTransform)transform;
        _baseScale = _root.localScale;

        if (healthGradient == null || healthGradient.colorKeys.Length == 0)
            healthGradient = BuildDefaultGradient();
    }

    private void OnEnable()
    {
        if (health == null)
            health = FindAnyObjectByType<PlayerHealth>();

        health.HealthChanged += OnHealthChanged;
        health.Damaged += OnDamaged;

        SnapTo(health.CurrentHealth, health.MaxHealth);
    }

    private void OnDisable()
    {
        if (health == null)
            return;

        health.HealthChanged -= OnHealthChanged;
        health.Damaged -= OnDamaged;
    }

    private void Update()
    {
        var dt = Time.unscaledDeltaTime;

        _displayed = Ease(_displayed, _target, fillSpeed, dt);

        // Chasing
        if (_trail < _target)
            _trail = _displayed;
        else if (Time.unscaledTime >= _trailHoldUntil)
            _trail = Ease(_trail, _displayed, trailSpeed, dt);

        ApplyFill(_displayed);

        if (trailImage != null)
            trailImage.fillAmount = _trail;

        UpdatePunch(dt);
    }

    private void OnHealthChanged(float current, float max)
    {
        _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void OnDamaged(float amount)
    {
        _trailHoldUntil = Time.unscaledTime + trailDelay;

        if (damagePunch > 0f)
            _punchRemaining = punchDuration;
    }

    // When the damage don't need easing
    private void SnapTo(float current, float max)
    {
        OnHealthChanged(current, max);

        _displayed = _target;
        _trail = _target;
        _trailHoldUntil = 0f;

        ApplyFill(_displayed);
        if (trailImage != null)
            trailImage.fillAmount = _trail;
    }

    private void ApplyFill(float normalized)
    {
        if (fillImage != null) {
            fillImage.fillAmount = normalized;

            if (tintByHealth && healthGradient != null)
                fillImage.color = healthGradient.Evaluate(normalized);
        }

        if (slider != null)
            slider.normalizedValue = normalized;
    }

    // Easing of punch
    private void UpdatePunch(float dt)
    {
        if (_punchRemaining <= 0f)
            return;

        _punchRemaining = Mathf.Max(0f, _punchRemaining - dt);

        var t = _punchRemaining / punchDuration;
        var scale = 1f + damagePunch * Mathf.Sin(t * Mathf.PI);
        _root.localScale = _baseScale * scale;

        if (_punchRemaining <= 0f)
            _root.localScale = _baseScale;
    }

    private static float Ease(float from, float to, float speed, float dt)
    {
        if (speed <= 0f || dt <= 0f)
            return to;

        return Mathf.Lerp(from, to, 1f - Mathf.Exp(-speed * dt));
    }

    private static Gradient BuildDefaultGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.85f, 0.18f, 0.18f), 0f),
                new GradientColorKey(new Color(0.95f, 0.72f, 0.20f), 0.4f),
                new GradientColorKey(new Color(0.35f, 0.80f, 0.35f), 0.75f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }
}
