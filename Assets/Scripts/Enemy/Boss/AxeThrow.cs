using System.Collections;
using UnityEngine;

// Phase one ranged move
public class AxeThrow : BossMove
{
    [Header("Axe")]
    [SerializeField]
    private ThrownAxe axePrefab;
    [SerializeField]
    private Transform throwOrigin;
    [SerializeField, Min(0f)]
    private float spawnOffset = 0.7f;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float windup = 0.6f;
    [SerializeField, Min(0f)]
    private float recovery = 0.4f;
    [SerializeField, Range(0f, 1f)]
    private float aimLockFraction = 0.35f;
    [SerializeField, Min(0f)]
    private float aimTimeout = 2f;

    [Header("Prediction")]
    [SerializeField, Range(0f, 1f)]
    private float leadStrength = 1f;
    [SerializeField, Min(0f)]
    private float maxLeadTime = 1.5f;

    [Header("Telegraph")]
    [SerializeField]
    private LineIndicator indicator;
    [SerializeField, Min(0.5f)]
    private float indicatorLength = 4.5f;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    public ThrownAxe ActiveAxe { get; private set; }
    public bool HasAxeInHand => ActiveAxe == null;

    private float AxeSpeed => axePrefab.Speed;

    private Transform _tracked;
    private Rigidbody2D _targetBody;
    private Vector2 _lastTargetPosition;
    private Vector2 _sampledVelocity;

    private void Reset()
    {
        MinRange = 4f;
        MaxRange = 20f;
        Cooldown = 3.5f;
        OutOfRangeWeight = 0.15f;
    }

    private void OnDestroy()
    {
        if (ActiveAxe != null)
            Destroy(ActiveAxe.gameObject);
    }

    public override bool CanUse(Minotaur boss, float distanceToTarget)
    {
        return HasAxeInHand && base.CanUse(boss, distanceToTarget);
    }

    public override IEnumerator Execute(Minotaur boss)
    {
        _tracked = null;

        var direction = AimDirection(boss, boss.Facing);

        var lockTime = windup * (1f - aimLockFraction);

        sounds.PlayWindup(Origin(boss));
        PlayAttack(boss, windup);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            if (elapsed < lockTime)
                direction = AimDirection(boss, direction);

            boss.FaceTowards(direction);
            Telegraph(boss, direction, true);

            yield return null;
        }

        Throw(boss, direction);

        if (indicator != null)
            indicator.Flash();

        Telegraph(boss, direction, false);
        EndAttack(boss);

        var deadline = Time.time + aimTimeout;
        while (ActiveAxe != null && ActiveAxe.IsFlying && Time.time < deadline)
            yield return null;

        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (indicator != null)
            indicator.SetVisible(false);
    }

    public void PickUpAxe()
    {
        if (ActiveAxe == null)
            return;

        ActiveAxe.Retrieve();
        ActiveAxe = null;
    }

    // Aim ahead of target
    private Vector2 AimDirection(Minotaur boss, Vector2 fallback)
    {
        var direct = boss.DirectionToTarget;

        if (direct == Vector2.zero)
            return fallback;
        if (leadStrength <= 0f)
            return direct;

        var velocity = TargetVelocity(boss);
        if (velocity.sqrMagnitude <= 0.0001f)
            return direct;
        
        var origin = Origin(boss) + Isometric.ToScreen(direct * spawnOffset, boss.YScale);
        var lead = InterceptTime(boss.TargetPosition - origin, velocity, AxeSpeed);

        if (lead <= 0f)
            return direct;

        var aimPoint = boss.TargetPosition + velocity * (Mathf.Min(lead, maxLeadTime) * leadStrength);
        var predicted = Isometric.GroundDirection(origin, aimPoint, boss.YScale);

        return predicted != Vector2.zero ? predicted : direct;
    }

    private static float InterceptTime(Vector2 relative, Vector2 velocity, float speed)
    {
        var a = Vector2.Dot(velocity, velocity) - speed * speed;
        var b = 2f * Vector2.Dot(relative, velocity);
        var c = Vector2.Dot(relative, relative);

        if (Mathf.Abs(a) < 0.0001f)
            return Mathf.Abs(b) > 0.0001f ? Mathf.Max(0f, -c / b) : 0f;

        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return 0f;

        var root = Mathf.Sqrt(discriminant);
        var first = (-b - root) / (2f * a);
        var second = (-b + root) / (2f * a);

        if (first > 0f && second > 0f)
            return Mathf.Min(first, second);

        return Mathf.Max(first, second, 0f);
    }

    private Vector2 TargetVelocity(Minotaur boss)
    {
        var target = boss.Target;

        if (target == null)
            return Vector2.zero;

        if (_tracked != target) {
            _tracked = target;
            _targetBody = target.GetComponentInParent<Rigidbody2D>();
            _lastTargetPosition = target.position;
            _sampledVelocity = Vector2.zero;
        }

        if (_targetBody != null)
            return _targetBody.linearVelocity;

        var position = (Vector2)target.position;

        if (Time.deltaTime > 0f)
            _sampledVelocity = Vector2.Lerp(_sampledVelocity,
                (position - _lastTargetPosition) / Time.deltaTime, 0.35f);

        _lastTargetPosition = position;

        return _sampledVelocity;
    }

    private void Telegraph(Minotaur boss, Vector2 groundDirection, bool visible)
    {
        if (indicator == null)
            return;

        indicator.Aim(Origin(boss), groundDirection, indicatorLength, boss.YScale);
        indicator.SetVisible(visible);
    }

    private Vector2 Origin(Minotaur boss)
    {
        return throwOrigin != null ? (Vector2)throwOrigin.position : boss.Position;
    }

    private void Throw(Minotaur boss, Vector2 groundDirection)
    {
        var origin = Origin(boss);
        var spawn = origin + Isometric.ToScreen(groundDirection * spawnOffset, boss.YScale);
        var position = new Vector3(spawn.x, spawn.y, boss.transform.position.z);

        sounds.PlaySwing(position);

        ActiveAxe = Instantiate(axePrefab, position, Quaternion.identity);

        ActiveAxe.Launch(groundDirection, boss.YScale);
    }
}
