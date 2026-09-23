using System;
using UnityEngine;

// Animator for the minotaur
[RequireComponent(typeof(Minotaur))]
public class MinotaurAnimator : MonoBehaviour
{

    [Serializable]
    private struct AttackAnimation
    {
        public int id;
        [Min(0f)]
        public float clipWindup;
    }

    [Header("Sprite")]
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [Header("Directions")]
    [SerializeField, Range(0f, 30f)]
    private float directionHysteresis = 10f;
    [SerializeField]
    private bool flipForLeft;

    [Header("Speed")]
    [SerializeField, Min(0f)]
    private float moveThreshold = 0.05f;
    [SerializeField, Range(0f, 0.5f)]
    private float speedDamping = 0.08f;

    [Header("Attacks")]
    [SerializeField]
    private AttackAnimation[] attacks;

    // Hash for animator
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackIdHash = Animator.StringToHash("AttackId");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int AttackingHash = Animator.StringToHash("Attacking");
    private static readonly int HurtHash = Animator.StringToHash("Hurt");
    private static readonly int PhaseHash = Animator.StringToHash("Phase");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private Minotaur _boss;
    private Vector2 _lastPosition;
    private float _speed;
    private int _direction;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        _boss = GetComponent<Minotaur>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _lastPosition = transform.position;
    }

    private void OnEnable()
    {
        _boss.Damaged += OnDamaged;
        _boss.PhaseChanged += OnPhaseChanged;
        _boss.Died += OnDied;
    }

    private void OnDisable()
    {
        _boss.Damaged -= OnDamaged;
        _boss.PhaseChanged -= OnPhaseChanged;
        _boss.Died -= OnDied;
    }

    private void FixedUpdate()
    {
        var position = (Vector2)transform.position;
        var travelled = Isometric.ToGround(position - _lastPosition, _boss.YScale);
        _lastPosition = position;

        var speed = Time.fixedDeltaTime > 0f ? travelled.magnitude / Time.fixedDeltaTime : 0f;

        _speed = speed < moveThreshold ? 0f : speed;
    }

    private void LateUpdate()
    {
        if (animator == null)
            return;

        var facing = _boss.Facing;

        _direction = Quantize(facing, _direction);

        animator.SetFloat(MoveXHash, facing.x);
        animator.SetFloat(MoveYHash, facing.y);
        animator.SetFloat(SpeedHash, _speed, speedDamping, Time.deltaTime);
        animator.SetInteger(DirectionHash, _direction);

        if (flipForLeft && spriteRenderer != null && Mathf.Abs(facing.x) > 0.01f)
            spriteRenderer.flipX = facing.x < 0f;
    }

    // Holds facing so no need so I don't gotta flip the sprite manually
    private int Quantize(Vector2 facing, int current)
    {
        var count = 2;

        if (facing.sqrMagnitude < 0.0001f)
            return current;

        var sector = 360f / count;
        var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        var candidate = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / sector) % count;

        if (candidate == current)
            return current;

        var fromCurrent = Mathf.Abs(Mathf.DeltaAngle(current * sector, angle));

        return fromCurrent > sector * 0.5f + directionHysteresis ? candidate : current;
    }

    public void PlayAttack(int attackId, float windup)
    {
        if (animator == null)
            return;

        animator.SetInteger(AttackIdHash, attackId);
        animator.SetBool(AttackingHash, true);
        animator.SetFloat(AttackSpeedHash, ResolveAttackSpeed(attackId, windup));
        animator.SetTrigger(AttackHash);
    }

    public void EndAttack()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(AttackHash);
        animator.SetBool(AttackingHash, false);
        animator.SetFloat(AttackSpeedHash, 1f);
    }

    private float ResolveAttackSpeed(int attackId, float windup)
    {
        if (windup <= 0f || attacks == null)
            return 1f;

        foreach (var attack in attacks) {
            if (attack.id == attackId && attack.clipWindup > 0f)
                return attack.clipWindup / windup;
        }

        return 1f;
    }

    private void OnDamaged(float amount)
    {
        if (animator != null)
            animator.SetTrigger(HurtHash);
    }

    private void OnPhaseChanged(int index)
    {
        if (animator != null)
            animator.SetInteger(PhaseHash, index);
    }

    private void OnDied()
    {
        EndAttack();

        if (animator != null)
            animator.SetTrigger(DieHash);
    }
}
