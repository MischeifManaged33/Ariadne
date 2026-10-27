using System.Collections;
using UnityEngine;

// Swipe, consecutive swipes 
public class MantacoreSwipe : EnemyAbility
{
    [Header("Swipe")]
    [SerializeField, Min(1)]
    private int swipes = 2;
    [SerializeField, Min(0.1f)]
    private float reach = 1.8f;
    [SerializeField, Range(10f, 360f)]
    private float arc = 110f;
    [SerializeField, Min(0f)]
    private float damage = 10f;
    [SerializeField]
    private LayerMask targetLayers;

    [Header("Step")]
    [SerializeField, Min(0f)]
    private float maxStep = 0.8f;
    [SerializeField, Min(0.01f)]
    private float stepDuration = 0.12f;
    [SerializeField, Range(0f, 1f)]
    private float stepDepth = 0.6f;

    [Header("Windup")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.55f;
    [SerializeField, Min(0.05f)]
    private float followupWindup = 0.3f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.7f;

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    private float betweenSwipes = 0.15f;
    [SerializeField, Min(0f)]
    private float recovery = 0.8f;

    [Header("Walls")]
    [SerializeField]
    private LayerMask wallLayers;
    [SerializeField, Min(0.05f)]
    private float bodyRadius = 0.3f;
    [SerializeField, Min(0f)]
    private float wallSkin = 0.05f;

    [Header("Telegraph")]
    [SerializeField]
    private AttackIndicator indicator;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private Mantacore _mantacore;
    private AttackIndicator _runtimeIndicator;

    private void Reset()
    {
        Weight = 2f;
        MinRange = 0f;
        MaxRange = 2.4f;
        Cooldown = 1.2f;

        targetLayers = PlayerMask();
        wallLayers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        _mantacore = GetComponentInParent<Mantacore>();

        if (targetLayers.value == 0)
            targetLayers = PlayerMask();
        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Walls");
    }

    private void OnDestroy()
    {
        if (_runtimeIndicator != null)
            Destroy(_runtimeIndicator.gameObject);
    }

    public override bool CanUse(EnemyAI ai, float distanceToTarget)
    {
        return _mantacore != null && _mantacore.IsAlive && base.CanUse(ai, distanceToTarget);
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        var fan = ResolveIndicator();

        for (var i = 0; i < swipes; i++) {
            var duration = i == 0 ? windup : followupWindup;
            var direction = Aim(ai);
            var step = PlanStep(ai, direction);

            sounds.PlayWindup(ai.Position);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime) {
                if (elapsed < duration * trackFraction) {
                    direction = Aim(ai);
                    step = PlanStep(ai, direction);
                }

                ai.FaceTowards(direction);

                fan.Aim(ai.Position, direction, reach + step, arc, ai.YScale);
                fan.SetReady(true);
                fan.SetVisible(true);

                yield return null;
            }

            yield return Swipe(ai, fan, direction, step);

            if (i < swipes - 1 && betweenSwipes > 0f)
                yield return new WaitForSeconds(betweenSwipes);
        }

        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);
    }

    private IEnumerator Swipe(EnemyAI ai, AttackIndicator fan, Vector2 direction, float step)
    {
        var origin = ai.Position;
        var range = reach + step;

        fan.Aim(origin, direction, range, arc, ai.YScale);
        fan.Flash();
        fan.SetVisible(false);

        sounds.PlaySwing(origin);

        if (step > 0f) {
            var end = origin + Isometric.ToScreen(direction * step, ai.YScale);

            for (var elapsed = 0f; elapsed < stepDuration; elapsed += Time.deltaTime) {
                MoveTo(ai, Vector2.Lerp(origin, end, Mathf.Clamp01(elapsed / stepDuration)));
                yield return null;
            }

            MoveTo(ai, end);
        }

        Strike(ai, origin, direction, range);
    }

    private void Strike(EnemyAI ai, Vector2 origin, Vector2 direction, float range)
    {
        var half = arc * 0.5f;
        var candidates = Physics2D.OverlapCircleAll(origin, range, ResolveMask(targetLayers));

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            var contact = candidate.ClosestPoint(origin);
            var relative = Isometric.ToGround(contact - origin, ai.YScale);

            if (relative.sqrMagnitude > range * range)
                continue;
            if (relative.sqrMagnitude > 0.0001f && Vector2.Angle(relative, direction) > half)
                continue;

            health.TakeDamage(damage);
            sounds.PlayImpact(contact);

            return;
        }
    }

    // Only steps in as far as it takes to get the target well inside the fan, and never into a wall
    private float PlanStep(EnemyAI ai, Vector2 direction)
    {
        if (maxStep <= 0f || ai.Target == null)
            return 0f;

        var wanted = Mathf.Clamp(ai.DistanceToTarget - reach * stepDepth, 0f, maxStep);
        if (wanted <= 0f)
            return 0f;

        return Mathf.Min(wanted, ClearLength(ai.Position, direction, wanted, ai.YScale));
    }

    private float ClearLength(Vector2 origin, Vector2 groundDirection, float distance, float yScale)
    {
        if (wallLayers.value == 0)
            return distance;

        var screen = Isometric.ToScreen(groundDirection * distance, yScale);
        var screenLength = screen.magnitude;

        if (screenLength <= 0.0001f)
            return distance;

        var hitInfo = Physics2D.CircleCast(origin, bodyRadius, screen / screenLength, screenLength, wallLayers);
        if (hitInfo.collider == null)
            return distance;

        var clear = Mathf.Max(0f, hitInfo.distance - wallSkin);

        return distance * clear / screenLength;
    }

    private Vector2 Aim(EnemyAI ai)
    {
        var direction = ai.DirectionToTarget;
        return direction != Vector2.zero ? direction : ai.Facing;
    }

    private void MoveTo(EnemyAI ai, Vector2 position)
    {
        if (_mantacore != null)
            _mantacore.SetGroundPosition(position);
        else
            ai.transform.position = new Vector3(position.x, position.y, ai.transform.position.z);
    }

    private AttackIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null) {
            var host = new GameObject("Mantacore Swipe Indicator");
            _runtimeIndicator = host.AddComponent<AttackIndicator>();
        }

        return _runtimeIndicator;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.7f);

        var origin = (Vector2)transform.position;
        var yScale = Isometric.DefaultYScale;

        Vector2 Point(float degrees, float range)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return origin + Isometric.ToScreen(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * range, yScale);
        }

        // Facing right, out to full reach plus the longest step
        var range = reach + maxStep;
        var half = arc * 0.5f;
        var previous = Point(-half, range);

        Gizmos.DrawLine(origin, previous);

        for (var i = 1; i <= 24; i++) {
            var point = Point(-half + arc * i / 24f, range);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        Gizmos.DrawLine(previous, origin);
    }
}
