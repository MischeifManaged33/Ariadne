using UnityEngine;
using UnityEngine.UI;

// Binds the settings menu sliders to music and SFX bus volumes
public class VolumeSettings : MonoBehaviour
{
    [SerializeField]
    private Slider musicSlider;
    [SerializeField]
    private Slider sfxSlider;

    private void OnEnable()
    {
        SoundManager sound = SoundManager.Instance;

        if (musicSlider != null)
        {
            if (sound != null)
                musicSlider.SetValueWithoutNotify(sound.MusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            if (sound != null)
                sfxSlider.SetValueWithoutNotify(sound.SfxVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }
    }

    private void OnDisable()
    {
        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
    }

    private static void SetMusicVolume(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.MusicVolume = value;
    }

    private static void SetSfxVolume(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SfxVolume = value;
    }
}
