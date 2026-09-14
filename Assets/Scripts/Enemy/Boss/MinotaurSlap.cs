using UnityEngine;

// Phase 1 move, faster but shorter than swing, also the default
public class MinotaurSlap : MinotaurMelee
{
    protected override bool IsAvailable => AxeMove == null || !HasAxe;

    protected override void Reset()
    {
        base.Reset();

        damage = 18f;
        arc = 120f;
        windup = 0.45f;
        Cooldown = 1.5f;
    }
}
