                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       using System.Collections;
using UnityEngine;

public class HarpyDive : EnemyAbility
{
    [SerializeField, Min(0.05f)]
    private float ascendDuration = 0.6f;

    [Header("Circle")]
    [SerializeField, Min(0.1f)]
    private float markDuration = 1.8f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.7f;
    [SerializeField, Min(0.2f)]
    private float markRadius = 1.6f;
    [SerializeField, Range(0f, 1f)]
    private float startFill = 0.12f;
    [SerializeField, Min(0f)]
    private float hoverSpeed = 4f;

    [Header("Attack")]
    [SerializeField, Min(0.05f)]
    private float diveDuration = 0.22f;
    [SerializeField, Min(0f)]
    private float damage = 20f;
    [SerializeField]
    private LayerMask targetLayers;

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    private float recovery = 1.6f;

    [Header("Telegraph")]
    [SerializeField]
    private CircleIndicator indicator;

    private Harpy _harpy;
    private CircleIndicator _runtimeIndicator;
    private WaitForSeconds _recoveryWait;
    private float _waitFor = -1f;

    private void Reset()
    {
        MinRange = 0f;
        MaxRange = 9f;
        Cooldown = 1.25f;

        targetLayers = PlayerMask();
    }

    private void Awake()
    {
        _harpy = GetComponentInParent<Harpy>();

        if (targetLayers.value == 0)
            targetLayers = PlayerMask();
    }

    private void OnDestroy()
    {
        if (_runtimeIndicator != null)
            Destroy(_runtimeIndicator.gameObject);
    }

    public override bool CanUse(EnemyAI ai, float distanceToTarget)
    {
        return _harpy != null && _harpy.IsAlive && base.CanUse(ai, distanceToTarget);
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        var circle = ResolveIndicator();

        for (var elapsed = 0f; elapsed < ascendDuration; elapsed += Time.deltaTime) {
            SetFlight(elapsed / ascendDuration);
            ai.FaceTowards(ai.DirectionToTarget);
            yield return null;
        }

        SetFlight(1f);

        var mark = ai.Target != null ? ai.TargetPosition : ai.Position;
        var trackUntil = markDuration * trackFraction;

        for (var elapsed = 0f; elapsed < markDuration; elapsed += Time.deltaTime) {
            if (elapsed < trackUntil && ai.Target != null)
                mark = ai.TargetPosition;

            var fill = Mathf.Lerp(startFill, 1f, elapsed / markDuration);

            circle.Aim(mark, markRadius, fill, ai.YScale);
            circle.SetVisible(true);

            Hover(ai, mark);
            ai.FaceTowards(Isometric.GroundDirection(ai.Position, mark, ai.YScale));

            yield return null;
        }

        circle.Aim(mark, markRadius, 1f, ai.YScale);

        // Drop
        var from = ai.Position;

        for (var elapsed = 0f; elapsed < diveDuration; elapsed += Time.deltaTime) {
            var t = Mathf.Clamp01(elapsed / diveDuration);

            MoveTo(ai, Vector2.Lerp(from, mark, t));
            SetFlight(1f - t);

            yield return null;
        }

        MoveTo(ai, mark);
        SetFlight(0f);

        circle.Flash();
        circle.SetVisible(false);

        Strike(ai, mark);

        // Grounded and open
        yield return RecoveryWait();
    }

    private WaitForSeconds RecoveryWait()
    {
        if (_recoveryWait == null || !Mathf.Approximately(_waitFor, recovery)) {
            _waitFor = recovery;
            _recoveryWait = new WaitForSeconds(recovery);
        }

        return _recoveryWait;
    }

    public override void Cancel()
    {
        SetFlight(0f);

        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);
    }

    private void Hover(EnemyAI ai, Vector2 mark)
    {
        if (hoverSpeed <= 0f)
            return;

        MoveTo(ai, Vector2.MoveTowards(ai.Position, mark, hoverSpeed * Time.deltaTime));
    }

    private void Strike(EnemyAI ai, Vector2 mark)
    {
        var candidates = Physics2D.OverlapCircleAll(mark, markRadius, ResolveMask(targetLayers));

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            var relative = Isometric.ToGround(candidate.ClosestPoint(mark) - mark, ai.YScale);
            if (relative.sqrMagnitude > markRadius * markRadius)
                continue;

            health.TakeDamage(damage);
            return;
        }
    }

    private void SetFlight(float progress)
    {
        if (_harpy != null)
            _harpy.SetFlight(progress);
    }

    private void MoveTo(EnemyAI ai, Vector2 position)
    {
        if (_harpy != null)
            _harpy.SetGroundPosition(position);
        else
            ai.transform.position = new Vector3(position.x, position.y, ai.transform.position.z);
    }

    private CircleIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null) {
            var host = new GameObject("Harpy Dive Indicator");
            _runtimeIndicator = host.AddComponent<CircleIndicator>();
        }

        return _runtimeIndicator;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.7f);

        var centre = transform.position;
        var yScale = Isometric.DefaultYScale;
        var previous = centre + new Vector3(markRadius, 0f, 0f);

        for (var i = 1; i <= 32; i++) {
            var radians = Mathf.PI * 2f * i / 32f;
            var point = centre + new Vector3(
                Mathf.Cos(radians) * markRadius,
                Mathf.Sin(radians) * markRadius * yScale,
                0f);

            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
