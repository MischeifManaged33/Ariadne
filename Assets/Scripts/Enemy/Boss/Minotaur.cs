using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Minotaur : MonoBehaviour, IDamagable
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 1000f;

    [Header("Phases")]
    private BossPhase[] phases = {
    };

    [Header("Target")]
    [SerializeField]
    private Transform target;
    [Tooltip("Delay before the first move is picked")]
    [SerializeField, Min(0f)]
    private float startDelay = 1f;

    [Header("Death")]
    [SerializeField]
    private bool destroyOnDeath = true;
    [SerializeField, Min(0f)]
    private float destroyDelay = 0f;

    [Header("Events")]
    [SerializeField]
    private UnityEvent<float> onDamaged;
    [SerializeField]
    private UnityEvent<int> onPhaseChanged;
    [SerializeField]
    private UnityEvent onDied;

    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action<int> PhaseChanged;
    public event Action Died;

    // IDamagable
    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsAlive => CurrentHealth > 0f;

    public int PhaseIndex { get; private set; }
    public BossPhase CurrentPhase => phases[Mathf.Clamp(PhaseIndex, 0, phases.Length - 1)];
    public BossMove CurrentMove { get; private set; }
    public bool IsTransitioning { get; private set; }
    public Transform Target => target;

    public float DistanceToTarget =>
        target != null ? Vector2.Distance(transform.position, target.position) : Mathf.Infinity;

    public Vector2 DirectionToTarget =>
        target != null ? ((Vector2)(target.position - transform.position)).normalized : Vector2.zero;

    private readonly List<BossMove> _usable = new();
    private readonly List<float> _weights = new();
    private BossMove[] _moves;
    private HitFlash _hitFlash;
    private Coroutine _fightRoutine;
    private Coroutine _moveRoutine;
    private int _pendingPhase;
    private bool _dead;

    private void Awake()
    {
        if (phases == null || phases.Length == 0)
            phases = new[] { new BossPhase() };

        _moves = GetComponentsInChildren<BossMove>(true);
        _hitFlash = HitFlash.GetOrAdd(gameObject);

        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        if (target == null) {
            var player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
                target = player.transform;
        }

        if (target == null)
            Debug.LogWarning($"No target found");

        RaiseHealthChanged();
        onPhaseChanged?.Invoke(PhaseIndex);
        PhaseChanged?.Invoke(PhaseIndex);

        _fightRoutine = StartCoroutine(RunFight());
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= applied;

        _hitFlash.Flash();

        onDamaged?.Invoke(applied);
        Damaged?.Invoke(applied);
        RaiseHealthChanged();

        var gated = ResolvePhase(Normalized);
        if (gated > _pendingPhase)
            _pendingPhase = gated;

        if (!IsAlive)
            Die();

        return applied;
    }

    public float Heal(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return 0f;

        var applied = Mathf.Min(amount, maxHealth - CurrentHealth);
        if (applied <= 0f)
            return 0f;

        CurrentHealth += applied;

        Healed?.Invoke(applied);
        RaiseHealthChanged();

        return applied;
    }

    // transition when gated, otherwise pick a move to run
    private IEnumerator RunFight()
    {
        yield return new WaitForSeconds(startDelay);

        while (IsAlive) {
            if (_pendingPhase != PhaseIndex) {
                yield return EnterPhase(_pendingPhase);
                continue;
            }

            var move = SelectMove();
            if (move == null) {
                yield return null;
                continue;
            }

            yield return RunMove(move);
            yield return new WaitForSeconds(CurrentPhase.MoveInterval);
        }
    }

    private IEnumerator RunMove(BossMove move)
    {
        CurrentMove = move;
        _moveRoutine = StartCoroutine(MoveRoutine(move));

        while (_moveRoutine != null) {
            if (_pendingPhase != PhaseIndex) {
                CancelMove();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator MoveRoutine(BossMove move)
    {
        yield return move.Execute(this);

        move.BeginCooldown();
        CurrentMove = null;
        _moveRoutine = null;
    }

    private void CancelMove()
    {
        if (_moveRoutine != null) {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        if (CurrentMove != null) {
            CurrentMove.Cancel();
            CurrentMove.BeginCooldown();
            CurrentMove = null;
        }
    }

    private IEnumerator EnterPhase(int index)
    {
        PhaseIndex = Mathf.Clamp(index, 0, phases.Length - 1);
        IsTransitioning = true;

        onPhaseChanged?.Invoke(PhaseIndex);
        PhaseChanged?.Invoke(PhaseIndex);

        yield return new WaitForSeconds(CurrentPhase.TransitionDuration);

        IsTransitioning = false;
    }

    // Uses the classic weighted random selection move system
    // Could be smarter
    private BossMove SelectMove()
    {
        _usable.Clear();
        _weights.Clear();

        var distance = DistanceToTarget;
        var total = 0f;

        foreach (var move in _moves) {
            if (move == null || !move.isActiveAndEnabled)
                continue;

            var weight = move.GetWeight(PhaseIndex);
            if (weight <= 0f || !move.CanUse(this, distance))
                continue;

            _usable.Add(move);
            _weights.Add(weight);
            total += weight;
        }

        if (total <= 0f)
            return null;

        var roll = UnityEngine.Random.value * total;
        for (var i = 0; i < _usable.Count; i++) {
            roll -= _weights[i];
            if (roll <= 0f)
                return _usable[i];
        }

        return _usable[_usable.Count - 1];
    }

    private int ResolvePhase(float normalized)
    {
        var index = 0;

        for (var i = 0; i < phases.Length; i++) {
            if (normalized <= phases[i].HealthThreshold)
                index = i;
        }

        return index;
    }

    private void Die()
    {

    }

    private void RaiseHealthChanged() => HealthChanged?.Invoke(CurrentHealth, maxHealth);

    private void OnValidate()
    {
        if (Application.isPlaying)
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
    }
}
