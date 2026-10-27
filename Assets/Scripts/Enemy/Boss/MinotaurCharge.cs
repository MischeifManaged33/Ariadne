using System.Collections;
using UnityEngine;

// Charge after telegraph
public class MinotaurCharge : BossMove
{
    private enum Outcome
    {
        Timeout,
        Target,
        Wall,
        Pillar
    }

    [Header("Charge")]
    [SerializeField, Min(0.1f)]
    private float speed = 12f;
    [SerializeField, Min(0.1f)]
    private float maxDuration = 1.6f;
    [SerializeField, Min(0f)]
    private float turnSpeed = 35f;
    [SerializeField, Min(0f)]
    private float damage = 25f;
    [SerializeField, Min(0.05f)]
    private float bodyRadius = 0.5f;
    [SerializeField, Min(0f)]
    private float skin = 0.05f;
    [SerializeField]
    private LayerMask targetLayers;
    [SerializeField]
    private LayerMask obstacleLayers;

    [Header("Windup")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.9f;
    [SerializeField, Range(0f, 1f)]
    private float trackFraction = 0.75f;
    [SerializeField, Range(0f, 1f)]
    private float startFill = 0.1f;

    [Header("Recovery")]
    [SerializeField, Min(0f)]
    private float targetRecovery = 0.5f;
    [SerializeField, Min(0f)]
    private float wallRecovery = 0.8f;
    [SerializeField, Min(0f)]
    private float missRecovery = 0.6f;
    [SerializeField, Min(0f)]
    private float stunDuration = 2.2f;

    [Header("Rocks")]
    [SerializeField]
    private RockFall rockFall;

    [Header("Telegraph")]
    [SerializeField]
    private RectIndicator indicator;
    [SerializeField, Min(0.5f)]
    private float laneLength = 8f;
    [SerializeField, Min(0.1f)]
    private float laneWidth = 1.4f;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();
    [SerializeField]
    private AttackSounds crashSounds = new AttackSounds();

    private RectIndicator _runtimeIndicator;

    private void Reset()
    {
        MinRange = 3f;
        MaxRange = 14f;
        Cooldown = 8f;

        targetLayers = LayerMask.GetMask("Player");
        obstacleLayers = LayerMask.GetMask("Walls", "Default");
    }

    private void Awake()
    {
        if (targetLayers.value == 0)
            targetLayers = LayerMask.GetMask("Player");
        if (obstacleLayers.value == 0)
            obstacleLayers = LayerMask.GetMask("Walls", "Default");

        if (rockFall == null)
            rockFall = GetComponentInParent<RockFall>();
    }

    private void OnDestroy()
    {
        if (_runtimeIndicator != null)
            Destroy(_runtimeIndicator.gameObject);
    }

    public override IEnumerator Execute(Minotaur boss)
    {
        var lane = ResolveIndicator();
        var direction = Aim(boss, boss.Facing);

        sounds.PlayWindup(boss.Position);
        PlayAttack(boss, windup);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            if (elapsed < windup * trackFraction)
                direction = Aim(boss, direction);

            boss.FaceTowards(direction);

            lane.Aim(boss.Position, direction, ClearLength(boss, direction, laneLength), laneWidth,
                Mathf.Lerp(startFill, 1f, elapsed / windup), boss.YScale);
            lane.SetVisible(true);

            yield return null;
        }

        EndAttack(boss);

        lane.Flash();
        sounds.PlaySwing(boss.Position);

        var outcome = Outcome.Timeout;

        for (var elapsed = 0f; elapsed < maxDuration; elapsed += Time.deltaTime) {
            var turn = turnSpeed * Mathf.Deg2Rad * Time.deltaTime;
            direction = ((Vector2)Vector3.RotateTowards(direction, Aim(boss, direction), turn, 0f)).normalized;

            boss.FaceTowards(direction);

            var step = Isometric.ToScreen(direction * (speed * Time.deltaTime), boss.YScale);

            if (Collide(boss, step, out var travel, out outcome)) {
                MoveBy(boss, step.normalized * travel);
                break;
            }

            MoveBy(boss, step);

            var left = speed * (maxDuration - elapsed);
            lane.Aim(boss.Position, direction, ClearLength(boss, direction, Mathf.Min(laneLength, left)), laneWidth,
                1f, boss.YScale);

            yield return null;
        }

        lane.SetVisible(false);

        switch (outcome) {
            case Outcome.Pillar:
                crashSounds.PlayImpact(boss.Position);

                if (rockFall != null && boss.Target != null)
                    rockFall.Drop(boss.TargetPosition, boss.YScale);

                // Open to punishment until it shakes it off
                yield return new WaitForSeconds(stunDuration);
                break;
            case Outcome.Wall:
                crashSounds.PlayImpact(boss.Position);
                yield return new WaitForSeconds(wallRecovery);
                break;
            case Outcome.Target:
                yield return new WaitForSeconds(targetRecovery);
                break;
            default:
                yield return new WaitForSeconds(missRecovery);
                break;
        }
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
        if (_runtimeIndicator != null)
            _runtimeIndicator.SetVisible(false);
    }

    private bool Collide(Minotaur boss, Vector2 step, out float travel, out Outcome outcome)
    {
        travel = 0f;
        outcome = Outcome.Timeout;

        var distance = step.magnitude;
        if (distance <= 0.0001f)
            return false;

        var direction = step / distance;
        var hits = Physics2D.CircleCastAll(boss.Position, bodyRadius, direction, distance + skin,
            targetLayers.value | obstacleLayers.value);

        var nearest = Mathf.Infinity;
        RaycastHit2D struck = default;

        foreach (var hit in hits) {
            if (hit.collider == null || hit.collider.transform.IsChildOf(boss.transform))
                continue;
            if (hit.distance >= nearest)
                continue;

            var layer = hit.collider.gameObject.layer;
            var isTarget = InMask(targetLayers, layer) && hit.collider.GetComponentInParent<PlayerHealth>() != null;

            if (!isTarget && (hit.collider.isTrigger || !InMask(obstacleLayers, layer)))
                continue;

            nearest = hit.distance;
            struck = hit;
        }

        if (struck.collider == null)
            return false;

        travel = Mathf.Max(0f, nearest - skin);

        var target = InMask(targetLayers, struck.collider.gameObject.layer)
            ? struck.collider.GetComponentInParent<PlayerHealth>()
            : null;

        if (target != null) {
            outcome = Outcome.Target;
            target.TakeDamage(damage);
            sounds.PlayImpact(struck.point);
        } else {
            outcome = struck.collider.GetComponentInParent<BossPillar>() != null ? Outcome.Pillar : Outcome.Wall;
        }

        return true;
    }

    private float ClearLength(Minotaur boss, Vector2 groundDirection, float length)
    {
        if (obstacleLayers.value == 0)
            return length;

        var screen = Isometric.ToScreen(groundDirection * length, boss.YScale);
        var screenLength = screen.magnitude;

        if (screenLength <= 0.0001f)
            return length;

        var clear = screenLength;

        foreach (var hit in Physics2D.CircleCastAll(boss.Position, bodyRadius, screen / screenLength, screenLength,
                     obstacleLayers)) {
            if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(boss.transform))
                continue;

            clear = Mathf.Min(clear, hit.distance);
        }

        return length * clear / screenLength;
    }

    private static Vector2 Aim(Minotaur boss, Vector2 fallback)
    {
        var direction = boss.DirectionToTarget;
        return direction != Vector2.zero ? direction : fallback;
    }

    private static void MoveBy(Minotaur boss, Vector2 delta)
    {
        var position = boss.transform.position;
        boss.transform.position = new Vector3(position.x + delta.x, position.y + delta.y, position.z);
    }

    private static bool InMask(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;

    private RectIndicator ResolveIndicator()
    {
        if (indicator != null)
            return indicator;

        if (_runtimeIndicator == null) {
            var host = new GameObject("Minotaur Charge Indicator");
            _runtimeIndicator = host.AddComponent<RectIndicator>();
        }

        return _runtimeIndicator;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.7f);

        var origin = (Vector2)transform.position;
        var yScale = Isometric.DefaultYScale;
        var side = new Vector2(0f, laneWidth * 0.5f);
        var along = new Vector2(laneLength, 0f);

        Vector2 Corner(Vector2 ground) => origin + Isometric.ToScreen(ground, yScale);

        Gizmos.DrawLine(Corner(side), Corner(along + side));
        Gizmos.DrawLine(Corner(-side), Corner(along - side));
        Gizmos.DrawLine(Corner(side), Corner(-side));
        Gizmos.DrawLine(Corner(along + side), Corner(along - side));
    }
}
