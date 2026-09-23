using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

// Fire FMOD oneshots
public static class Sfx
{

    // Play a sound at a world position
    public static void PlayOneShot(EventReference sound, Vector3 position = default)
    {
        if (sound.IsNull)
            return;

        try
        {
            RuntimeManager.PlayOneShot(sound, position);
        }
        catch (EventNotFoundException)
        {
            WarnOnce(sound);
        }
    }

    // Play a sound that follows a moving object until it finishes
    public static void PlayAttached(EventReference sound, GameObject source)
    {
        if (sound.IsNull)
            return;

        if (source == null)
        {
            PlayOneShot(sound);
            return;
        }

        try
        {
            RuntimeManager.PlayOneShotAttached(sound, source);
        }
        catch (EventNotFoundException)
        {
            WarnOnce(sound);
        }
    }

    private static void WarnOnce(EventReference sound)
    {
        return;
    }
}
