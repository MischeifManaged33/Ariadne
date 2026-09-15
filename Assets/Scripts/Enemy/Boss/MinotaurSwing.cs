using UnityEngine;

// Phase 1 move, a swing of the axe in a wide arc, usable only with axe
public class MinotaurSwing : MinotaurMelee
{
    protected override bool IsAvailable => HasAxe;

    protected override void Reset()
    {
        base.Reset();

        damage = 30f;
        arc = 150f;
        windup = 0.8f;
        Cooldown = 2f;
    }
}
