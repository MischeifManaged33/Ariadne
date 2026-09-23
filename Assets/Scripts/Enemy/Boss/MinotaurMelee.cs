using System.Collections;
using UnityEngine;


public abstract class MinotaurMelee : BossMove
{
    [Header("Damage")]
    [SerializeField, Min(0f)]
    protected float damage = 18f;
    [SerializeField, Range(5f, 360f)]
    protected float arc = 120f;
    [SerializeField, Min(0f)]
    protected float windup = 0.45f;
    [SerializeField, Min(0f)]
    protected float recovery = 0.35f;
    [SerializeField]
    protected LayerMask targetLayers;
    [SerializeField]
    protected Transform pivot;
    [SerializeField]
    protected AttackIndicator indicator;

    [Header("Audio")]
    [SerializeField]
    protected AttackSounds sounds = new AttackSounds();

    [Header("Approach")]
    [SerializeField, Min(0f)]
    protected float approachTimeout = 4f;
    [SerializeField, Range(0.1f, 3f)]
    protected float approachSpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 1f)]
    protected float approachTightness = 0.7f;

    [Header("Melee Chance")]
    [SerializeField, Range(0f, 0.99f)]
    private float nearChance = 0.9f;
    [SerializeField, Range(0f, 0.99f)]
    private float farChance = 0.7f;
    [SerializeField, Min(0.1f)]
    private float chanceFalloff = 8f;

    public float Reach => MaxRange;

    protected AxeThrow AxeMove { get; private set; }

    protected bool HasAxe => AxeMove == null || AxeMove.HasAxeInHand;

    protected abstract bool IsAvailable { get; }

    protected virtual void Reset()
    {
        MinRange = 0f;
        MaxRange = 2.5f;
        Cooldown = 2f;

        targetLayers = PlayerMask();
    }

    protected virtual void Awake()
    {
        var boss = GetComponentInParent<Minotaur>();

        AxeMove = boss != null
            ? boss.GetComponentInChildren<AxeThrow>(true)
            : GetComponentInChildren<AxeThrow>(true);
    }

    public override bool CanUse(Minotaur boss, float distanceToTarget)
    {
        return !IsOnCooldown && IsAvailable;
    }

    public override float GetSelectionWeight(Minotaur boss, int phaseIndex, float distanceToTarget)
    {
        var weight = GetWeight(phaseIndex);

        if (weight <= 0f || !IsAvailable)
            return 0f;
        if (InRange(distanceToTarget))
            return weight;

        var past = Mathf.Clamp01((distanceToTarget - MaxRange) / chanceFalloff);
        var chance = Mathf.Lerp(nearChance, farChance, past);

        return weight * ChanceToWeight(chance);
    }

    public override IEnumerator Execute(Minotaur boss)
    {
        if (boss.DistanceToTarget > Reach && boss.Mover != null && boss.Target != null)
            yield return boss.Mover.Follow(() => boss.TargetPosition, Reach * approachTightness,
                approachTimeout, approachSpeedMultiplier);

        var direction = boss.DirectionToTarget;
        if (direction == Vector2.zero)
            direction = boss.Facing;

        boss.FaceTowards(direction);

        Telegraph(boss, direction, true);
        sounds.PlayWindup(Origin(boss));
        PlayAttack(boss, windup);

        yield return new WaitForSeconds(windup);

        Strike(boss, direction);

        if (indicator != null)
            indicator.Flash();

        Telegraph(boss, direction, false);
        EndAttack(boss);
        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
    }

    private static float ChanceToWeight(float chance)
    {
        var clamped = Mathf.Clamp(chance, 0f, 0.99f);

        return clamped / Mathf.Max(0.01f, 1f - clamped);
    }

    private void Telegraph(Minotaur boss, Vector2 groundDirection, bool visible)
    {
        if (indicator == null)
            return;

        indicator.Aim(Origin(boss), groundDirection, Reach, arc, boss.YScale);
        indicator.SetReady(true);
        indicator.SetVisible(visible);
    }

    private void Strike(Minotaur boss, Vector2 groundDirection)
    {
        var origin = Origin(boss);
        var half = arc * 0.5f;

        sounds.PlaySwing(origin);

        var candidates = Physics2D.OverlapCircleAll(origin, Reach, ResolveMask(targetLayers));

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            var relative = Isometric.ToGround(candidate.ClosestPoint(origin) - origin, boss.YScale);

            if (relative.sqrMagnitude > Reach * Reach)
                continue;
            if (relative.sqrMagnitude > 0.0001f && Vector2.Angle(relative, groundDirection) > half)
                continue;

            health.TakeDamage(damage);
            sounds.PlayImpact(candidate.ClosestPoint(origin));

            return;
        }
    }

    private Vector2 Origin(Minotaur boss)
    {
        return pivot != null ? (Vector2)pivot.position : boss.Position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(pivot != null ? pivot.position : transform.position, MaxRange);
    }

    private static LayerMask PlayerMask()
    {
        var mask = LayerMask.GetMask("Player");

        return mask != 0 ? mask : LayerMask.GetMask("Default");
    }
}
