using System.Collections;
using UnityEngine;


public abstract class BossMove : MonoBehaviour
{

    [Header("Selection")]
    [SerializeField]
    private float[] phaseWeights = { 1f, 1f, 1f };
    [SerializeField, Min(0f)]
    private float cooldown = 3f;
    [SerializeField, Range(0f, 1f)]
    private float outOfRangeWeight = 0f;

    [Header("Range")]
    [SerializeField, Min(0f)]
    private float minRange = 0f;
    [SerializeField, Min(0f)]
    private float maxRange = 20f;

    public bool IsOnCooldown => Time.time < _nextUsableTime;

    public float MinRange { get => minRange; protected set => minRange = Mathf.Max(0f, value); }
    public float MaxRange { get => maxRange; protected set => maxRange = Mathf.Max(0f, value); }
    public float Cooldown { get => cooldown; protected set => cooldown = Mathf.Max(0f, value); }
    public float OutOfRangeWeight { get => outOfRangeWeight; protected set => outOfRangeWeight = Mathf.Clamp01(value); }

    private float _nextUsableTime;

    public float GetWeight(int phaseIndex)
    {
        if (phaseWeights == null || phaseWeights.Length == 0)
            return 0f;

        return Mathf.Max(0f, phaseWeights[Mathf.Clamp(phaseIndex, 0, phaseWeights.Length - 1)]);
    }

    public bool InRange(float distanceToTarget)
    {
        return distanceToTarget >= minRange && distanceToTarget <= maxRange;
    }

    public virtual float GetSelectionWeight(Minotaur boss, int phaseIndex, float distanceToTarget)
    {
        var weight = GetWeight(phaseIndex);
        if (weight <= 0f)
            return 0f;

        return InRange(distanceToTarget) ? weight : weight * outOfRangeWeight;
    }

    public virtual bool CanUse(Minotaur boss, float distanceToTarget)
    {
        if (IsOnCooldown)
            return false;

        return InRange(distanceToTarget) || outOfRangeWeight > 0f;
    }

    public abstract IEnumerator Execute(Minotaur boss);

    public virtual void Cancel()
    {
    }

    public void BeginCooldown() => _nextUsableTime = Time.time + cooldown;

    public void ResetCooldown() => _nextUsableTime = 0f;

    protected static int ResolveMask(LayerMask mask)
    {
        return mask.value != 0 ? mask.value : Physics2D.AllLayers;
    }
}
