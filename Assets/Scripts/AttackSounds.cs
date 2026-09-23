using System;
using FMODUnity;
using UnityEngine;

[Serializable]
public class AttackSounds
{
    [SerializeField]
    private EventReference windup;

    [SerializeField]
    private EventReference swing;

    [SerializeField]
    private EventReference impact;

    public void PlayWindup(Vector3 position) => Sfx.PlayOneShot(windup, position);
    public void PlaySwing(Vector3 position) => Sfx.PlayOneShot(swing, position);
    public void PlayImpact(Vector3 position) => Sfx.PlayOneShot(impact, position);
    public void PlaySwingAttached(GameObject source) => Sfx.PlayAttached(swing, source);
}
