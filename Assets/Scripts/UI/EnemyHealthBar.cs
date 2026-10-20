using UnityEngine;

// Same logic as the other healthbars, but not display on canvas
public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Enemy enemy;
    [SerializeField]
    private SpriteRenderer frameRenderer;
    [SerializeField]
    private SpriteRenderer fillRenderer;
    [SerializeField]
    private SpriteRenderer trailRenderer;

    [Header("Visibility")]
    [SerializeField]
    private bool onlyShowWhenDamaged = false;
    [SerializeField, Min(0f)]
    private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)]
    private float fadeOutDuration = 0.3f;
    [SerializeField, Min(0f)]
    private float hideAfterDeathDelay = 0.5f;

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

    public Enemy Enemy => enemy;
    public bool IsShown => _alpha > 0f;

    private Vector3 _baseScale;
    private Bar _fillBar;
    private Bar _trailBar;
    private Color _frameColor = Color.white;
    private Color _fillColor = Color.white;
    private Color _trailColor = Color.white;

    private float _target = 1f;
    private float _displayed = 1f;
    private float _trail = 1f;
    private float _trailHoldUntil;
    private float _punchRemaining;

    private float _alpha = -1f;
    private float _hideAtTime = Mathf.Infinity;
    private bool _hiddenByDeath;
    private bool _started;

    private void Reset()
    {
        enemy = GetComponentInParent<Enemy>();
        healthGradient = BuildDefaultGradient();
    }

    private void Awake()
    {
        _baseScale = transform.localScale;

        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();

        if (healthGradient == null || healthGradient.colorKeys.Length == 0)
            healthGradient = BuildDefaultGradient();

        _fillBar = new Bar(fillRenderer);
        _trailBar = new Bar(trailRenderer);

        if (frameRenderer != null)
            _frameColor = frameRenderer.color;
        if (fillRenderer != null)
            _fillColor = fillRenderer.color;
        if (trailRenderer != null)
            _trailColor = trailRenderer.color;

        SetAlpha(0f);
    }

    private void OnEnable()
    {
        if (enemy == null)
            return;

        enemy.HealthChanged += OnHealthChanged;
        enemy.Damaged += OnDamaged;
        enemy.Died += OnDied;

        if (_started)
            SnapTo(enemy.CurrentHealth, enemy.MaxHealth);
    }

    private void Start()
    {
        _started = true;

        if (enemy != null)
            SnapTo(enemy.CurrentHealth, enemy.MaxHealth);
    }

    private void OnDisable()
    {
        if (enemy == null)
            return;

        enemy.HealthChanged -= OnHealthChanged;
        enemy.Damaged -= OnDamaged;
        enemy.Died -= OnDied;
    }

    private void Update()
    {
        var dt = Time.deltaTime;

        if (Time.time >= _hideAtTime) {
            _hiddenByDeath = true;
            _hideAtTime = Mathf.Infinity;
        }

        StepFade(dt);

        _displayed = Ease(_displayed, _target, fillSpeed, dt);

        // Chasing
        if (_trail < _target)
            _trail = _displayed;
        else if (Time.time >= _trailHoldUntil)
            _trail = Ease(_trail, _displayed, trailSpeed, dt);

        ApplyFill(_displayed);
        _trailBar.Set(_trail);

        UpdatePunch(dt);
    }

    private bool WantsVisible =>
        enemy != null && enemy.ShowHealthBar && !_hiddenByDeath
        && (!onlyShowWhenDamaged || _target < 1f || _trail < 1f);

    private void OnHealthChanged(float current, float max)
    {
        _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (current > 0f) {
            _hiddenByDeath = false;
            _hideAtTime = Mathf.Infinity;
        }
    }

    private void OnDamaged(float amount)
    {
        _trailHoldUntil = Time.time + trailDelay;

        if (damagePunch > 0f)
            _punchRemaining = punchDuration;
    }

    private void OnDied()
    {
        if (hideAfterDeathDelay <= 0f)
            _hiddenByDeath = true;
        else
            _hideAtTime = Time.time + hideAfterDeathDelay;
    }

    private void StepFade(float dt)
    {
        var goal = WantsVisible ? 1f : 0f;
        if (Mathf.Approximately(_alpha, goal))
            return;

        var duration = goal > _alpha ? fadeInDuration : fadeOutDuration;
        SetAlpha(duration <= 0f ? goal : Mathf.MoveTowards(_alpha, goal, dt / duration));
    }

    private void SetAlpha(float value)
    {
        _alpha = Mathf.Clamp01(value);
        var visible = _alpha > 0f;

        if (frameRenderer != null) {
            frameRenderer.enabled = visible;
            frameRenderer.color = WithAlpha(_frameColor, _frameColor.a * _alpha);
        }

        if (trailRenderer != null) {
            trailRenderer.enabled = visible;
            trailRenderer.color = WithAlpha(_trailColor, _trailColor.a * _alpha);
        }

        if (fillRenderer != null)
            fillRenderer.enabled = visible;

        ApplyFill(_displayed);
    }

    // When the damage don't need easing
    private void SnapTo(float current, float max)
    {
        OnHealthChanged(current, max);

        _displayed = _target;
        _trail = _target;
        _trailHoldUntil = 0f;

        ApplyFill(_displayed);
        _trailBar.Set(_trail);
    }

    private void ApplyFill(float normalized)
    {
        _fillBar.Set(normalized);

        if (fillRenderer == null)
            return;

        var color = tintByHealth && healthGradient != null ? healthGradient.Evaluate(normalized) : _fillColor;
        fillRenderer.color = WithAlpha(color, color.a * _alpha);
    }

    // Easing of punch
    private void UpdatePunch(float dt)
    {
        if (_punchRemaining <= 0f)
            return;

        _punchRemaining = Mathf.Max(0f, _punchRemaining - dt);

        var t = _punchRemaining / punchDuration;
        var scale = 1f + damagePunch * Mathf.Sin(t * Mathf.PI);
        transform.localScale = _baseScale * scale;

        if (_punchRemaining <= 0f)
            transform.localScale = _baseScale;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
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

    private readonly struct Bar
    {
        private readonly Transform _transform;
        private readonly Vector3 _basePosition;
        private readonly Vector3 _baseScale;
        private readonly float _leftEdge;

        public Bar(SpriteRenderer renderer)
        {
            _transform = renderer != null ? renderer.transform : null;
            _basePosition = _transform != null ? _transform.localPosition : Vector3.zero;
            _baseScale = _transform != null ? _transform.localScale : Vector3.one;
            _leftEdge = renderer != null && renderer.sprite != null ? renderer.sprite.bounds.min.x : 0f;
        }

        public void Set(float normalized)
        {
            if (_transform == null)
                return;

            normalized = Mathf.Clamp01(normalized);

            var scale = _baseScale;
            scale.x *= normalized;
            _transform.localScale = scale;

            var position = _basePosition;
            position.x += _leftEdge * _baseScale.x * (1f - normalized);
            _transform.localPosition = position;
        }
    }
}
