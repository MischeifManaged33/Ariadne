using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// FMOD Studio manager
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Banks")]
    [SerializeField, BankRef]
    private List<string> banks = new List<string> { "Master", "Master.strings" };
    private bool loadSampleData = true;

    [Header("Music")]
    [SerializeField] private EventReference mazeMusic;
    [SerializeField] private EventReference bossMusic;

    [Header("Mixer")]
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private readonly List<string> loadedBanks = new List<string>();

    private EventInstance musicInstance;
    private EventReference currentMusic;

    private Bus musicBus;
    private Bus sfxBus;

    public bool BanksLoaded { get; private set; }

    public float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyBusVolume(musicBus, musicVolume);
        }
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyBusVolume(sfxBus, sfxVolume);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadBanks();
        CacheBuses();
    }

    private void Start()
    {
        PlayMazeMusic();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        StopMusic(allowFadeOut: false);
        UnloadBanks();

        Instance = null;
    }

    private void LoadBanks()
    {
        foreach (string bank in banks)
        {
            if (string.IsNullOrEmpty(bank) || loadedBanks.Contains(bank))
                continue;

            try
            {
                RuntimeManager.LoadBank(bank, loadSampleData);
                loadedBanks.Add(bank);
            }
            catch (BankLoadException exception)
            {
                Debug.LogException(exception, this);
            }
        }

        if (loadSampleData)
            RuntimeManager.WaitForAllSampleLoading();

        BanksLoaded = loadedBanks.Count == banks.Count;
    }

    private void UnloadBanks()
    {
        foreach (string bank in loadedBanks)
            RuntimeManager.UnloadBank(bank);

        loadedBanks.Clear();
        BanksLoaded = false;
    }

    private void CacheBuses()
    {
        musicBus = GetBus(musicBusPath);
        sfxBus = GetBus(sfxBusPath);

        ApplyBusVolume(musicBus, musicVolume);
        ApplyBusVolume(sfxBus, sfxVolume);
    }

    private Bus GetBus(string path)
    {
        if (string.IsNullOrEmpty(path))
            return default;

        if (RuntimeManager.StudioSystem.getBus(path, out Bus bus) != FMOD.RESULT.OK)
        {
            Debug.LogWarning($"No FMOD bus at '{path}'.", this);
            return default;
        }

        return bus;
    }

    private static void ApplyBusVolume(Bus bus, float volume)
    {
        if (bus.isValid())
            bus.setVolume(volume);
    }

    public void PlayMazeMusic()
    {
        PlayMusic(mazeMusic);
    }

    public void PlayBossMusic()
    {
        PlayMusic(bossMusic);
    }

    // Play the music that will loop
    public void PlayMusic(EventReference music)
    {
        if (music.IsNull)
        {
            Debug.LogWarning("No music event was assigned.", this);
            return;
        }

        if (musicInstance.isValid() && currentMusic.Guid == music.Guid)
            return;

        StopMusic();

        musicInstance = RuntimeManager.CreateInstance(music);
        musicInstance.start();

        currentMusic = music;
    }

    public void StopMusic(bool allowFadeOut = true)
    {
        if (!musicInstance.isValid())
            return;

        musicInstance.stop(allowFadeOut
            ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT
            : FMOD.Studio.STOP_MODE.IMMEDIATE);

        musicInstance.release();
        musicInstance.clearHandle();

        currentMusic = default;
    }

    // Parameter control for the music event
    public void SetMusicParameter(string name, float value)
    {
        if (musicInstance.isValid())
            musicInstance.setParameterByName(name, value);
    }

    // Parameter control for the music event with a label
    public void SetMusicParameter(string name, string label)
    {
        if (musicInstance.isValid())
            musicInstance.setParameterByNameWithLabel(name, label);
    }

    // Play a one shot SFX at a world position
    public void PlayOneShot(EventReference sound, Vector3 position = default)
    {
        Sfx.PlayOneShot(sound, position);
    }

    // Play a one shot SFX attached to a GameObject
    public void PlayOneShotAttached(EventReference sound, GameObject source)
    {
        Sfx.PlayAttached(sound, source);
    }

    // Create a sound instance
    public EventInstance CreateInstance(EventReference sound)
    {
        return RuntimeManager.CreateInstance(sound);
    }

    public void PauseAll(bool paused)
    {
        RuntimeManager.PauseAllEvents(paused);
    }
}
