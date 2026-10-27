using UnityEngine;

// Poison ticks
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerPoison : MonoBehaviour
{
    [SerializeField, Min(0.05f)]
    private float tickInterval = 0.5f;
    [SerializeField]
    private Color tint = new Color(0.55f, 1f, 0.45f);

    public bool IsPoisoned => _remaining > 0f;

    private PlayerHealth _health;
    private float _damagePerSecond;
    private float _remaining;
    private float _untilTick;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
    }

    public void Apply(float damagePerSecond, float duration)
    {
        if (damagePerSecond <= 0f || duration <= 0f)
            return;

        if (!IsPoisoned) {
            _damagePerSecond = damagePerSecond;
            _untilTick = tickInterval;
        } else {
            _damagePerSecond = Mathf.Max(_damagePerSecond, damagePerSecond);
        }

        _remaining = Mathf.Max(_remaining, duration);
        _health.SetStatusTint(tint);
    }

    public void Clear()
    {
        _remaining = 0f;
        _damagePerSecond = 0f;
        _health.SetStatusTint(Color.white);
    }

    private void Update()
    {
        if (!IsPoisoned)
            return;

        var deltaTime = Mathf.Min(Time.deltaTime, _remaining);
        _remaining -= deltaTime;
        _untilTick -= deltaTime;

        if (_untilTick <= 0f) {
            Tick(tickInterval);
            _untilTick += tickInterval;
        }

        if (_remaining > 0f)
            return;

        Tick(tickInterval - _untilTick);
        Clear();
    }

    private void Tick(float seconds)
    {
        if (seconds > 0.0001f)
            _health.TakeTickDamage(_damagePerSecond * seconds);
    }

    public static PlayerPoison GetOrAdd(GameObject target)
    {
        var poison = target.GetComponent<PlayerPoison>();
        return poison != null ? poison : target.AddComponent<PlayerPoison>();
    }
}
