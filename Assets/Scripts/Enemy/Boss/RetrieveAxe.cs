using System.Collections;
using UnityEngine;

// Phase 1 move, gets back the axe, leaves self vulnerable
public class RetrieveAxe : BossMove
{
    [SerializeField]
    private AxeThrow throwMove;
    [SerializeField, Min(0.1f)]
    private float pickupRadius = 0.7f;
    [SerializeField, Min(0f)]
    private float pickupDelay = 0.4f;
    [SerializeField, Min(0f)]
    private float travelTimeout = 10f;
    [SerializeField, Range(0.1f, 3f)]
    private float speedMultiplier = 1.2f;
    [SerializeField]
    private bool recallWhenUnreachable = true;

    private void Reset()
    {
        MinRange = 0f;
        MaxRange = 100f;
        Cooldown = 0f;
    }

    private void Awake()
    {
        if (throwMove != null)
            return;

        var boss = GetComponentInParent<Minotaur>();
        throwMove = boss != null ? boss.GetComponentInChildren<AxeThrow>(true)
            : GetComponentInChildren<AxeThrow>(true);
    }

    private bool AxeIsOut => throwMove != null && throwMove.ActiveAxe != null;

    public override bool CanUse(Minotaur boss, float distanceToTarget)
    {
        return !IsOnCooldown && AxeIsOut && throwMove.ActiveAxe.IsStuck;
    }

    public override float GetSelectionWeight(Minotaur boss, int phaseIndex, float distanceToTarget)
    {
        return GetWeight(phaseIndex);
    }

    public override IEnumerator Execute(Minotaur boss)
    {
        if (!AxeIsOut)
            yield break;

        var axe = throwMove.ActiveAxe;

        if (boss.Mover != null)
            yield return boss.Mover.Follow(() => axe != null ? axe.Position : boss.Position,
                pickupRadius, travelTimeout, speedMultiplier);

        if (axe == null || !AxeIsOut)
            yield break;

        var reached = boss.Mover == null || boss.Mover.GroundDistanceTo(axe.Position) <= pickupRadius * 1.5f;

        if (!reached && !recallWhenUnreachable)
            yield break;

        yield return new WaitForSeconds(pickupDelay);

        throwMove.PickUpAxe();
    }
}
