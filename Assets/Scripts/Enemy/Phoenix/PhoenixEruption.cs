using System.Collections;
using UnityEngine;

// Big explosion for opener and death
public class PhoenixEruption : PhoenixMove
{
    [Header("Eruption")]
    [SerializeField, Min(0.2f)]
    private float radius = 3f;
    [SerializeField, Min(0f)]
    private float damage = 20f;
    [SerializeField, Min(0.05f)]
    private float windup = 0.9f;
    [SerializeField, Range(0f, 1f)]
    private float startFill = 0.1f;
    [SerializeField, Min(0f)]
    private float recovery = 0.5f;

    [Header("Telegraph")]
    [SerializeField]
    private CircleIndicator indicator;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    public float Radius => radius;

    private CircleIndicator _runtimeIndicator;

    protected override void Reset()
    {
        base.Reset();

        MinRange = 0f;
        MaxRange = 100f;
        Cooldown = 0f;
        Weight = 100f;
        NeedsLineOfSight = false;
    }

    private void OnDestroy()
    {
        if (_runtimeIndicator != null)
            Destroy(_runtimeIndicator.gameObject);
    }

    // Only ever once, and before anything else
    public override bool CanUse(EnemyAI ai, float distanceToTarget)
    {
        return Phoenix != null && Phoenix.IsAlive && !Phoenix.HasErupted && !IsOnCooldown;
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        Phoenix.Reveal();

        yield return Erupt(ai);

        Phoenix.MarkErupted();

        yield return new WaitForSeconds(recovery);
    }

    // The circle fills, then it goes off. Also how the phoenix goes out when it dies
    public IEnumerator Erupt(EnemyAI ai)
    {
        var circle = ResolveIndicator();

        sounds.PlayWindup(ai.Position);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            ai.FaceTowards(ai.DirectionToTarget);

            circle.Aim(ai.Position, radius, Mathf.Lerp(startFill, 1f, elapsed / windup), ai.YScale);
            circle.SetVisible(true);

            yield return null;
        }

        circle.Aim(ai.Position, radius, 1f, ai.YScale);
        circle.Flash();
        circle.SetVisible(false);

        Detonate(ai.Position, radius, damage, ai.YScale, targetLayers, sounds);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);

        // Interrupted mid-eruption still counts, so the fight carries on rather than restarting
        if (Phoenix != null)
            Phoenix.MarkErupted();
    }

    private CircleIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null)
            _runtimeIndicator = CreateIndicator<CircleIndicator>("Phoenix Eruption Indicator");

        return _runtimeIndicator;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.8f);

        var centre = transform.position;
        var previous = centre + new Vector3(radius, 0f, 0f);

        for (var i = 1; i <= 32; i++) {
            var radians = Mathf.PI * 2f * i / 32f;
            var point = centre + new Vector3(Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius * Isometric.DefaultYScale, 0f);

            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
