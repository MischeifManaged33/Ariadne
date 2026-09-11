using System;
using UnityEngine;

[Serializable]
public class BossPhase
{
    [SerializeField, Range(0f, 1f)]
    private float healthThreshold = 1f;
    [SerializeField, Min(0f)]
    private float moveInterval = 1.5f;
    [SerializeField, Min(0f)]
    private float transitionDuration = 1f;
    [SerializeField, Min(0.1f)]
    private float speedMultiplier = 1f;

    public float HealthThreshold => healthThreshold;
    public float MoveInterval => moveInterval;
    public float TransitionDuration => transitionDuration;
    public float SpeedMultiplier => speedMultiplier;

    public BossPhase()
    {
    }

    public BossPhase(float threshold, float interval, float speed)
    {
        healthThreshold = threshold;
        moveInterval = interval;
        speedMultiplier = speed;
    }
}
