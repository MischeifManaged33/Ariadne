using UnityEngine;
using UnityEngine.UI;

// Same bar as the player's, except hides until the minotaur has aggro
[RequireComponent(typeof(CanvasGroup))]
public class BossHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Minotaur boss;
    [SerializeField]
    private CanvasGroup group;
    // Can be either a fill or a slider
    [SerializeField]
    private Image fillImage;
    [SerializeField]
    private Slider slider;
    [SerializeField]
    private Image trailImage;

    [Header("Binding")]
    [SerializeField]
    private bool findBossAutomatically = true;
    [SerializeField, Min(0.05f)]
    private float searchInterval = 0.5f;

    [Header("Reveal")]
    [SerializeField, Min(0f)]
    private float fadeInDuration = 0.35f;
    [SerializeField, Min(0f)]
    private float fadeOutDuration = 0.6f;
    [SerializeField, Min(0f)]
    private float hideAfterDeathDelay = 1.25f;

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

    public Minotaur Boss => boss;
    public bool IsShown => _shown;

    private RectTransform _root;
    private Vector3 _baseScale;
    private float _target = 1f;
    private float _displayed = 1f;
    private float _trail = 1f;
    private float _trailHoldUntil;
    private float _punchRemaining;

    private bool _bound;
    private bool _shown;
    private float _alpha;
    private float _nextSearchTime;
    private float _hideAtTime = Mathf.Infinity;
    private int _revealFrame = -1;

    private void Reset()
    {
        group = GetComponent<CanvasGroup>();
        fillImage = GetComponentInChildren<Image>();
        slider = GetComponentInChildren<Slider>();
        healthGradient = BuildDefaultGradient();
    }

    private void Awake()
    {
        _root = (RectTransform)transform;
        _baseScale = _root.localScale;

        if (group == null)
            group = GetComponent<CanvasGroup>();

        if (healthGradient == null || healthGradient.colorKeys.Length == 0)
            healthGradient = BuildDefaultGradient();
    }

    private void OnEnable()
    {
        Hide(true);
        Bind(boss);
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Update()
    {
        var dt = Time.unscaledDeltaTime;

        if (!_bound) {
            SearchForBoss();
        }
        else if (boss == null) {
            Unbind();

            if (_shown && float.IsPositiveInfinity(_hideAtTime))
                Hide(false);
        }

        if (_shown && Time.unscaledTime >= _hideAtTime)
            Hide(false);

        StepFade(dt);

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

    public void SetBoss(Minotaur value)
    {
        Unbind();
        Hide(true);

        boss = value;
        Bind(value);
    }

    private void SearchForBoss()
    {
        if (!findBossAutomatically || Time.unscaledTime < _nextSearchTime)
            return;

        _nextSearchTime = Time.unscaledTime + searchInterval;

        var found = FindAnyObjectByType<Minotaur>();
        if (found != null)
            Bind(found);
    }

    private void Bind(Minotaur value)
    {
        if (value == null)
            return;

        boss = value;
        _bound = true;

        Hide(false);

        boss.Aggroed += OnAggro;
        boss.HealthChanged += OnHealthChanged;
        boss.Damaged += OnDamaged;
        boss.Died += OnDied;

        if (boss.IsAggro)
            Reveal();
    }

    private void Unbind()
    {
        _bound = false;

        if (boss == null)
            return;

        boss.Aggroed -= OnAggro;
        boss.HealthChanged -= OnHealthChanged;
        boss.Damaged -= OnDamaged;
        boss.Died -= OnDied;
    }

    private void OnAggro() => Reveal();

    private void OnHealthChanged(float current, float max)
    {
        _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void OnDamaged(float amount)
    {
        if (_revealFrame == Time.frameCount && boss != null && boss.MaxHealth > 0f) {
            _displayed = Mathf.Clamp01(_displayed + amount / boss.MaxHealth);
            _trail = _displayed;
        }

        _trailHoldUntil = Time.unscaledTime + trailDelay;

        if (damagePunch > 0f)
            _punchRemaining = punchDuration;
    }

    private void OnDied()
    {
        if (hideAfterDeathDelay <= 0f)
            Hide(false);
        else
            _hideAtTime = Time.unscaledTime + hideAfterDeathDelay;
    }

    private void Reveal()
    {
        if (_shown)
            return;

        _shown = true;
        _revealFrame = Time.frameCount;
        _hideAtTime = Mathf.Infinity;

        if (boss != null)
            SnapTo(boss.CurrentHealth, boss.MaxHealth);

        if (fadeInDuration <= 0f)
            SetAlpha(1f);
    }

    private void Hide(bool immediate)
    {
        _shown = false;
        _hideAtTime = Mathf.Infinity;

        if (immediate || fadeOutDuration <= 0f)
            SetAlpha(0f);
    }

    private void StepFade(float dt)
    {
        var goal = _shown ? 1f : 0f;
        if (Mathf.Approximately(_alpha, goal))
            return;

        var duration = _shown ? fadeInDuration : fadeOutDuration;
        SetAlpha(duration <= 0f ? goal : Mathf.MoveTowards(_alpha, goal, dt / duration));
    }

    private void SetAlpha(float value)
    {
        _alpha = Mathf.Clamp01(value);

        if (group == null)
            return;

        group.alpha = _alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
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
