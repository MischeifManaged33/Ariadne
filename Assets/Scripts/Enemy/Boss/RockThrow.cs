using System.Collections;
using UnityEngine;

// Throwing rocks for ranged option
public class RockThrow : BossMove
{
    [SerializeField]
    private RockFall rockFall;

    [Header("Weight")]
    [SerializeField, Min(0f)]
    private float nearWeight = 1f;
    [SerializeField, Min(0f)]
    private float farWeight = 3f;
    [SerializeField, Min(0f)]
    private float nearDistance = 3f;
    [SerializeField, Min(0f)]
    private float farDistance = 10f;

    [Header("Pickup")]
    [SerializeField, Min(0.1f)]
    private float pickupRadius = 1f;
    [SerializeField, Min(0f)]
    private float pickupDelay = 0.35f;
    [SerializeField, Min(0f)]
    private float travelTimeout = 5f;
    [SerializeField, Range(0.1f, 3f)]
    private float speedMultiplier = 1.2f;
    [SerializeField, Min(0f)]
    private float holdHeight = 4.4f;

    [Header("Throw")]
    [SerializeField, Min(0.05f)]
    private float windup = 0.8f;
    [SerializeField, Range(0f, 1f)]
    private float aimLockFraction = 0.3f;
    [SerializeField, Range(0f, 1f)]
    private float startFill = 0.1f;
    [SerializeField, Min(0.05f)]
    private float flightTime = 0.8f;
    [SerializeField, Min(0f)]
    private float arcHeight = 3f;
    [SerializeField, Min(0.1f)]
    private float impactRadius = 1.1f;
    [SerializeField, Min(0f)]
    private float damage = 20f;
    [SerializeField, Min(0.5f)]
    private float maxThrowRange = 12f;
    [SerializeField, Min(0f)]
    private float recovery = 0.5f;

    [Header("Prediction")]
    [SerializeField, Range(0f, 1f)]
    private float leadStrength = 0.8f;
    [SerializeField, Min(0f)]
    private float maxLeadTime = 0.8f;

    [Header("Audio")]
    [SerializeField]
    private AttackSounds sounds = new AttackSounds();

    private BossRock _held;
    private Transform _tracked;
    private Rigidbody2D _targetBody;

    private void Reset()
    {
        MinRange = 0f;
        MaxRange = 100f;
        Cooldown = 2f;
    }

    private void Awake()
    {
        if (rockFall == null)
            rockFall = GetComponentInParent<RockFall>();
    }

    private void OnDestroy()
    {
        if (_held != null)
            _held.Discard();
    }

    public override bool CanUse(Minotaur boss, float distanceToTarget)
    {
        return !IsOnCooldown && boss.Target != null && rockFall != null && rockFall.HasRock;
    }

    public override float GetSelectionWeight(Minotaur boss, int phaseIndex, float distanceToTarget)
    {
        var weight = GetWeight(phaseIndex);
        if (weight <= 0f)
            return 0f;

        var t = Mathf.InverseLerp(nearDistance, farDistance, distanceToTarget);

        return weight * Mathf.Lerp(nearWeight, farWeight, t);
    }

    public override IEnumerator Execute(Minotaur boss)
    {
        var rock = rockFall != null ? rockFall.Nearest(boss.Position, boss.YScale) : null;
        if (rock == null)
            yield break;

        if (boss.Mover != null)
            yield return boss.Mover.Follow(() => rock != null ? rock.Position : boss.Position,
                pickupRadius, travelTimeout, speedMultiplier);

        if (rock == null || !rock.CanPickUp)
            yield break;
        if (Isometric.GroundDistance(boss.Position, rock.Position, boss.YScale) > pickupRadius * 1.5f)
            yield break;

        boss.FaceTowards(Isometric.GroundDirection(boss.Position, rock.Position, boss.YScale));

        yield return new WaitForSeconds(pickupDelay);

        if (!rockFall.Take(rock))
            yield break;

        _held = rock;
        _tracked = null;

        var point = AimPoint(boss);
        var lockTime = windup * (1f - aimLockFraction);

        sounds.PlayWindup(boss.Position);
        PlayAttack(boss, windup);

        for (var elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime) {
            if (elapsed < lockTime)
                point = AimPoint(boss);

            boss.FaceTowards(Isometric.GroundDirection(boss.Position, point, boss.YScale));

            rock.HoldAt(HoldPoint(boss));
            rock.Aim(point, impactRadius, Mathf.Lerp(startFill, 1f, elapsed / windup));

            yield return null;
        }

        sounds.PlaySwing(boss.Position);

        rock.Lob(point, flightTime, arcHeight, impactRadius, damage);
        _held = null;

        EndAttack(boss);

        var deadline = Time.time + flightTime + 1f;
        while (rock != null && rock.IsFlying && Time.time < deadline)
            yield return null;

        yield return new WaitForSeconds(recovery);
    }

    public override void Cancel()
    {
        if (_held != null)
            _held.Discard();

        _held = null;
    }

    private Vector2 HoldPoint(Minotaur boss) => boss.Position + new Vector2(0f, holdHeight);

    private Vector2 AimPoint(Minotaur boss)
    {
        var point = boss.TargetPosition + TargetVelocity(boss) * (Mathf.Min(flightTime, maxLeadTime) * leadStrength);
        var ground = Isometric.ToGround(point - boss.Position, boss.YScale);

        if (ground.magnitude > maxThrowRange)
            point = boss.Position + Isometric.ToScreen(ground.normalized * maxThrowRange, boss.YScale);

        return point;
    }

    private Vector2 TargetVelocity(Minotaur boss)
    {
        if (leadStrength <= 0f || boss.Target == null)
            return Vector2.zero;

        if (_tracked != boss.Target) {
            _tracked = boss.Target;
            _targetBody = boss.Target.GetComponentInParent<Rigidbody2D>();
        }

        return _targetBody != null ? _targetBody.linearVelocity : Vector2.zero;
    }
}
