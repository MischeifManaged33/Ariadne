using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioClip mazeMusic;
    [SerializeField] private AudioClip bossMusic;

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)]
    private float musicVolume = 0.7f;

    [SerializeField, Min(0f)]
    private float fadeDuration = 1f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sourceA = CreateMusicSource();
        sourceB = CreateMusicSource();
        activeSource = sourceA;
    }

    private void Start()
    {
        PlayMazeMusic();
    }

    private AudioSource CreateMusicSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;

        return source;
    }

    public void PlayMazeMusic()
    {
        PlayMusic(mazeMusic);
    }

    public void PlayBossMusic()
    {
        PlayMusic(bossMusic);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("No music clip was assigned.", this);
            return;
        }

        // Do not restart a track that is already playing.
        if (activeSource.clip == clip && activeSource.isPlaying)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(CrossfadeTo(clip));
    }

    private IEnumerator CrossfadeTo(AudioClip newClip)
    {
        AudioSource oldSource = activeSource;
        AudioSource newSource =
            activeSource == sourceA ? sourceB : sourceA;

        newSource.clip = newClip;
        newSource.volume = 0f;
        newSource.Play();

        if (fadeDuration <= 0f)
        {
            oldSource.Stop();
            oldSource.volume = 0f;
            newSource.volume = musicVolume;
        }
        else
        {
            float elapsed = 0f;
            float startingOldVolume = oldSource.volume;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float amount =
                    Mathf.Clamp01(elapsed / fadeDuration);

                oldSource.volume = Mathf.Lerp(
                    startingOldVolume,
                    0f,
                    amount
                );

                newSource.volume = Mathf.Lerp(
                    0f,
                    musicVolume,
                    amount
                );

                yield return null;
            }

            oldSource.Stop();
            oldSource.volume = 0f;
            newSource.volume = musicVolume;
        }

        activeSource = newSource;
        fadeCoroutine = null;
    }
}