using System.Collections;
using UnityEngine;


public abstract class BossMove : MonoBehaviour
{
    [SerializeField]
    private string moveName;

    [Header("Selection")]
    [SerializeField]
    private float[] phaseWeights = { 1f, 1f, 1f };
    [SerializeField, Min(0f)]
    private float cooldown = 3f;

    [Header("Range")]
    [SerializeField, Min(0f)]
    private float minRange = 0f;
    [SerializeField, Min(0f)]
    private float maxRange = 20f;

    public string MoveName => string.IsNullOrEmpty(moveName) ? GetType().Name : moveName;
    public bool IsOnCooldown => Time.time < _nextUsableTime;

    private float _nextUsableTime;

    public float GetWeight(int phaseIndex)
    {
        if (phaseWeights == null || phaseWeights.Length == 0)
            return 0f;

        return Mathf.Max(0f, phaseWeights[Mathf.Clamp(phaseIndex, 0, phaseWeights.Length - 1)]);
    }

    public virtual bool CanUse(Minotaur boss, float distanceToTarget)
    {
        if (IsOnCooldown)
            return false;

        return distanceToTarget >= minRange && distanceToTarget <= maxRange;
    }

    public abstract IEnumerator Execute(Minotaur boss);

    public virtual void Cancel()
    {
    }

    public void BeginCooldown() => _nextUsableTime = Time.time + cooldown;

    public void ResetCooldown() => _nextUsableTime = 0f;
}
