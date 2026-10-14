using System.Collections;
using UnityEngine;

// Backwards dash for distancing
public class PhoenixBackdash : PhoenixMove
{
    [Header("Backdash")]
    [SerializeField, Min(0.5f)]
    private float distance = 3.5f;
    [SerializeField, Min(0.05f)]
    private float windup = 0.15f;
    [SerializeField, Min(0.05f)]
    private float dashDuration = 0.25f;
    [SerializeField, Min(0f)]
    private float recovery = 0.2f;
    [SerializeField, Min(0.05f)]
    private float bodyRadius = 0.3f;
    [SerializeField, Min(0f)]
    private float minimumHop = 1f;
    [SerializeField]
    private bool matchEruptionRadius = true;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private static readonly float[] Angles = { 0f, 35f, -35f, 70f, -70f };

    protected override void Reset()
    {
        base.Reset();

        MinRange = 0f;
        MaxRange = 3f;
        Cooldown = 3f;
        Weight = 4f;
        NeedsLineOfSight = false;
    }

    protected override void Awake()
    {
        base.Awake();

        if (!matchEruptionRadius)
            return;

        var eruption = Phoenix != null ? Phoenix.GetComponentInChildren<PhoenixEruption>(true) : null;
        if (eruption != null)
            MaxRange = eruption.Radius;
    }

    public override bool CanUse(EnemyAI ai, float distanceToTarget)
    {
        return base.CanUse(ai, distanceToTarget) && PickEscape(ai, out _, out _);
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        if (!PickEscape(ai, out var direction, out var length))
            yield break;

        ai.FaceTowards(ai.DirectionToTarget);
        sounds.PlayWindup(ai.Position);

        yield return new WaitForSeconds(windup);

        var start = ai.Position;
        var end = start + Isometric.ToScreen(direction * length, ai.YScale);

        sounds.PlaySwing(start);

        for (var elapsed = 0f; elapsed < dashDuration; elapsed += Time.deltaTime) {
            var t = Mathf.Clamp01(elapsed / dashDuration);
            MoveTo(ai, Vector2.Lerp(start, end, 1f - (1f - t) * (1f - t)));

            ai.FaceTowards(ai.DirectionToTarget);
            yield return null;
        }

        MoveTo(ai, end);

        yield return new WaitForSeconds(recovery);
    }

    private bool PickEscape(EnemyAI ai, out Vector2 direction, out float length)
    {
        var away = -ai.DirectionToTarget;
        if (away == Vector2.zero)
            away = -ai.Facing;

        direction = away;
        length = 0f;

        foreach (var angle in Angles) {
            var candidate = Rotate(away, angle).normalized;
            var clear = ClearDistance(ai.Position, candidate, distance, bodyRadius, ai.YScale);

            if (clear <= length)
                continue;

            direction = candidate;
            length = clear;

            if (clear >= distance)
                break;
        }

        return length >= minimumHop;
    }
}
