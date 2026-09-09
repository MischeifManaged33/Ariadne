using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    [Header("Damage")]
    [SerializeField, Min(0f)]
    private float invulnerabilityTime = 0.5f;
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private Color hitFlashColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField, Min(0f)]
    private float hitFlashDuration = 0.12f;

    [Header("Events")]
    [SerializeField]
    private UnityEvent<float> onDamaged;
    [SerializeField]
    private UnityEvent<float> onHealed;

    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;

    // Properties
    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsInvulnerable => Time.time < _invulnerableUntil;

    private float _invulnerableUntil;
    private float _flashUntil;
    private Color _baseColor = Color.white;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            _baseColor = spriteRenderer.color;

        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        RaiseHealthChanged();
    }

    private void Update() => UpdateFlash();

    // Applies damage, returning damage delt
    public float TakeDamage(float amount, bool ignoreInvulnerability = false)
    {
        if (amount <= 0f)
            return 0f;
        if (IsInvulnerable && !ignoreInvulnerability)
            return 0f;

        var applied = Mathf.Min(amount, CurrentHealth);
        if (applied <= 0f)
            return 0f;

        CurrentHealth -= applied;

        if (CurrentHealth <= 0)
        {
            SceneManager.LoadScene(4);
        }

        _invulnerableUntil = Time.time + invulnerabilityTime;
        _flashUntil = Time.time + hitFlashDuration;

        onDamaged?.Invoke(applied);
        Damaged?.Invoke(applied);
        RaiseHealthChanged();

        return applied;
    }

    // Restores health, returning health restored
    public float Heal(float amount)
    {
        if (amount <= 0f)
            return 0f;

        var applied = Mathf.Min(amount, maxHealth - CurrentHealth);
        if (applied <= 0f)
            return 0f;

        CurrentHealth += applied;

        onHealed?.Invoke(applied);
        Healed?.Invoke(applied);
        RaiseHealthChanged();

        return applied;
    }

    // wrappers for testing, delete later
    public void ApplyDamage(float amount) => TakeDamage(amount);

    public void ApplyHeal(float amount) => Heal(amount);

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        _invulnerableUntil = 0f;
        RaiseHealthChanged();
    }

    public void SetMaxHealth(float value, bool healToFull = false)
    {
        maxHealth = Mathf.Max(1f, value);
        CurrentHealth = healToFull ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
        RaiseHealthChanged();
    }

    public void InvulnerbilityWindow(float seconds)
    {
        _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + Mathf.Max(0f, seconds));
    }

    // Flashing the sprite on hit
    private void UpdateFlash()
    {
        if (spriteRenderer == null || hitFlashDuration <= 0f)
            return;

        var remaining = _flashUntil - Time.time;
        if (remaining <= 0f) {
            if (spriteRenderer.color != _baseColor)
                spriteRenderer.color = _baseColor;
            return;
        }

        spriteRenderer.color = Color.Lerp(_baseColor, hitFlashColor, remaining / hitFlashDuration);
    }

    private void RaiseHealthChanged() => HealthChanged?.Invoke(CurrentHealth, maxHealth);

    private void OnValidate()
    {

        if (Application.isPlaying)
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
    }
}
