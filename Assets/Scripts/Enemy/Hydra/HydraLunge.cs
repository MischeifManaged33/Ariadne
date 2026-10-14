using System.Collections;
using UnityEngine;

// Telegraphs a rectangle towards the player, then charges down it and stays at the far end.
// Later stages chain several lunges per attack, each re-aimed with only a short windup
public class HydraLunge : EnemyAbility
{
    [Header("Lunge")]
    [SerializeField, Min(0.5f)]
    private float lungeDistance = 5f;
    [SerializeField, Min(0.1f)]
    private float lungeWidth = 1.1f;
    [SerializeField, Min(0.05f)]
    private float dashDuration = 0.2f;
    [SerializeField, Min(0f)]
    private float damage = 15f;
    [SerializeField]
    private LayerMask targetLayers;

    [Header("Windup")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.8f;
    // Chained lunges after the first, kept short so they follow almost straight on
    [SerializeField, Min(0.05f)]
    private float followupWindup = 0.3f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.6f;
    [SerializeField, Range(0f, 1f)]
    private float startFill = 0.1f;

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    private float betweenLunges = 0.1f;
    [SerializeField, Min(0f)]
    private float recovery = 1f;

    [Header("Walls")]
    [SerializeField]
    private LayerMask wallLayers;
    [SerializeField, Min(0.05f)]
    private float bodyRadius = 0.3f;
    [SerializeField, Min(0f)]
    private float wallSkin = 0.05f;

    [Header("Telegraph")]
    [SerializeField]
    private RectIndicator indicator;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private Hydra _hydra;
    private RectIndicator _runtimeIndicator;

    private void Reset()
    {
        MinRange = 0f;
        MaxRange = 6f;
        Cooldown = 1.5f;

        targetLayers = PlayerMask();
        wallLayers = LayerMask.GetMask("Walls");
    }

    private void Awake()
    {
        _hydra = GetComponentInParent<Hydra>();

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
        return _hydra != null && _hydra.IsAlive && !_hydra.IsRegrowing && base.CanUse(ai, distanceToTarget);
    }

    public override IEnumerator Execute(EnemyAI ai)
    {
        var rect = ResolveIndicator();
        var lunges = _hydra != null ? _hydra.Lunges : 1;

        for (var i = 0; i < lunges; i++) {
            var duration = i == 0 ? windup : followupWindup;
            var direction = Aim(ai);
            var length = ClearLength(ai.Position, direction, ai.YScale);

            sounds.PlayWindup(ai.Position);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime) {
                if (elapsed < duration * trackFraction) {
                    direction = Aim(ai);
                    length = ClearLength(ai.Position, direction, ai.YScale);
                }

                ai.FaceTowards(direction);

                rect.Aim(ai.Position, direction, length, lungeWidth, Mathf.Lerp(startFill, 1f, elapsed / duration),
                    ai.YScale);
                rect.SetVisible(true);

                yield return null;
            }

            yield return Dash(ai, rect, direction, length);

            if (i < lunges - 1 && betweenLunges > 0f)
                yield return new WaitForSeconds(betweenLunges);
        }

        // Left open at the end of the charge
        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);
    }

    private IEnumerator Dash(EnemyAI ai, RectIndicator rect, Vector2 direction, float length)
    {
        var start = ai.Position;
        var end = start + Isometric.ToScreen(direction * length, ai.YScale);
        var hit = false;

        rect.Aim(start, direction, length, lungeWidth, 1f, ai.YScale);
        rect.Flash();
        rect.SetVisible(false);

        sounds.PlaySwing(start);

        for (var elapsed = 0f; elapsed < dashDuration; elapsed += Time.deltaTime) {
            var t = Mathf.Clamp01(elapsed / dashDuration);

            MoveTo(ai, Vector2.Lerp(start, end, t));

            // Swept: anything the body passed over this lunge gets hit, once
            if (!hit)
                hit = Strike(ai, start, direction, length * t);

            yield return null;
        }

        MoveTo(ai, end);

        if (!hit)
            Strike(ai, start, direction, length);
    }

    private bool Strike(EnemyAI ai, Vector2 start, Vector2 direction, float reached)
    {
        var half = lungeWidth * 0.5f;
        var middle = start + Isometric.ToScreen(direction * (reached * 0.5f), ai.YScale);
        var candidates = Physics2D.OverlapCircleAll(middle, reached * 0.5f + lungeWidth, ResolveMask(targetLayers));

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            // Measured on the ground, against the part of the rectangle the body has covered so far
            var contact = candidate.ClosestPoint(middle);
            var relative = Isometric.ToGround(contact - start, ai.YScale);

            var along = Vector2.Dot(relative, direction);
            var across = Mathf.Abs(direction.x * relative.y - direction.y * relative.x);

            if (along < -half || along > reached + half || across > half)
                continue;

            health.TakeDamage(damage);
            sounds.PlayImpact(contact);

            return true;
        }

        return false;
    }

    private Vector2 Aim(EnemyAI ai)
    {
        var direction = ai.DirectionToTarget;
        return direction != Vector2.zero ? direction : ai.Facing;
    }

    // Shortens the lunge so the charge stops at a wall instead of ending inside it
    private float ClearLength(Vector2 origin, Vector2 groundDirection, float yScale)
    {
        if (wallLayers.value == 0)
            return lungeDistance;

        var screen = Isometric.ToScreen(groundDirection * lungeDistance, yScale);
        var screenLength = screen.magnitude;

        if (screenLength <= 0.0001f)
            return lungeDistance;

        var hitInfo = Physics2D.CircleCast(origin, bodyRadius, screen / screenLength, screenLength, wallLayers);
        if (hitInfo.collider == null)
            return lungeDistance;

        var clear = Mathf.Max(0f, hitInfo.distance - wallSkin);

        return lungeDistance * clear / screenLength;
    }

    private void MoveTo(EnemyAI ai, Vector2 position)
    {
        if (_hydra != null)
            _hydra.SetGroundPosition(position);
        else
            ai.transform.position = new Vector3(position.x, position.y, ai.transform.position.z);
    }

    private RectIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null) {
            var host = new GameObject("Hydra Lunge Indicator");
            _runtimeIndicator = host.AddComponent<RectIndicator>();
        }

        return _runtimeIndicator;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.7f);

        var origin = (Vector2)transform.position;
        var yScale = Isometric.DefaultYScale;
        var side = new Vector2(0f, lungeWidth * 0.5f);
        var along = new Vector2(lungeDistance, 0f);

        Vector2 Corner(Vector2 ground) => origin + Isometric.ToScreen(ground, yScale);

        Gizmos.DrawLine(Corner(side), Corner(along + side));
        Gizmos.DrawLine(Corner(-side), Corner(along - side));
        Gizmos.DrawLine(Corner(side), Corner(-side));
        Gizmos.DrawLine(Corner(along + side), Corner(along - side));
    }
}
