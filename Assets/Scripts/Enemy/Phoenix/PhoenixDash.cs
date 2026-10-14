using System.Collections;
using UnityEngine;

// Dash w firetrail
public class PhoenixDash : PhoenixMove
{
    [Header("Dash")]
    [SerializeField, Min(0.5f)]
    private float minDistance = 3f;
    [SerializeField, Min(0.5f)]
    private float maxDistance = 7f;
    [SerializeField, Min(0f)]
    private float overshoot = 2f;
    [SerializeField, Min(0.05f)]
    private float dashDuration = 0.3f;
    [SerializeField, Min(0f)]
    private float contactDamage = 12f;
    [SerializeField, Min(0.05f)]
    private float bodyRadius = 0.3f;

    [Header("Prediction")]
    [SerializeField, Range(0f, 1f)]
    private float leadStrength = 1f;
    [SerializeField, Min(0f)]
    private float leadTime = 0.6f;

    [Header("Timing")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.6f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.7f;
    [SerializeField, Min(0f)]
    private float recovery = 0.7f;

    [Header("Fire Trail")]
    [SerializeField, Min(0.1f)]
    private float trailWidth = 0.8f;
    [SerializeField, Range(0.2f, 1f)]
    private float trailSpacing = 0.75f;
    [SerializeField, Min(0f)]
    private float trailDamage = 8f;
    [SerializeField, Min(0.05f)]
    private float trailTickInterval = 0.5f;
    [SerializeField, Min(0f)]
    private float trailLifetime = 3f;
    [SerializeField, Min(0.01f)]
    private float trailFadeTime = 0.5f;
    [SerializeField]
    private Color trailColor = new Color(0.9f, 0.1f, 0.05f, 0.75f);

    [Header("Telegraph")]
    [SerializeField]
    private RectIndicator indicator;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private RectIndicator _runtimeIndicator;
    private Transform _tracked;
    private Rigidbody2D _targetBody;

    protected override void Reset()
    {
        base.Reset();

        MinRange = 0f;
        MaxRange = 9f;
        Cooldown = 2f;
    }

    private void OnDestroy()
    {
        if (_runtimeIndicator != null)
            Destroy(_runtimeIndicator.gameObject);
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        var rect = ResolveIndicator();

        Plan(ai, out var direction, out var length);
        sounds.PlayWindup(ai.Position);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            if (elapsed < windup * trackFraction)
                Plan(ai, out direction, out length);

            ai.FaceTowards(direction);

            rect.Aim(ai.Position, direction, length, trailWidth, elapsed / windup, ai.YScale);
            rect.SetVisible(true);

            yield return null;
        }

        rect.Flash();
        rect.SetVisible(false);

        var start = ai.Position;
        var end = start + Isometric.ToScreen(direction * length, ai.YScale);
        var step = trailWidth * trailSpacing;
        var laid = 0f;
        var hit = false;

        sounds.PlaySwing(start);

        for (var elapsed = 0f; elapsed < dashDuration; elapsed += Time.deltaTime) {
            var t = Mathf.Clamp01(elapsed / dashDuration);
            var position = Vector2.Lerp(start, end, t);

            MoveTo(ai, position);
            laid = LayTrail(ai, start, direction, length * t, laid, step);

            if (!hit && contactDamage > 0f)
                hit = Detonate(position, bodyRadius + 0.2f, contactDamage, ai.YScale, targetLayers, null);

            yield return null;
        }

        MoveTo(ai, end);
        LayTrail(ai, start, direction, length, laid, step);

        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);
    }

    private float LayTrail(EnemyAI ai, Vector2 start, Vector2 direction, float covered, float laid, float step)
    {
        while (laid <= covered) {
            var point = start + Isometric.ToScreen(direction * laid, ai.YScale);

            PhoenixFireTrail.Spawn(point, trailWidth, ai.YScale, trailDamage, trailTickInterval, trailLifetime,
                trailFadeTime, targetLayers, trailColor);

            laid += step;
        }

        return laid;
    }

    private void Plan(EnemyAI ai, out Vector2 direction, out float length)
    {
        var aimPoint = ai.TargetPosition + TargetVelocity(ai) * (leadTime * leadStrength);

        direction = Isometric.GroundDirection(ai.Position, aimPoint, ai.YScale);
        if (direction == Vector2.zero)
            direction = ai.DirectionToTarget != Vector2.zero ? ai.DirectionToTarget : ai.Facing;

        var wanted = Mathf.Clamp(Isometric.GroundDistance(ai.Position, aimPoint, ai.YScale) + overshoot,
            minDistance, Mathf.Max(minDistance, maxDistance));

        length = ClearDistance(ai.Position, direction, wanted, bodyRadius, ai.YScale);
    }

    private Vector2 TargetVelocity(EnemyAI ai)
    {
        if (ai.Target == null)
            return Vector2.zero;

        if (_tracked != ai.Target) {
            _tracked = ai.Target;
            _targetBody = ai.Target.GetComponentInParent<Rigidbody2D>();
        }

        return _targetBody != null ? _targetBody.linearVelocity : Vector2.zero;
    }

    private RectIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null)
            _runtimeIndicator = CreateIndicator<RectIndicator>("Phoenix Dash Indicator");

        return _runtimeIndicator;
    }
}
