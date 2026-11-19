using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

// Same as boss' move
public abstract class EnemyAbility : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField, Min(0f)]
    private float weight = 1f;
    [SerializeField, Min(0f)]
    private float cooldown = 4f;
    [SerializeField, Min(0f)]
    private float firstUseDelay = 0.5f;

    [Header("Range")]
    [SerializeField, Min(0f)]
    private float minRange;
    [SerializeField, Min(0f)]
    private float maxRange = 8f;
    [SerializeField]
    private bool needsLineOfSight = true;

    [Header("Animation")]
    [SerializeField]
    private AnimationClip windupClip;
    [SerializeField, FormerlySerializedAs("landClip")]
    private AnimationClip launchClip;
    [SerializeField]
    private bool fitWindupToTiming = true;

    public bool IsOnCooldown => Time.time < _nextUsableTime;

    public float Weight { get => weight; protected set => weight = Mathf.Max(0f, value); }
    public float Cooldown { get => cooldown; protected set => cooldown = Mathf.Max(0f, value); }
    public float MinRange { get => minRange; protected set => minRange = Mathf.Max(0f, value); }
    public float MaxRange { get => maxRange; protected set => maxRange = Mathf.Max(0f, value); }
    public bool NeedsLineOfSight { get => needsLineOfSight; protected set => needsLineOfSight = value; }

    private float _nextUsableTime;

    private Animator _animator;
    private bool _animatorResolved;
    private PlayableGraph _clipGraph;

    protected virtual void OnEnable()
    {
        _nextUsableTime = Mathf.Max(_nextUsableTime, Time.time + firstUseDelay);
    }

    protected virtual void OnDisable() => StopAnimation();

    public bool InRange(float distanceToTarget)
    {
        return distanceToTarget >= minRange && distanceToTarget <= maxRange;
    }

    public virtual float GetSelectionWeight(EnemyAI ai, float distanceToTarget)
    {
        return weight;
    }

    public virtual bool CanUse(EnemyAI ai, float distanceToTarget)
    {
        if (weight <= 0f || IsOnCooldown)
            return false;
        if (!InRange(distanceToTarget))
            return false;

        return !needsLineOfSight || ai.CanSeeTarget;
    }

    public abstract IEnumerator Execute(EnemyAI ai);

    public virtual void Cancel()
    {
    }

    // Consolidated the animation playing logic
    protected void PlayWindup(float duration = 0f) =>
        PlayClip(windupClip, fitWindupToTiming ? duration : 0f);

    protected void PlayLaunch() => PlayClip(launchClip);

    protected void PlayClip(AnimationClip clip, float fitDuration = 0f)
    {
        var animator = ResolveAnimator();
        if (clip == null || animator == null || !animator.isActiveAndEnabled)
            return;

        StopAnimation();

        var playable = AnimationPlayableUtilities.PlayClip(animator, clip, out _clipGraph);
        if (fitDuration > 0f && clip.length > 0f)
            playable.SetSpeed(clip.length / fitDuration);
    }

    public void StopAnimation()
    {
        if (_clipGraph.IsValid())
            _clipGraph.Destroy();
    }

    private Animator ResolveAnimator()
    {
        if (_animatorResolved)
            return _animator;

        _animatorResolved = true;
        _animator = GetComponentInParent<Animator>();

        if (_animator == null) {
            var enemy = GetComponentInParent<Enemy>();
            if (enemy != null)
                _animator = enemy.GetComponentInChildren<Animator>();
        }

        return _animator;
    }

    public void BeginCooldown() => _nextUsableTime = Time.time + cooldown;

    public void ResetCooldown() => _nextUsableTime = 0f;

    protected static int ResolveMask(LayerMask mask)
    {
        return mask.value != 0 ? mask.value : Physics2D.AllLayers;
    }

    protected static LayerMask PlayerMask()
    {
        var mask = LayerMask.GetMask("Player");

        return mask != 0 ? mask : LayerMask.GetMask("Default");
    }
}
