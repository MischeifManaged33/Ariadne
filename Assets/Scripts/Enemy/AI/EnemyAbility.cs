using System.Collections;
using UnityEngine;

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

    public bool IsOnCooldown => Time.time < _nextUsableTime;

    public float Weight { get => weight; protected set => weight = Mathf.Max(0f, value); }
    public float Cooldown { get => cooldown; protected set => cooldown = Mathf.Max(0f, value); }
    public float MinRange { get => minRange; protected set => minRange = Mathf.Max(0f, value); }
    public float MaxRange { get => maxRange; protected set => maxRange = Mathf.Max(0f, value); }
    public bool NeedsLineOfSight { get => needsLineOfSight; protected set => needsLineOfSight = value; }

    private float _nextUsableTime;

    protected virtual void OnEnable()
    {
        _nextUsableTime = Mathf.Max(_nextUsableTime, Time.time + firstUseDelay);
    }

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
