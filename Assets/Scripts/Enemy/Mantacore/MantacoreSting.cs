using System.Collections;
using UnityEngine;

// Sting, tracking aim w poision DoT
public class MantacoreSting : EnemyAbility
{
    [Header("Sting")]
    [SerializeField, Min(0.5f)]
    private float length = 3.2f;
    [SerializeField, Min(0.1f)]
    private float width = 0.6f;
    [SerializeField, Min(0f)]
    private float damage = 8f;
    [SerializeField]
    private LayerMask targetLayers;

    [Header("Poison")]
    [SerializeField, Min(0f)]
    private float poisonPerSecond = 3f;
    [SerializeField, Min(0f)]
    private float poisonDuration = 4f;

    [Header("Windup")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.9f;
    [SerializeField, Min(0f)]
    private float turnSpeed = 60f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.8f;
    [SerializeField, Range(0f, 1f)]
    private float startFill = 0.1f;

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    private float recovery = 0.9f;

    [Header("Walls")]
    [SerializeField]
    private LayerMask wallLayers;
    [SerializeField, Min(0f)]
    private float wallSkin = 0.05f;

    [Header("Telegraph")]
    [SerializeField]
    private RectIndicator indicator;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private Mantacore _mantacore;
    private RectIndicator _runtimeIndicator;

    private void Reset()
    {
        Weight = 1f;
        MinRange = 0f;
        MaxRange = 3.5f;
        Cooldown = 2.5f;

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
        var rect = ResolveIndicator();
        var direction = Aim(ai);
        var reach = ClearLength(ai.Position, direction, ai.YScale);

        sounds.PlayWindup(ai.Position);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            if (elapsed < windup * trackFraction) {
                var turn = turnSpeed * Mathf.Deg2Rad * Time.deltaTime;
                direction = ((Vector2)Vector3.RotateTowards(direction, Aim(ai), turn, 0f)).normalized;
                reach = ClearLength(ai.Position, direction, ai.YScale);
            }

            ai.FaceTowards(direction);

            rect.Aim(ai.Position, direction, reach, width, Mathf.Lerp(startFill, 1f, elapsed / windup), ai.YScale);
            rect.SetVisible(true);

            yield return null;
        }

        var origin = ai.Position;

        rect.Aim(origin, direction, reach, width, 1f, ai.YScale);
        rect.Flash();
        rect.SetVisible(false);

        sounds.PlaySwing(origin);

        Strike(ai, origin, direction, reach);

        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);
    }

    private void Strike(EnemyAI ai, Vector2 origin, Vector2 direction, float reach)
    {
        var half = width * 0.5f;
        var middle = origin + Isometric.ToScreen(direction * (reach * 0.5f), ai.YScale);
        var candidates = Physics2D.OverlapCircleAll(middle, reach * 0.5f + width, ResolveMask(targetLayers));

        foreach (var candidate in candidates) {
            var health = candidate.GetComponentInParent<PlayerHealth>();
            if (health == null)
                continue;

            // Measured on the ground, against the rectangle that was telegraphed
            var contact = candidate.ClosestPoint(middle);
            var relative = Isometric.ToGround(contact - origin, ai.YScale);

            var along = Vector2.Dot(relative, direction);
            var across = Mathf.Abs(direction.x * relative.y - direction.y * relative.x);

            if (along < -half || along > reach + half || across > half)
                continue;

            // Dodged through i-frames means no poison either
            if (health.TakeDamage(damage) > 0f)
                PlayerPoison.GetOrAdd(health.gameObject).Apply(poisonPerSecond, poisonDuration);

            sounds.PlayImpact(contact);

            return;
        }
    }

    private Vector2 Aim(EnemyAI ai)
    {
        var direction = ai.DirectionToTarget;
        return direction != Vector2.zero ? direction : ai.Facing;
    }

    private float ClearLength(Vector2 origin, Vector2 groundDirection, float yScale)
    {
        if (wallLayers.value == 0)
            return length;

        var screen = Isometric.ToScreen(groundDirection * length, yScale);
        var screenLength = screen.magnitude;

        if (screenLength <= 0.0001f)
            return length;

        var hitInfo = Physics2D.Raycast(origin, screen / screenLength, screenLength, wallLayers);
        if (hitInfo.collider == null)
            return length;

        var clear = Mathf.Max(0f, hitInfo.distance - wallSkin);

        return length * clear / screenLength;
    }

    private RectIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null) {
            var host = new GameObject("Mantacore Sting Indicator");
            _runtimeIndicator = host.AddComponent<RectIndicator>();
        }

        return _runtimeIndicator;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 1f, 0.35f, 0.7f);

        var origin = (Vector2)transform.position;
        var yScale = Isometric.DefaultYScale;
        var side = new Vector2(0f, width * 0.5f);
        var along = new Vector2(length, 0f);

        Vector2 Corner(Vector2 ground) => origin + Isometric.ToScreen(ground, yScale);

        Gizmos.DrawLine(Corner(side), Corner(along + side));
        Gizmos.DrawLine(Corner(-side), Corner(along - side));
        Gizmos.DrawLine(Corner(side), Corner(-side));
        Gizmos.DrawLine(Corner(along + side), Corner(along - side));
    }
}
