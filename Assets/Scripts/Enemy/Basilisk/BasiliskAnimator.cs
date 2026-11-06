using System;
using UnityEngine;

[RequireComponent(typeof(Basilisk))]
public class BasiliskAnimator : MonoBehaviour
{
    // Matches AttackId in the controller
    public const int Peck = 1;

    [Serializable]
    private struct AttackAnimation
    {
        public int id;
        public AnimationClip windupClip;
    }

    [Header("Sprite")]
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [Header("Directions")]
    [SerializeField, Range(0f, 1f)]
    private float backDeadzone = 0.15f;
    [SerializeField]
    private bool backArtMirrored = true;

    [Header("Attacks")]
    [SerializeField]
    private AttackAnimation[] attacks = { new AttackAnimation { id = Peck } };

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackIdHash = Animator.StringToHash("AttackId");
    private static readonly int AttackingHash = Animator.StringToHash("Attacking");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private const string BackTag = "Back";
    private static readonly int BackHash = Animator.StringToHash("Back");
    private static readonly int TurningHash = Animator.StringToHash("Turning");
    private static readonly int TurnFrameHash = Animator.StringToHash("TurnFrame");

    private const float FrontLeft = 0.125f;
    private const float FrontRight = 0.375f;
    private const float BackRight = 0.625f;
    private const float BackLeft = 0.875f;

    private Basilisk _basilisk;
    private EnemyMover _mover;
    private bool _artFlipped;
    private bool _back;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        _basilisk = GetComponent<Basilisk>();
        _mover = GetComponent<EnemyMover>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            _artFlipped = spriteRenderer.flipX;
    }

    private void Update()
    {
        if (animator == null)
            return;

        var facing = _basilisk.Facing;
        _back = ResolveBack(facing, _back);

        animator.SetFloat(SpeedHash, _mover != null && _mover.IsMoving && _basilisk.IsAlive ? 1f : 0f);
        animator.SetBool(BackHash, _back);
        animator.SetBool(TurningHash, _basilisk.IsTurning);
        animator.SetFloat(TurnFrameHash, TurnFrame(facing));
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
            return;

        if (_basilisk.IsTurning)
            spriteRenderer.flipX = _artFlipped;
        else if (backArtMirrored && ShowingBackArt)
            spriteRenderer.flipX = _basilisk.ArtTurned == _artFlipped;
    }

    public void PlayAttack(int attackId, float windup)
    {
        if (animator == null)
            return;

        animator.SetInteger(AttackIdHash, attackId);
        animator.SetFloat(AttackSpeedHash, AttackSpeed(attackId, windup));
        animator.SetBool(AttackingHash, true);
        animator.SetTrigger(AttackHash);
    }

    public void EndAttack()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(AttackHash);
        animator.SetBool(AttackingHash, false);
    }

    private bool ShowingBackArt => animator != null && animator.GetCurrentAnimatorStateInfo(0).IsTag(BackTag);
    private float AttackSpeed(int attackId, float windup)
    {
        if (windup <= 0f || attacks == null)
            return 1f;

        foreach (var attack in attacks) {
            if (attack.id == attackId && attack.windupClip != null && attack.windupClip.length > 0f)
                return attack.windupClip.length / windup;
        }

        return 1f;
    }

    private static float TurnFrame(Vector2 facing)
    {
        if (facing.y <= 0f)
            return facing.x < 0f ? FrontLeft : FrontRight;

        return facing.x < 0f ? BackLeft : BackRight;
    }

    private bool ResolveBack(Vector2 facing, bool current)
    {
        if (facing.y > backDeadzone)
            return true;
        if (facing.y < -backDeadzone)
            return false;

        return current;
    }
}
